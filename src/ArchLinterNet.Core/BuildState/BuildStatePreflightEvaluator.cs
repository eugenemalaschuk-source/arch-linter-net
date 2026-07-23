using System.Reflection;
using ArchLinterNet.Core.Discovery;
using ArchLinterNet.Core.Model;

namespace ArchLinterNet.Core.BuildState;

// Evaluates the complete selected project graph and emits exactly one primary
// BuildStatePreflightDiagnostic per discovered project, following the fixed precedence order in
// BuildStatePreflightState. See "Preflight state machine" in
// docs/internal/analysis-build-state-blueprint.md.
public static class BuildStatePreflightEvaluator
{
    private const string ContractName = "build-state-preflight";

    public static BuildStatePreflightResult Evaluate(BuildStatePreflightRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var discoveredProjects = request.ProjectDiscovery.DiscoveredProjects;
        if (discoveredProjects.Count == 0)
        {
            return new BuildStatePreflightResult(Array.Empty<BuildStatePreflightDiagnostic>());
        }

        Dictionary<string, Assembly> resolvedByName = request.Resolution.ResolvedAssemblies
            .Where(a => a.GetName().Name != null)
            .GroupBy(a => a.GetName().Name!, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

        HashSet<string> missing = new(request.Resolution.MissingAssemblyNames, StringComparer.Ordinal);

        Dictionary<string, BuildStatePreflightDiagnostic> diagnosticsByProjectPath = new(StringComparer.Ordinal);

        foreach (ArchitectureDiscoveredProject project in discoveredProjects)
        {
            diagnosticsByProjectPath[project.Path] = EvaluateProject(project, request, resolvedByName, missing);
        }

        ElevateInconsistentDependencyArtifacts(discoveredProjects, diagnosticsByProjectPath);

        return new BuildStatePreflightResult(diagnosticsByProjectPath.Values.ToArray());
    }

    private static BuildStatePreflightDiagnostic EvaluateProject(
        ArchitectureDiscoveredProject project,
        BuildStatePreflightRequest request,
        IReadOnlyDictionary<string, Assembly> resolvedByName,
        HashSet<string> missing)
    {
        if (request.CancellationToken.IsCancellationRequested)
        {
            return Diagnostic(project, BuildStatePreflightState.Cancelled,
                Evidence(project, detail: "Preflight evaluation was cancelled."));
        }

        if (request.RequestedTargetFramework != null
            && project.TargetFrameworks.Count > 0
            && !project.TargetFrameworks.Contains(request.RequestedTargetFramework, StringComparer.OrdinalIgnoreCase))
        {
            return Diagnostic(project, BuildStatePreflightState.WrongTargetFramework,
                Evidence(project,
                    requestedTfm: request.RequestedTargetFramework,
                    observedTfm: string.Join(", ", project.TargetFrameworks)));
        }

        if (missing.Contains(project.AssemblyName) || !resolvedByName.TryGetValue(project.AssemblyName, out Assembly? assembly))
        {
            return Diagnostic(project, BuildStatePreflightState.MissingArtifact,
                Evidence(project, buildCommand: BuildCommand(project, request.RequestedConfiguration)));
        }

        string? assemblyPath = SafeAssemblyLocation(assembly);
        if (string.IsNullOrEmpty(assemblyPath) || !File.Exists(assemblyPath))
        {
            return Diagnostic(project, BuildStatePreflightState.MissingArtifact,
                Evidence(project, buildCommand: BuildCommand(project, request.RequestedConfiguration)));
        }

        if (!BuildReceiptStore.TryRead(assemblyPath, out BuildReceiptV1? receipt) || receipt is null)
        {
            return Diagnostic(project, BuildStatePreflightState.UnverifiableArtifact,
                Evidence(project, expectedOutputPath: assemblyPath,
                    detail: "No ArchLinterNet build receipt was found for this artifact. Run with --ensure-built " +
                        "to build and verify it, or build via `dotnet build` and re-run with --ensure-built."));
        }

        if (!string.Equals(receipt.AssemblyName, project.AssemblyName, StringComparison.Ordinal))
        {
            return Diagnostic(project, BuildStatePreflightState.WrongProjectOutput,
                Evidence(project, expectedOutputPath: assemblyPath,
                    detail: $"Receipt identifies assembly '{receipt.AssemblyName}', expected '{project.AssemblyName}'."));
        }

        if (request.RequestedConfiguration != null && receipt.Configuration != null
            && !string.Equals(receipt.Configuration, request.RequestedConfiguration, StringComparison.OrdinalIgnoreCase))
        {
            return Diagnostic(project, BuildStatePreflightState.WrongConfiguration,
                Evidence(project,
                    requestedConfiguration: request.RequestedConfiguration,
                    observedConfiguration: receipt.Configuration,
                    expectedOutputPath: assemblyPath));
        }

        if (request.RequestedTargetFramework != null && receipt.TargetFramework != null
            && !string.Equals(receipt.TargetFramework, request.RequestedTargetFramework, StringComparison.OrdinalIgnoreCase))
        {
            return Diagnostic(project, BuildStatePreflightState.WrongTargetFramework,
                Evidence(project,
                    requestedTfm: request.RequestedTargetFramework,
                    observedTfm: receipt.TargetFramework,
                    expectedOutputPath: assemblyPath));
        }

        string currentFingerprint = BuildStateCanonicalHasher.ComputeBuildInputFingerprint(project.Path, request.RepositoryRoot);
        if (!string.Equals(receipt.BuildInputFingerprint, currentFingerprint, StringComparison.Ordinal))
        {
            return Diagnostic(project, BuildStatePreflightState.StaleArtifact,
                Evidence(project, expectedOutputPath: assemblyPath,
                    buildCommand: BuildCommand(project, request.RequestedConfiguration),
                    detail: "Selected source, project, or import content changed since the artifact was built."));
        }

        string currentAssemblyDigest = BuildStateCanonicalHasher.ComputeContentDigest(assemblyPath);
        if (!string.Equals(receipt.AssemblyContentDigest, currentAssemblyDigest, StringComparison.Ordinal))
        {
            return Diagnostic(project, BuildStatePreflightState.StaleArtifact,
                Evidence(project, expectedOutputPath: assemblyPath,
                    buildCommand: BuildCommand(project, request.RequestedConfiguration),
                    detail: "The artifact on disk no longer matches the digest recorded in its build receipt."));
        }

        return Diagnostic(project, BuildStatePreflightState.Current, Evidence(project, expectedOutputPath: assemblyPath));
    }

    // A dependent project whose own artifact is otherwise current cannot be trusted if a project
    // it directly references is missing, stale, or otherwise not current — downgrade it to
    // InconsistentDependencyArtifact rather than reporting it as Current.
    private static void ElevateInconsistentDependencyArtifacts(
        IReadOnlyCollection<ArchitectureDiscoveredProject> discoveredProjects,
        Dictionary<string, BuildStatePreflightDiagnostic> diagnosticsByProjectPath)
    {
        foreach (ArchitectureDiscoveredProject project in discoveredProjects)
        {
            if (!diagnosticsByProjectPath.TryGetValue(project.Path, out BuildStatePreflightDiagnostic? own)
                || own.State != BuildStatePreflightState.Current)
            {
                continue;
            }

            bool hasBlockingDependency = project.ProjectReferences
                .Select(reference => ResolveReferencedProjectPath(project.Path, reference.Path))
                .Any(referencedPath => diagnosticsByProjectPath.TryGetValue(referencedPath, out BuildStatePreflightDiagnostic? dependency)
                    && dependency.IsBlocking);

            if (hasBlockingDependency)
            {
                diagnosticsByProjectPath[project.Path] = own with
                {
                    State = BuildStatePreflightState.InconsistentDependencyArtifact,
                    Evidence = own.Evidence with
                    {
                        Detail = "A directly referenced project does not have a current, verified build."
                    }
                };
            }
        }
    }

    private static string ResolveReferencedProjectPath(string ownerProjectPath, string referencePath)
    {
        string ownerDirectory = Path.GetDirectoryName(Path.GetFullPath(ownerProjectPath)) ?? ".";
        return Path.GetFullPath(Path.Combine(ownerDirectory, referencePath));
    }

    private static string? SafeAssemblyLocation(Assembly assembly)
    {
        try
        {
            return assembly.Location;
        }
        catch (NotSupportedException)
        {
            return null;
        }
    }

    private static string BuildCommand(ArchitectureDiscoveredProject project, string? configuration)
    {
        string configArg = string.IsNullOrWhiteSpace(configuration) ? string.Empty : $" -c {configuration}";
        return $"dotnet build \"{project.Path}\"{configArg}";
    }

    private static BuildStatePreflightEvidence Evidence(
        ArchitectureDiscoveredProject project,
        string? requestedConfiguration = null,
        string? observedConfiguration = null,
        string? requestedTfm = null,
        string? observedTfm = null,
        string? expectedOutputPath = null,
        string? buildCommand = null,
        string? detail = null)
    {
        return new BuildStatePreflightEvidence(
            project.Path,
            project.AssemblyName,
            requestedConfiguration,
            observedConfiguration,
            requestedTfm,
            observedTfm,
            expectedOutputPath,
            SearchedPaths: null,
            buildCommand,
            detail);
    }

    private static BuildStatePreflightDiagnostic Diagnostic(
        ArchitectureDiscoveredProject project, BuildStatePreflightState state, BuildStatePreflightEvidence evidence)
    {
        return new BuildStatePreflightDiagnostic(ContractName, project.Path, state, evidence);
    }
}
