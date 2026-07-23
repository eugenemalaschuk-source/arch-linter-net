using System.Diagnostics;
using System.Reflection;
using System.Text;
using ArchLinterNet.Core.Discovery;
using ArchLinterNet.Core.Model;

namespace ArchLinterNet.Core.BuildState;

public interface IBuildStatePreparationService
{
    BuildStatePreflightResult Prepare(BuildStatePreflightRequest request);
}

// Orchestrates all three preparation modes (Ordinary, NoRestore, EnsureBuilt) through one entry
// point so CLI and Testing API share identical state-machine behavior. Only this service
// constructs the `dotnet build` invocation — see the security/trust boundary in
// docs/internal/analysis-build-state-blueprint.md: structured argv only, never a shell string,
// never sourced from policy/baseline/receipt/cache content.
public sealed class BuildStatePreparationService : IBuildStatePreparationService
{
    private const string ContractName = "build-state-preflight";

    public BuildStatePreflightResult Prepare(BuildStatePreflightRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.NoRestore)
        {
            BuildStatePreflightResult restoreCheck = CheckRestorePrerequisites(request);
            if (restoreCheck.Blocked)
            {
                return restoreCheck;
            }
        }

        return request.PreparationMode == BuildPreparationMode.EnsureBuilt
            ? EnsureBuilt(request)
            : BuildStatePreflightEvaluator.Evaluate(request);
    }

    // dotnet restore/build resolves prerequisites from the local NuGet cache without network
    // access whenever a project has already been restored once; the presence of
    // obj/project.assets.json is the same signal `dotnet build --no-restore` itself relies on.
    private static BuildStatePreflightResult CheckRestorePrerequisites(BuildStatePreflightRequest request)
    {
        List<BuildStatePreflightDiagnostic> blocking = new();
        foreach (ArchitectureDiscoveredProject project in request.ProjectDiscovery.DiscoveredProjects)
        {
            if (!BuildStatePreflightEvaluator.IsRelevantToResolution(project, request.Resolution))
            {
                continue;
            }

            string? projectDirectory = Path.GetDirectoryName(
                BuildStatePathResolution.ResolveAbsoluteProjectPath(request.RepositoryRoot, project.Path));
            string assetsPath = projectDirectory == null
                ? string.Empty
                : Path.Combine(projectDirectory, "obj", "project.assets.json");

            if (projectDirectory != null && File.Exists(assetsPath))
            {
                continue;
            }

            blocking.Add(new BuildStatePreflightDiagnostic(
                ContractName,
                project.Path,
                BuildStatePreflightState.RestoreRequired,
                new BuildStatePreflightEvidence(
                    project.Path,
                    project.AssemblyName,
                    BuildCommand: $"dotnet restore \"{project.Path}\"",
                    Detail: "No prior restore output was found and --no-restore prevents restoring now. " +
                        "Run `dotnet restore` (or `--ensure-built` without --no-restore) first.")));
        }

        return new BuildStatePreflightResult(blocking);
    }

    private static BuildStatePreflightResult EnsureBuilt(BuildStatePreflightRequest request)
    {
        foreach (ArchitectureDiscoveredProject project in SelectGraphRoots(request.ProjectDiscovery))
        {
            request.CancellationToken.ThrowIfCancellationRequested();

            BuildStatePreflightDiagnostic? failure = InvokeDotnetBuild(project, request);
            if (failure != null)
            {
                return new BuildStatePreflightResult(new[] { failure });
            }
        }

        // The caller's ResolutionResult snapshot predates this build and cannot see assemblies
        // that did not exist yet — re-resolve from the freshly built output before evaluating,
        // otherwise a first-time build would always evaluate as missing-artifact.
        BuildStatePreflightRequest postBuildRequest = request with { Resolution = ResolveBuiltAssemblies(request) };

        BuildStatePreflightResult postBuildEvaluation = BuildStatePreflightEvaluator.Evaluate(postBuildRequest);
        WriteReceiptsForCurrentArtifacts(postBuildRequest, postBuildEvaluation);

        // Verified artifacts are re-checked once more from the freshly written receipts so the
        // TOCTOU window between build completion and receipt materialization cannot admit a
        // digest/identity change without aborting the session.
        return BuildStatePreflightEvaluator.Evaluate(postBuildRequest);
    }

    // Building the whole selected graph once — not once per project — means invoking `dotnet
    // build` only for projects that are not referenced by any other discovered project: MSBuild
    // transitively builds each root's project references, so a referenced project already gets
    // built exactly once as a dependency of its root(s), and is never built a second time
    // directly. Falls back to building every discovered project only in the (unusual) case where
    // reference cycles or an incomplete graph leave no unreferenced root at all.
    // internal (not private) so BuildStatePreparationServiceGraphRootTests can verify path
    // resolution directly against real (absolute) ArchitectureDiscoveredProjectReference.Path
    // values without needing a real dotnet build.
    internal static IReadOnlyList<ArchitectureDiscoveredProject> SelectGraphRoots(ProjectDiscoveryResult discovery)
    {
        // ArchitectureDiscoveredProjectReference.Path is already repository-relative — the same
        // canonical form every discovered project's own .Path has (both are produced by
        // ArchitectureProjectDiscoveryService's identical GetRelativePath helper) — so reference
        // paths are directly comparable to project paths with no filesystem resolution at all.
        // Combining a reference path with a filesystem directory here previously produced an
        // absolute path that could never match another project's repo-relative .Path, so no
        // project was ever recognized as referenced and every project was (wrongly) treated as
        // its own root.
        HashSet<string> referenced = new(
            discovery.DiscoveredProjects.SelectMany(project => project.ProjectReferences.Select(reference => reference.Path)),
            StringComparer.Ordinal);

        List<ArchitectureDiscoveredProject> roots = discovery.DiscoveredProjects
            .Where(project => !referenced.Contains(project.Path))
            .ToList();

        return roots.Count > 0 ? roots : discovery.DiscoveredProjects.ToList();
    }

    private static BuildStateResolvedAssemblies ResolveBuiltAssemblies(BuildStatePreflightRequest request)
    {
        List<Assembly> resolved = new();
        List<string> missing = new();

        foreach (ArchitectureDiscoveredProject project in request.ProjectDiscovery.DiscoveredProjects)
        {
            string? projectDirectory = Path.GetDirectoryName(
                BuildStatePathResolution.ResolveAbsoluteProjectPath(request.RepositoryRoot, project.Path));
            string? assemblyPath = projectDirectory == null
                ? null
                : FindBuiltAssembly(
                    projectDirectory, project.AssemblyName, request.RequestedConfiguration, request.RequestedTargetFramework);

            if (assemblyPath == null)
            {
                missing.Add(project.AssemblyName);
                continue;
            }

            resolved.Add(Assembly.LoadFrom(assemblyPath));
        }

        return new BuildStateResolvedAssemblies(resolved, missing);
    }

    // Standard SDK-style output layout is bin/<Configuration>/<TFM>/<AssemblyName>.dll. When the
    // caller requested a specific configuration and/or target framework, only a candidate whose
    // path segments match those exactly is acceptable — falling back to "the most recently
    // written candidate" when a requested value doesn't match would silently accept output built
    // for the wrong configuration/TFM as current, which is exactly the state this preflight
    // exists to reject. A fallback to the newest candidate is only used when the caller placed no
    // constraint on configuration/TFM at all, in which case there is no requested value to
    // violate — ambiguity is intentionally accepted there since ordinary preflight has no
    // request-vs-observed mismatch to check.
    private static string? FindBuiltAssembly(
        string projectDirectory, string assemblyName, string? configuration, string? targetFramework)
    {
        string binDirectory = Path.Combine(projectDirectory, "bin");
        if (!Directory.Exists(binDirectory))
        {
            return null;
        }

        string[] candidates = Directory.GetFiles(binDirectory, $"{assemblyName}.dll", SearchOption.AllDirectories);
        if (candidates.Length == 0)
        {
            return null;
        }

        if (configuration == null && targetFramework == null)
        {
            return candidates.OrderByDescending(File.GetLastWriteTimeUtc).First();
        }

        return candidates.FirstOrDefault(path => MatchesRequestedOutputPath(
            Path.GetRelativePath(binDirectory, path), configuration, targetFramework));
    }

    private static bool MatchesRequestedOutputPath(string relativePath, string? configuration, string? targetFramework)
    {
        string[] segments = relativePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        // segments: [Configuration, TargetFramework, ..., AssemblyName.dll] for the common
        // non-RID-specific layout; require the leading segments to match when requested.
        bool configurationMatches = configuration == null
            || (segments.Length > 0 && string.Equals(segments[0], configuration, StringComparison.OrdinalIgnoreCase));
        bool targetFrameworkMatches = targetFramework == null
            || (segments.Length > 1 && string.Equals(segments[1], targetFramework, StringComparison.OrdinalIgnoreCase));

        return configurationMatches && targetFrameworkMatches;
    }

    // Resolves an absolute path to the `dotnet` host executable rather than passing the bare
    // command name to ProcessStartInfo — an unqualified executable name is resolved by searching
    // PATH, which on some platforms/configurations can be influenced by an attacker-controlled
    // directory ahead of the legitimate install (a PATH/executable-hijacking risk); an absolute
    // path removes that ambiguity. Falls back to the bare command only if no absolute path can be
    // determined, so this still works in environments where DOTNET_ROOT isn't set and PATH search
    // is the only option (the same environments today's implicit-lookup behavior already relies
    // on).
    private static string ResolveDotnetExecutablePath()
    {
        string executableName = OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet";

        string? dotnetRoot = Environment.GetEnvironmentVariable("DOTNET_ROOT")
            ?? Environment.GetEnvironmentVariable("DOTNET_ROOT(x86)");
        if (dotnetRoot != null)
        {
            string candidate = Path.Combine(dotnetRoot, executableName);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        string? pathVariable = Environment.GetEnvironmentVariable("PATH");
        foreach (string directory in (pathVariable ?? string.Empty).Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            string candidate = Path.Combine(directory, executableName);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return executableName;
    }

    private static BuildStatePreflightDiagnostic? InvokeDotnetBuild(
        ArchitectureDiscoveredProject project, BuildStatePreflightRequest request)
    {
        List<string> arguments = new() { "build", project.Path, "--nologo" };
        if (request.RequestedConfiguration != null)
        {
            arguments.Add("-c");
            arguments.Add(request.RequestedConfiguration);
        }

        if (request.NoRestore)
        {
            arguments.Add("--no-restore");
        }

        ProcessStartInfo startInfo = new(ResolveDotnetExecutablePath())
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = request.RepositoryRoot,
        };
        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using Process process = new() { StartInfo = startInfo };
        StringBuilder stdOut = new();
        StringBuilder stdErr = new();

        // Both streams must be drained concurrently with the process running, not sequentially
        // after Start() — `dotnet build` can write enough to either pipe to fill its OS buffer,
        // and reading only one stream (or reading one fully before starting the other) blocks the
        // child process on the unread pipe while this process blocks on ReadToEnd/WaitForExit,
        // deadlocking both.
        process.OutputDataReceived += (_, e) => { if (e.Data != null) { stdOut.AppendLine(e.Data); } };
        process.ErrorDataReceived += (_, e) => { if (e.Data != null) { stdErr.AppendLine(e.Data); } };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        process.WaitForExit();

        if (process.ExitCode == 0)
        {
            return null;
        }

        string combinedOutput = (stdOut.ToString() + stdErr).Trim();
        return new BuildStatePreflightDiagnostic(
            ContractName,
            project.Path,
            BuildStatePreflightState.BuildFailed,
            new BuildStatePreflightEvidence(
                project.Path,
                project.AssemblyName,
                BuildCommand: string.Join(' ', arguments.Prepend("dotnet")),
                Detail: $"`dotnet build` failed with exit code {process.ExitCode}: {combinedOutput}"));
    }

    private static void WriteReceiptsForCurrentArtifacts(
        BuildStatePreflightRequest request, BuildStatePreflightResult evaluation)
    {
        Dictionary<string, ArchitectureDiscoveredProject> projectsByPath =
            request.ProjectDiscovery.DiscoveredProjects.ToDictionary(p => p.Path, StringComparer.Ordinal);

        foreach (BuildStatePreflightDiagnostic diagnostic in evaluation.Diagnostics)
        {
            // Write (or overwrite) a fresh receipt for every state where ExpectedOutputPath is
            // confidently *this* project's own artifact — UnverifiableArtifact (freshly built, no
            // receipt yet), StaleArtifact (a receipt already existed but had a stale fingerprint —
            // restricting this to only UnverifiableArtifact meant a rebuild after a source change
            // never got a new receipt written, so preflight kept reporting StaleArtifact even
            // immediately after a successful --ensure-built rebuild), and WrongConfiguration/
            // WrongTargetFramework (the artifact path itself already matched the request exactly —
            // see FindBuiltAssembly — only the existing receipt's recorded metadata was wrong).
            // WrongProjectOutput is deliberately excluded: it means the receipt at this path
            // claims a *different* assembly's identity, a real name/path collision that
            // overwriting would only mask rather than resolve. Current has nothing to update.
            bool ownsExpectedOutput = diagnostic.State is BuildStatePreflightState.UnverifiableArtifact
                or BuildStatePreflightState.StaleArtifact
                or BuildStatePreflightState.WrongConfiguration
                or BuildStatePreflightState.WrongTargetFramework;

            if (!ownsExpectedOutput
                || diagnostic.Evidence.ExpectedOutputPath == null
                || !projectsByPath.TryGetValue(diagnostic.Evidence.ProjectPath, out ArchitectureDiscoveredProject? project))
            {
                continue;
            }

            string assemblyPath = diagnostic.Evidence.ExpectedOutputPath;
            string fingerprint = BuildStateCanonicalHasher.ComputeBuildInputFingerprint(project.Path, request.RepositoryRoot);
            string assemblyDigest = BuildStateCanonicalHasher.ComputeContentDigest(assemblyPath);

            // The receipt's configuration/TFM record what was actually resolved for this build —
            // ResolveBuiltAssemblies only accepted a candidate whose own output path matched a
            // requested configuration/TFM (see FindBuiltAssembly/MatchesRequestedOutputPath), so
            // recording the request values here is sound: any mismatch would already have made
            // this project unresolved (missing), never reaching this receipt-writing step.
            BuildReceiptStore.Write(assemblyPath, new BuildReceiptV1(
                project.Path,
                project.AssemblyName,
                request.RequestedConfiguration,
                request.RequestedTargetFramework,
                fingerprint,
                assemblyDigest));
        }
    }
}
