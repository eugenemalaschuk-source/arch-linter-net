using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Text.Json;
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

            if (projectDirectory != null && HasRestoredTargets(assetsPath))
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

    // A weaker signal than a real restore verification (it doesn't cross-check the assets file's
    // recorded package identities/versions against the project's current PackageReferences, so a
    // stale assets.json can still pass this and fail later inside `dotnet build --no-restore` as
    // a generic build failure instead of this typed restore-required diagnostic — a known,
    // documented limitation). It is a meaningful improvement over bare File.Exists, though: a
    // trivially empty or corrupt assets file (e.g. `{}`) — which a real `dotnet restore` never
    // produces — no longer passes as "restored".
    private static bool HasRestoredTargets(string assetsPath)
    {
        if (!File.Exists(assetsPath))
        {
            return false;
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(assetsPath));
            return document.RootElement.TryGetProperty("targets", out JsonElement targets)
                && targets.ValueKind == JsonValueKind.Object
                && targets.EnumerateObject().Any();
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static BuildStatePreflightResult EnsureBuilt(BuildStatePreflightRequest request)
    {
        request.CancellationToken.ThrowIfCancellationRequested();

        BuildStatePreflightDiagnostic? failure = InvokeGraphBuild(request);
        if (failure != null)
        {
            return new BuildStatePreflightResult(new[] { failure });
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

    // Building the whole selected graph once — not once per project, and not once per independent
    // root — means generating a single temporary .slnx solution file listing every discovered
    // project and invoking `dotnet build` on it exactly once. A solution build shares one MSBuild
    // graph across every listed entry point: a project referenced by more than one other listed
    // project (or by more than one independent root) is still built exactly once, and multiple
    // otherwise-unconnected project trees are covered by the same single invocation instead of one
    // invocation per tree. This replaces an earlier per-project-root loop that invoked `dotnet
    // build` once per unreferenced project and could still rebuild a dependency shared by more
    // than one root, or invoke the process multiple times for disconnected trees.
    private static string WriteTemporaryGraphSolution(BuildStatePreflightRequest request)
    {
        IEnumerable<string> projectEntries = SelectRelevantProjectsWithTransitiveReferences(request)
            .Select(project => BuildStatePathResolution.ResolveAbsoluteProjectPath(request.RepositoryRoot, project.Path))
            .Distinct(StringComparer.Ordinal)
            .Select(absolutePath => $"    <Project Path=\"{System.Security.SecurityElement.Escape(absolutePath)}\" />");

        string content = "<Solution>" + Environment.NewLine
            + "  <Folder Name=\"/build-state-preflight/\">" + Environment.NewLine
            + string.Join(Environment.NewLine, projectEntries) + Environment.NewLine
            + "  </Folder>" + Environment.NewLine
            + "</Solution>" + Environment.NewLine;

        string path = Path.Combine(Path.GetTempPath(), $"archlinternet-ensure-built-{Guid.NewGuid():N}.slnx");
        File.WriteAllText(path, content);
        return path;
    }

    // A project discovered only to feed project-scope coverage (never attempted by assembly
    // resolution — see BuildStatePreflightEvaluator.IsRelevantToResolution) has no business being
    // part of the ensure-built build: it may not even be restorable/buildable on its own, and
    // including it in the shared .slnx would fail the whole graph build for every relevant
    // project too. Build only the relevant projects plus whatever they transitively reference —
    // a relevant project's own dependency closure is still needed even if a referenced project
    // itself isn't independently relevant.
    private static IReadOnlyCollection<ArchitectureDiscoveredProject> SelectRelevantProjectsWithTransitiveReferences(
        BuildStatePreflightRequest request)
    {
        ArchitectureDiscoveredProject[] seeds = request.ProjectDiscovery.DiscoveredProjects
            .Where(project => BuildStatePreflightEvaluator.IsRelevantToResolution(project, request.Resolution))
            .ToArray();

        // The pre-build resolution snapshot can be empty-empty (nothing resolved, nothing missing
        // yet) on a genuinely first-ever build — assembly resolution hasn't had anything to find
        // or report missing. Relevance can't be determined from that snapshot in that case, so
        // fall back to the full discovered set rather than building nothing.
        if (seeds.Length == 0)
        {
            return request.ProjectDiscovery.DiscoveredProjects;
        }

        Dictionary<string, ArchitectureDiscoveredProject> byPath =
            request.ProjectDiscovery.DiscoveredProjects.ToDictionary(p => p.Path, StringComparer.Ordinal);

        Dictionary<string, ArchitectureDiscoveredProject> selected = new(StringComparer.Ordinal);
        Queue<ArchitectureDiscoveredProject> pending = new(seeds);

        while (pending.Count > 0)
        {
            ArchitectureDiscoveredProject project = pending.Dequeue();
            if (!selected.TryAdd(project.Path, project))
            {
                continue;
            }

            foreach (ArchitectureDiscoveredProjectReference reference in project.ProjectReferences)
            {
                if (byPath.TryGetValue(reference.Path, out ArchitectureDiscoveredProject? referenced)
                    && !selected.ContainsKey(referenced.Path))
                {
                    pending.Enqueue(referenced);
                }
            }
        }

        return selected.Values;
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

            // Assembly.LoadFrom (not Assembly.Load(byte[])) so the resulting Assembly.Location is
            // populated — BuildStatePreflightEvaluator.CheckArtifactPresence reads it to locate
            // the artifact/receipt. The trade-off is a known one: on Windows, LoadFrom keeps an
            // exclusive handle/mapping on the file for the life of this process, so a *second*
            // --ensure-built rebuild targeting the same output path within the same process can
            // fail to overwrite it (MSB3026). See BuildStatePreflightAssemblyReloadTests and the
            // Windows-excluded regression tests below for this documented limitation.
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

    private static void AddFrameworkArgument(List<string> arguments, string? requestedTargetFramework)
    {
        if (requestedTargetFramework != null)
        {
            arguments.Add("-f");
            arguments.Add(requestedTargetFramework);
        }
    }

    private static BuildStatePreflightDiagnostic? InvokeGraphBuild(BuildStatePreflightRequest request)
    {
        string solutionPath = WriteTemporaryGraphSolution(request);
        try
        {
            // Restore the solution once, up front, before building with --no-restore. Letting
            // `dotnet build` restore inline lets MSBuild's parallel build nodes each restore the
            // same referenced project concurrently (once as its own solution entry, once implicitly
            // via a ProjectReference from another entry), racing to write the same
            // obj/*.nuget.g.props file and failing with "file already exists".
            if (!request.NoRestore)
            {
                // -m:1 disables MSBuild's parallel build nodes for this restore. Parallel nodes
                // each restoring a different solution-listed project can still race on a project
                // that's *also* reached transitively via another entry's ProjectReference (e.g. a
                // referenced library listed both as its own solution entry and pulled in by the
                // app that references it), writing the same obj/*.nuget.g.props file concurrently.
                List<string> restoreArguments = new() { "restore", solutionPath, "--nologo", "-m:1" };
                AddFrameworkArgument(restoreArguments, request.RequestedTargetFramework);
                BuildStatePreflightDiagnostic? restoreFailure =
                    RunDotnetCommand(request, solutionPath, restoreArguments, "restore");
                if (restoreFailure != null)
                {
                    return restoreFailure;
                }
            }

            List<string> arguments = new() { "build", solutionPath, "--nologo", "--no-restore" };
            if (request.RequestedConfiguration != null)
            {
                arguments.Add("-c");
                arguments.Add(request.RequestedConfiguration);
            }

            // Without -f, a multi-targeted project builds every TFM it declares — an unrelated
            // TFM in that list (e.g. a Windows-only target on a non-Windows CI runner) can fail
            // the whole graph build even though the caller only asked to validate one specific
            // framework.
            AddFrameworkArgument(arguments, request.RequestedTargetFramework);

            return RunDotnetCommand(request, solutionPath, arguments, "build");
        }
        finally
        {
            File.Delete(solutionPath);
        }
    }

    private static BuildStatePreflightDiagnostic? RunDotnetCommand(
        BuildStatePreflightRequest request, string solutionPath, List<string> arguments, string commandLabel)
    {
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
        // after Start() — `dotnet build`/`dotnet restore` can write enough to either pipe to fill
        // its OS buffer, and reading only one stream (or reading one fully before starting the
        // other) blocks the child process on the unread pipe while this process blocks on
        // ReadToEnd/WaitForExit, deadlocking both.
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
            request.RepositoryRoot,
            BuildStatePreflightState.BuildFailed,
            new BuildStatePreflightEvidence(
                request.RepositoryRoot,
                string.Join(", ", request.ProjectDiscovery.DiscoveredProjects.Select(p => p.AssemblyName)),
                BuildCommand: $"dotnet {commandLabel} \"{solutionPath}\"" +
                    (request.RequestedConfiguration != null ? $" -c {request.RequestedConfiguration}" : string.Empty),
                Detail: $"`dotnet {commandLabel}` failed with exit code {process.ExitCode}: {combinedOutput}"));
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
