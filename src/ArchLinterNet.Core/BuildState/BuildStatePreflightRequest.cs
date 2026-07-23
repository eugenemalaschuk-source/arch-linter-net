using ArchLinterNet.Core.Discovery;

namespace ArchLinterNet.Core.BuildState;

public sealed record BuildStatePreflightRequest(
    string RepositoryRoot,
    ProjectDiscoveryResult ProjectDiscovery,
    BuildStateResolvedAssemblies Resolution,
    BuildPreparationMode PreparationMode,
    bool NoRestore = false,
    string? RequestedConfiguration = null,
    string? RequestedTargetFramework = null,
    CancellationToken CancellationToken = default);
