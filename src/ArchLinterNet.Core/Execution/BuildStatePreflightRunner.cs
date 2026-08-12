using ArchLinterNet.Core.BuildState;
using ArchLinterNet.Core.Discovery;
using ArchLinterNet.Core.Model;

namespace ArchLinterNet.Core.Execution;

// Shared by ArchitectureValidationApplicationService and ArchitectureBaselineApplicationService
// (Core.Validation): both preflight a runner's already-resolved assembly closure against build
// state before contract execution proceeds, short-circuiting when there is no project graph or
// resolution was never attempted. Lives here rather than in Core.Validation because reaching
// Core.Discovery types is only permitted through Core.Execution (core-validation-must-not-bypass-
// application-internals).
internal static class BuildStatePreflightRunner
{
    public static BuildStatePreflightResult Run(
        string repositoryRoot,
        ProjectDiscoveryResult? discovery,
        IReadOnlyCollection<System.Reflection.Assembly> targetAssemblies,
        IReadOnlyCollection<string> missingAssemblyNames,
        bool includeResolvedAssemblyPathsFromDiscovery,
        Func<IBuildStatePreparationService> requirePreparationService,
        BuildPreparationMode preparationMode,
        bool noRestore,
        string? requestedConfiguration,
        string? requestedTargetFramework,
        string? requestedPlatform,
        string? requestedRuntimeIdentifier,
        CancellationToken cancellationToken)
    {
        if (discovery == null || discovery.DiscoveredProjects.Count == 0)
        {
            return new BuildStatePreflightResult(Array.Empty<BuildStatePreflightDiagnostic>());
        }

        BuildStateResolvedAssemblies resolution = includeResolvedAssemblyPathsFromDiscovery
            ? new(targetAssemblies, missingAssemblyNames) { ResolvedAssemblyPaths = discovery.ResolvedAssemblyPaths }
            : new(targetAssemblies, missingAssemblyNames);

        if (resolution.ResolvedAssemblies.Count == 0 && resolution.MissingAssemblyNames.Count == 0)
        {
            return new BuildStatePreflightResult(Array.Empty<BuildStatePreflightDiagnostic>());
        }

        return requirePreparationService().Prepare(new BuildStatePreflightRequest(
            repositoryRoot,
            discovery,
            resolution,
            preparationMode,
            noRestore,
            requestedConfiguration,
            requestedTargetFramework,
            requestedPlatform,
            requestedRuntimeIdentifier,
            cancellationToken));
    }
}
