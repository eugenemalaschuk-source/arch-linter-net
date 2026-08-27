using ArchLinterNet.Core.BuildState;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Abstractions;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Execution.Abstractions;
using ArchLinterNet.Core.Model;

namespace ArchLinterNet.Core.Validation;

public sealed partial class ArchitectureBaselineApplicationService
{
    private BaselineCandidateCollection CollectDiffCandidates(BaselineDiffRequest request)
    {
        BaselineBuildStateOptions? buildState = BaselineBuildStateOptions.From(
            request.PreparationMode,
            request.NoRestore,
            request.RequestedConfiguration,
            request.RequestedTargetFramework,
            request.RequestedPlatform,
            request.RequestedRuntimeIdentifier);

        return CollectCandidatesCore(
            request.PolicyPath, request.Mode, request.ConditionSetName, request.ContractIds, request.CancellationToken, buildState);
    }

    private BaselineCandidateCollection CollectVerifyCandidates(BaselineVerifyRequest request)
    {
        BaselineBuildStateOptions? buildState = BaselineBuildStateOptions.From(
            request.PreparationMode,
            request.NoRestore,
            request.RequestedConfiguration,
            request.RequestedTargetFramework,
            request.RequestedPlatform,
            request.RequestedRuntimeIdentifier);

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

    private sealed record BaselineBuildStateOptions(
        BuildPreparationMode PreparationMode,
        bool NoRestore,
        string? RequestedConfiguration,
        string? RequestedTargetFramework,
        string? RequestedPlatform,
        string? RequestedRuntimeIdentifier)
    {
        public static BaselineBuildStateOptions? From(
            BuildPreparationMode preparationMode,
            bool noRestore,
            string? requestedConfiguration,
            string? requestedTargetFramework,
            string? requestedPlatform,
            string? requestedRuntimeIdentifier)
        {
            return preparationMode == BuildPreparationMode.EnsureBuilt
                || noRestore
                || requestedConfiguration is not null
                || requestedTargetFramework is not null
                || requestedPlatform is not null
                || requestedRuntimeIdentifier is not null
                ? new(
                    preparationMode,
                    noRestore,
                    requestedConfiguration,
                    requestedTargetFramework,
                    requestedPlatform,
                    requestedRuntimeIdentifier)
                : null;
        }
    }

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
