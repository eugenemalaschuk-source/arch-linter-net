using System.Diagnostics;
using System.Reflection;
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
            string? projectDirectory = Path.GetDirectoryName(Path.GetFullPath(project.Path));
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
        foreach (ArchitectureDiscoveredProject project in request.ProjectDiscovery.DiscoveredProjects)
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

    private static BuildStateResolvedAssemblies ResolveBuiltAssemblies(BuildStatePreflightRequest request)
    {
        List<Assembly> resolved = new();
        List<string> missing = new();

        foreach (ArchitectureDiscoveredProject project in request.ProjectDiscovery.DiscoveredProjects)
        {
            string? projectDirectory = Path.GetDirectoryName(Path.GetFullPath(project.Path));
            string? assemblyPath = projectDirectory == null
                ? null
                : FindBuiltAssembly(projectDirectory, project.AssemblyName, request.RequestedConfiguration);

            if (assemblyPath == null)
            {
                missing.Add(project.AssemblyName);
                continue;
            }

            resolved.Add(Assembly.LoadFrom(assemblyPath));
        }

        return new BuildStateResolvedAssemblies(resolved, missing);
    }

    private static string? FindBuiltAssembly(string projectDirectory, string assemblyName, string? configuration)
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

        if (configuration != null)
        {
            string? configurationMatch = candidates.FirstOrDefault(path =>
                path.Contains(Path.DirectorySeparatorChar + configuration + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase));
            if (configurationMatch != null)
            {
                return configurationMatch;
            }
        }

        return candidates
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .First();
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

        ProcessStartInfo startInfo = new("dotnet")
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
        process.Start();
        string stdErr = process.StandardError.ReadToEnd();
        process.WaitForExit();

        if (process.ExitCode == 0)
        {
            return null;
        }

        return new BuildStatePreflightDiagnostic(
            ContractName,
            project.Path,
            BuildStatePreflightState.MissingArtifact,
            new BuildStatePreflightEvidence(
                project.Path,
                project.AssemblyName,
                BuildCommand: string.Join(' ', arguments.Prepend("dotnet")),
                Detail: $"`dotnet build` failed with exit code {process.ExitCode}: {stdErr.Trim()}"));
    }

    private static void WriteReceiptsForCurrentArtifacts(
        BuildStatePreflightRequest request, BuildStatePreflightResult evaluation)
    {
        Dictionary<string, ArchitectureDiscoveredProject> projectsByPath =
            request.ProjectDiscovery.DiscoveredProjects.ToDictionary(p => p.Path, StringComparer.Ordinal);

        foreach (BuildStatePreflightDiagnostic diagnostic in evaluation.Diagnostics)
        {
            if (diagnostic.State != BuildStatePreflightState.UnverifiableArtifact
                || diagnostic.Evidence.ExpectedOutputPath == null
                || !projectsByPath.TryGetValue(diagnostic.Evidence.ProjectPath, out ArchitectureDiscoveredProject? project))
            {
                continue;
            }

            string assemblyPath = diagnostic.Evidence.ExpectedOutputPath;
            string fingerprint = BuildStateCanonicalHasher.ComputeBuildInputFingerprint(project.Path, request.RepositoryRoot);
            string assemblyDigest = BuildStateCanonicalHasher.ComputeContentDigest(assemblyPath);

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
