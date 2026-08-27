using ArchLinterNet.Core.BuildState;
using ArchLinterNet.Core.Execution;

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

    // Set only when an enclosing operation already prepared the selected output. Diff still
    // fails closed on receipt verification, then uses an isolated post-build runner without
    // rebuilding the graph.
    public bool UsePreparedPostBuildState { get; init; }

    // The exact receipt-backed selection produced by an enclosing successful preparation. This
    // takes precedence over rediscovery when UsePreparedPostBuildState is set.
    public ArchitectureRunnerPreparation? PreparedPostBuildRunner { get; init; }

    public CancellationToken CancellationToken { get; init; } = default;
}
