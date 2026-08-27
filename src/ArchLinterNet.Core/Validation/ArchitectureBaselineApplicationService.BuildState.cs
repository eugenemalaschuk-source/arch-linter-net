using ArchLinterNet.Core.BuildState;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Abstractions;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Execution.Abstractions;
using ArchLinterNet.Core.Model;

namespace ArchLinterNet.Core.Validation;

public sealed partial class ArchitectureBaselineApplicationService
{
    private BaselineCandidateCollection CollectVerifyCandidates(BaselineVerifyRequest request)
    {
        BaselineBuildStateOptions? buildState = request.PreparationMode == BuildPreparationMode.EnsureBuilt || request.NoRestore
            ? new BaselineBuildStateOptions(
                request.PreparationMode,
                request.NoRestore,
                request.RequestedConfiguration,
                request.RequestedTargetFramework,
                request.RequestedPlatform,
                request.RequestedRuntimeIdentifier)
            : null;

        return CollectCandidatesCore(
            request.PolicyPath, request.Mode, request.ConditionSetName, request.ContractIds, request.CancellationToken, buildState);
    }

    private BuildStatePreflightResult RunBuildStatePreflight(
        IArchitectureContractRunner runner,
        BaselineBuildStateOptions options,
        CancellationToken cancellationToken)
    {
        // Isolated post-build loads are stream-backed, so discovery remains the authoritative
        // location of their selected project output paths for receipt verification — unlike
        // ordinary validation, baseline verify must include ResolvedAssemblyPaths from discovery.
        return BuildStatePreflightRunner.Run(
            runner.Session.Context.RepositoryRoot,
            runner.Session.Context.ProjectDiscovery,
            runner.Session.Context.TargetAssemblies,
            runner.Session.Context.MissingAssemblyNames,
            includeResolvedAssemblyPathsFromDiscovery: true,
            () => buildStatePreparationService
                ?? throw new InvalidOperationException("Build-state preparation is unavailable for baseline verification."),
            options.PreparationMode,
            options.NoRestore,
            options.RequestedConfiguration,
            options.RequestedTargetFramework,
            options.RequestedPlatform,
            options.RequestedRuntimeIdentifier,
            cancellationToken);
    }

    private BuildStatePreflightResult RunBuildStatePreflight(
        ArchitectureRunnerPreparation preparation,
        BaselineBuildStateOptions options,
        CancellationToken cancellationToken)
    {
        // Metadata-only preparation has no Assembly instances to pass through. Discovery still
        // owns the exact project output paths, so baseline verification must retain those paths as
        // receipt evidence in both its build-capable and ordinary post-build preflight passes.
        if (preparation.ProjectDiscovery.DiscoveredProjects.Count == 0)
        {
            return new BuildStatePreflightResult(Array.Empty<BuildStatePreflightDiagnostic>());
        }

        BuildStateResolvedAssemblies resolution = new(
            Array.Empty<System.Reflection.Assembly>(), preparation.MissingAssemblyNames)
        {
            ResolvedAssemblyPaths = preparation.ProjectDiscovery.ResolvedAssemblyPaths,
        };

        if (resolution.ResolvedAssemblyPaths.Count == 0 && resolution.MissingAssemblyNames.Count == 0)
        {
            return new BuildStatePreflightResult(Array.Empty<BuildStatePreflightDiagnostic>());
        }

        IBuildStatePreparationService preparationService = buildStatePreparationService
            ?? throw new InvalidOperationException("Build-state preparation is unavailable for baseline verification.");
        return preparationService.Prepare(new BuildStatePreflightRequest(
            preparation.RepositoryRoot,
            preparation.ProjectDiscovery,
            resolution,
            options.PreparationMode,
            options.NoRestore,
            options.RequestedConfiguration,
            options.RequestedTargetFramework,
            options.RequestedPlatform,
            options.RequestedRuntimeIdentifier,
            cancellationToken));
    }

    private sealed record BaselineBuildStateOptions(
        BuildPreparationMode PreparationMode,
        bool NoRestore,
        string? RequestedConfiguration,
        string? RequestedTargetFramework,
        string? RequestedPlatform,
        string? RequestedRuntimeIdentifier);

    private sealed record BaselineCandidateCollection(
        ArchitectureContractDocument Document,
        IReadOnlyList<ArchitectureBaselineCandidate>? Candidates,
        List<ArchitectureViolation> ConfigurationViolations,
        IReadOnlyCollection<BuildStatePreflightDiagnostic> PreflightDiagnostics)
    {
        public static BaselineCandidateCollection PreflightBlocked(
            ArchitectureContractDocument document,
            IReadOnlyCollection<BuildStatePreflightDiagnostic> diagnostics) =>
            new(document, null, new List<ArchitectureViolation>(), diagnostics);
    }
}
