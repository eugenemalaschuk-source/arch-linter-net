using ArchLinterNet.Core.BuildState;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Execution.Abstractions;
using ArchLinterNet.Core.Execution.Results;

namespace ArchLinterNet.Core.Graph;

public sealed partial class ArchitectureGraphApplicationService
{
    private ArchitectureRunnerSetup PrepareBuildStateRunner(
        ArchitectureGraphRequest request,
        ArchitectureContractDocument document,
        HashSet<string>? selectedContractIds,
        ArchitectureRunnerSetup setup)
    {
        BuildStateGraphOptions? buildState = BuildStateGraphOptions.From(request);
        if (buildState is null)
        {
            return setup;
        }

        BuildStatePreflightResult preflight = RunBuildStatePreflight(setup.Runner, buildState);
        if (preflight.Blocked)
        {
            setup.Runner.Session.Context.Dispose();
            throw new InvalidOperationException(DescribePreflightFailure(preflight));
        }

        if (buildState.PreparationMode != BuildPreparationMode.EnsureBuilt
            || setup.Runner.Session.Context.ProjectDiscovery is not { DiscoveredProjects.Count: > 0 })
        {
            return setup;
        }

        // The initial runner only identifies what must be built. The graph must execute against a
        // fresh isolated post-build runner; ordinary resolution would miss opted-in framework
        // closure dependencies even after the explicit build has succeeded.
        ArchitectureRunnerSetup postBuildSetup = runnerSetupService.BuildRunnerForPostBuild(
            document,
            request.PolicyPath,
            request.ConditionSetName,
            selectedContractIds: selectedContractIds,
            enableUnmatchedIgnoreTracking: false,
            mode: request.Mode == "all" ? null : request.Mode);
        setup.Runner.Session.Context.Dispose();

        preflight = RunBuildStatePreflight(
            postBuildSetup.Runner,
            buildState with { PreparationMode = BuildPreparationMode.Ordinary });
        if (preflight.Blocked)
        {
            postBuildSetup.Runner.Session.Context.Dispose();
            throw new InvalidOperationException(DescribePreflightFailure(preflight));
        }

        return postBuildSetup;
    }

    private BuildStatePreflightResult RunBuildStatePreflight(
        IArchitectureContractRunner runner,
        BuildStateGraphOptions options)
    {
        return BuildStatePreflightRunner.Run(
            runner.Session.Context.RepositoryRoot,
            runner.Session.Context.ProjectDiscovery,
            runner.Session.Context.TargetAssemblies,
            runner.Session.Context.MissingAssemblyNames,
            // Discovery owns the selected output path for project-backed graph inputs, including
            // the stream-backed artifacts loaded by the isolated post-build runner. Keep that
            // path in preflight evidence just as baseline verification does; otherwise an
            // otherwise valid post-build graph is misclassified as missing its root artifact.
            includeResolvedAssemblyPathsFromDiscovery: true,
            () => buildStatePreparationService
                ?? throw new InvalidOperationException("Build-state preparation is unavailable for graph analysis."),
            options.PreparationMode,
            options.NoRestore,
            options.RequestedConfiguration,
            options.RequestedTargetFramework,
            options.RequestedPlatform,
            options.RequestedRuntimeIdentifier,
            CancellationToken.None);
    }

    private static string DescribePreflightFailure(BuildStatePreflightResult preflight)
    {
        string details = string.Join(
            "; ",
            preflight.Diagnostics.Select(diagnostic =>
                $"{diagnostic.State} ({diagnostic.Evidence.ProjectPath ?? diagnostic.ContractName ?? "unknown project"})"));
        string suffix = string.IsNullOrEmpty(details) ? string.Empty : $" Diagnostics: {details}";
        return $"Graph build-state preflight is blocked; graph facts were not collected.{suffix}";
    }

    private sealed record BuildStateGraphOptions(
        BuildPreparationMode PreparationMode,
        bool NoRestore,
        string? RequestedConfiguration,
        string? RequestedTargetFramework,
        string? RequestedPlatform,
        string? RequestedRuntimeIdentifier)
    {
        public static BuildStateGraphOptions? From(ArchitectureGraphRequest request)
        {
            return request.PreparationMode == BuildPreparationMode.EnsureBuilt
                || request.NoRestore
                || request.RequestedConfiguration is not null
                || request.RequestedTargetFramework is not null
                || request.RequestedPlatform is not null
                || request.RequestedRuntimeIdentifier is not null
                ? new(
                    request.PreparationMode,
                    request.NoRestore,
                    request.RequestedConfiguration,
                    request.RequestedTargetFramework,
                    request.RequestedPlatform,
                    request.RequestedRuntimeIdentifier)
                : null;
        }
    }
}
