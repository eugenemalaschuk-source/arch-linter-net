using ArchLinterNet.Core.BuildState;
using ArchLinterNet.Core.Model;

namespace ArchLinterNet.Core.Graph;

public sealed record ArchitectureGraphRequest
{
    public required string PolicyPath { get; init; }

    public string Mode { get; init; } = "all";

    public ArchitectureGraphLevel Level { get; init; } = ArchitectureGraphLevel.Namespace;

    public string? ConditionSetName { get; init; }

    public IReadOnlyCollection<string>? ContractIds { get; init; }

    // Keep graph projections aligned with validate and baseline contributors when a snapshot
    // explicitly selects build-state preparation or an output context.
    public BuildPreparationMode PreparationMode { get; init; } = BuildPreparationMode.Ordinary;

    public bool NoRestore { get; init; }

    public string? RequestedConfiguration { get; init; }

    public string? RequestedTargetFramework { get; init; }

    public string? RequestedPlatform { get; init; }

    public string? RequestedRuntimeIdentifier { get; init; }
}
