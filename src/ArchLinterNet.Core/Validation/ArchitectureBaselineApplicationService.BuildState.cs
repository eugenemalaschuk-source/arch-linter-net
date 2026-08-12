using ArchLinterNet.Core.BuildState;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Abstractions;
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
        Discovery.ProjectDiscoveryResult? discovery = runner.Session.Context.ProjectDiscovery;
        if (discovery == null || discovery.DiscoveredProjects.Count == 0)
        {
            return new BuildStatePreflightResult(Array.Empty<BuildStatePreflightDiagnostic>());
        }

        BuildStateResolvedAssemblies resolution = new(
            runner.Session.Context.TargetAssemblies,
            runner.Session.Context.MissingAssemblyNames)
        {
            // Isolated post-build loads are stream-backed, so discovery remains the authoritative
            // location of their selected project output paths for receipt verification.
            ResolvedAssemblyPaths = discovery.ResolvedAssemblyPaths,
        };

        if (resolution.ResolvedAssemblies.Count == 0 && resolution.MissingAssemblyNames.Count == 0)
        {
            return new BuildStatePreflightResult(Array.Empty<BuildStatePreflightDiagnostic>());
        }

        IBuildStatePreparationService preparationService = buildStatePreparationService
            ?? throw new InvalidOperationException("Build-state preparation is unavailable for baseline verification.");
        return preparationService.Prepare(new BuildStatePreflightRequest(
            runner.Session.Context.RepositoryRoot,
            discovery,
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
