using ArchLinterNet.Core.BuildState;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Model;

namespace ArchLinterNet.Core.Graph;

public sealed record ArchitectureGraphRequest
{
    public required string PolicyPath { get; init; }

    public string Mode { get; init; } = "all";

    public string? ConditionSetName { get; init; }

    public IReadOnlyCollection<string>? ContractIds { get; init; }

    // Keep graph projections aligned with validate and baseline contributors when a snapshot
    // explicitly selects build-state preparation or an output context.
    public BuildPreparationMode PreparationMode { get; init; } = BuildPreparationMode.Ordinary;

    public bool NoRestore { get; init; }

    // Kept alongside the build-state flags so Graph's request shape remains visibly distinct
    // from baseline's otherwise similar output-context contract.
    public ArchitectureGraphLevel Level { get; init; } = ArchitectureGraphLevel.Namespace;

    public string? RequestedConfiguration { get; init; }

    public string? RequestedTargetFramework { get; init; }

    public string? RequestedPlatform { get; init; }

    public string? RequestedRuntimeIdentifier { get; init; }

    // The orchestrator has already completed ensure-built preparation for this output context.
    // Graph analysis must still re-verify its receipt, but then loads an isolated post-build
    // runner without invoking another graph build.
    public bool UsePreparedPostBuildState { get; init; }

    // The exact receipt-backed selection produced by an enclosing successful preparation. This
    // takes precedence over rediscovery when UsePreparedPostBuildState is set.
    public ArchitectureRunnerPreparation? PreparedPostBuildRunner { get; init; }
}
