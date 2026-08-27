using ArchLinterNet.Core.BuildState;

namespace ArchLinterNet.Core.Validation;

public sealed record BaselineDiffRequest
{
    public required string PolicyPath { get; init; }

    public required string BaselinePath { get; init; }

    public required string Mode { get; init; }

    public string? ConditionSetName { get; init; }

    public IReadOnlyCollection<string>? ContractIds { get; init; }

    // Baseline debt is a live analysis contributor to change snapshots. Keep its explicit
    // build-state contract aligned with validate and baseline verify.
    public BuildPreparationMode PreparationMode { get; init; } = BuildPreparationMode.Ordinary;

    public bool NoRestore { get; init; }

    public string? RequestedConfiguration { get; init; }

    public string? RequestedTargetFramework { get; init; }

    public string? RequestedPlatform { get; init; }

    public string? RequestedRuntimeIdentifier { get; init; }

    public CancellationToken CancellationToken { get; init; } = default;
}
