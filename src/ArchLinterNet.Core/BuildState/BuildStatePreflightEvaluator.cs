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

        // Only evaluate discovered projects that assembly resolution actually attempted to
        // resolve (present in either the resolved or the missing name set). A policy may declare
        // a project list solely to feed project-scope coverage contracts, independently of which
        // assemblies analysis.target_assemblies actually resolves — such projects have no
        // necessary correspondence to a resolved/missing assembly and must not be
        // preflight-blocked just for being discovered.
        foreach (ArchitectureDiscoveredProject project in discoveredProjects)
        {
            if (!IsRelevantToResolution(project, request.Resolution))
            {
                continue;
            }

            diagnosticsByProjectPath[project.Path] = EvaluateProject(project, request, resolvedByName, missing);
        }

        ElevateInconsistentDependencyArtifacts(discoveredProjects, diagnosticsByProjectPath);

        return new BuildStatePreflightResult(diagnosticsByProjectPath.Values.ToArray());
    }

    // Shared with BuildStatePreparationService.CheckRestorePrerequisites so --no-restore checks
    // exactly the same set of projects Evaluate() would otherwise preflight-block — a project
    // discovered only to feed project-scope coverage, and never attempted by assembly resolution,
    // must not be treated as requiring a restore either.
    internal static bool IsRelevantToResolution(ArchitectureDiscoveredProject project, BuildStateResolvedAssemblies resolution)
    {
        return resolution.ResolvedAssemblies.Any(a => string.Equals(a.GetName().Name, project.AssemblyName, StringComparison.Ordinal))
            || resolution.MissingAssemblyNames.Contains(project.AssemblyName, StringComparer.Ordinal);
    }

    private static BuildStatePreflightDiagnostic EvaluateProject(
        ArchitectureDiscoveredProject project,
        BuildStatePreflightRequest request,
        Dictionary<string, Assembly> resolvedByName,
        HashSet<string> missing)
    {
        return CheckCancellation(project, request)
            ?? CheckRequestedTargetFrameworkAgainstProject(project, request)
            ?? CheckArtifactPresence(project, request, resolvedByName, missing, out string? assemblyPath)
            ?? CheckReceipt(project, request, assemblyPath!);
    }

    private static BuildStatePreflightDiagnostic? CheckCancellation(
        ArchitectureDiscoveredProject project, BuildStatePreflightRequest request)
    {
        if (!request.CancellationToken.IsCancellationRequested)
        {
            return null;
        }

        return Diagnostic(project, BuildStatePreflightState.Cancelled,
            Evidence(project) with { Detail = "Preflight evaluation was cancelled." });
    }

    // Rejects a target framework the project doesn't even declare, before looking at any build
    // output — a cheap, purely-declarative check that doesn't need the resolved assembly.
    private static BuildStatePreflightDiagnostic? CheckRequestedTargetFrameworkAgainstProject(
        ArchitectureDiscoveredProject project, BuildStatePreflightRequest request)
    {
        if (request.RequestedTargetFramework == null
            || project.TargetFrameworks.Count == 0
            || project.TargetFrameworks.Contains(request.RequestedTargetFramework, StringComparer.OrdinalIgnoreCase))
        {
            return null;
        }

        return Diagnostic(project, BuildStatePreflightState.WrongTargetFramework,
            Evidence(project) with
            {
                RequestedTargetFramework = request.RequestedTargetFramework,
                ObservedTargetFramework = string.Join(", ", project.TargetFrameworks)
            });
    }

    private static BuildStatePreflightDiagnostic? CheckArtifactPresence(
        ArchitectureDiscoveredProject project,
        BuildStatePreflightRequest request,
        Dictionary<string, Assembly> resolvedByName,
        HashSet<string> missing,
        out string? assemblyPath)
    {
        assemblyPath = null;

        if (missing.Contains(project.AssemblyName) || !resolvedByName.TryGetValue(project.AssemblyName, out Assembly? assembly))
        {
            return MissingArtifactDiagnostic(project, request);
        }

        assemblyPath = SafeAssemblyLocation(assembly);
        if (string.IsNullOrEmpty(assemblyPath) || !File.Exists(assemblyPath))
        {
            assemblyPath = null;
            return MissingArtifactDiagnostic(project, request);
        }

        return null;
    }

    private static BuildStatePreflightDiagnostic MissingArtifactDiagnostic(
        ArchitectureDiscoveredProject project, BuildStatePreflightRequest request)
    {
        return Diagnostic(project, BuildStatePreflightState.MissingArtifact,
            Evidence(project) with { BuildCommand = BuildCommand(project, request.RequestedConfiguration) });
    }

    private static BuildStatePreflightDiagnostic CheckReceipt(
        ArchitectureDiscoveredProject project, BuildStatePreflightRequest request, string assemblyPath)
    {
        if (!BuildReceiptStore.TryRead(assemblyPath, out BuildReceiptV1? receipt) || receipt is null)
        {
            return Diagnostic(project, BuildStatePreflightState.UnverifiableArtifact,
                Evidence(project) with
                {
                    ExpectedOutputPath = assemblyPath,
                    Detail = "No ArchLinterNet build receipt was found for this artifact. Run with --ensure-built " +
                        "to build and verify it, or build via `dotnet build` and re-run with --ensure-built."
                });
        }

        return CheckReceiptIdentity(project, request, assemblyPath, receipt)
            ?? CheckReceiptFreshness(project, request, assemblyPath, receipt)
            ?? Diagnostic(project, BuildStatePreflightState.Current, Evidence(project) with { ExpectedOutputPath = assemblyPath });
    }

    private static BuildStatePreflightDiagnostic? CheckReceiptIdentity(
        ArchitectureDiscoveredProject project, BuildStatePreflightRequest request, string assemblyPath, BuildReceiptV1 receipt)
    {
        if (!string.Equals(receipt.AssemblyName, project.AssemblyName, StringComparison.Ordinal))
        {
            return Diagnostic(project, BuildStatePreflightState.WrongProjectOutput,
                Evidence(project) with
                {
                    ExpectedOutputPath = assemblyPath,
                    Detail = $"Receipt identifies assembly '{receipt.AssemblyName}', expected '{project.AssemblyName}'."
                });
        }

        if (request.RequestedConfiguration != null && receipt.Configuration != null
            && !string.Equals(receipt.Configuration, request.RequestedConfiguration, StringComparison.OrdinalIgnoreCase))
        {
            return Diagnostic(project, BuildStatePreflightState.WrongConfiguration,
                Evidence(project) with
                {
                    RequestedConfiguration = request.RequestedConfiguration,
                    ObservedConfiguration = receipt.Configuration,
                    ExpectedOutputPath = assemblyPath
                });
        }

        if (request.RequestedTargetFramework != null && receipt.TargetFramework != null
            && !string.Equals(receipt.TargetFramework, request.RequestedTargetFramework, StringComparison.OrdinalIgnoreCase))
        {
            return Diagnostic(project, BuildStatePreflightState.WrongTargetFramework,
                Evidence(project) with
                {
                    RequestedTargetFramework = request.RequestedTargetFramework,
                    ObservedTargetFramework = receipt.TargetFramework,
                    ExpectedOutputPath = assemblyPath
                });
        }

        return null;
    }

    private static BuildStatePreflightDiagnostic? CheckReceiptFreshness(
        ArchitectureDiscoveredProject project, BuildStatePreflightRequest request, string assemblyPath, BuildReceiptV1 receipt)
    {
        string currentFingerprint = BuildStateCanonicalHasher.ComputeBuildInputFingerprint(project.Path, request.RepositoryRoot);
        if (!string.Equals(receipt.BuildInputFingerprint, currentFingerprint, StringComparison.Ordinal))
        {
            return Diagnostic(project, BuildStatePreflightState.StaleArtifact,
                Evidence(project) with
                {
                    ExpectedOutputPath = assemblyPath,
                    BuildCommand = BuildCommand(project, request.RequestedConfiguration),
                    Detail = "Selected source, project, or import content changed since the artifact was built."
                });
        }

        string currentAssemblyDigest = BuildStateCanonicalHasher.ComputeContentDigest(assemblyPath);
        if (!string.Equals(receipt.AssemblyContentDigest, currentAssemblyDigest, StringComparison.Ordinal))
        {
            return Diagnostic(project, BuildStatePreflightState.StaleArtifact,
                Evidence(project) with
                {
                    ExpectedOutputPath = assemblyPath,
                    BuildCommand = BuildCommand(project, request.RequestedConfiguration),
                    Detail = "The artifact on disk no longer matches the digest recorded in its build receipt."
                });
        }

        return null;
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

            // ArchitectureDiscoveredProjectReference.Path is already repository-relative — the
            // same canonical form ArchitectureProjectDiscoveryService gives every discovered
            // project's own .Path (both go through the identical GetRelativePath helper) — so it
            // is directly usable as the diagnosticsByProjectPath key with no further path
            // combination. Combining it with an owner-relative directory here previously produced
            // an absolute path that could never match a repo-relative dictionary key, silently
            // disabling this check.
            bool hasBlockingDependency = project.ProjectReferences
                .Any(reference => diagnosticsByProjectPath.TryGetValue(reference.Path, out BuildStatePreflightDiagnostic? dependency)
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

    private static BuildStatePreflightEvidence Evidence(ArchitectureDiscoveredProject project)
    {
        return new BuildStatePreflightEvidence(project.Path, project.AssemblyName);
    }

    private static BuildStatePreflightDiagnostic Diagnostic(
        ArchitectureDiscoveredProject project, BuildStatePreflightState state, BuildStatePreflightEvidence evidence)
    {
        return new BuildStatePreflightDiagnostic(ContractName, project.Path, state, evidence);
    }
}
