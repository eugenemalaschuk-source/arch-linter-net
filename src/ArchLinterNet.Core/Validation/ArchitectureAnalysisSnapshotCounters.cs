namespace ArchLinterNet.Core.Validation;

// Minimal typed counters for #363: composition/evaluation counts only. Full profiling/timing
// counters (durations, per-checker breakdowns) are deferred to #374 per
// docs/internal/analysis-build-state-blueprint.md's downstream implementation map, which
// explicitly requires timings/counters to never affect session/snapshot identity.
public sealed record ArchitectureAnalysisSnapshotCounters
{
    public int PolicyCompositions { get; init; }

    public int ProjectGraphEvaluations { get; init; }

    public int AssemblyLoads { get; init; }

    public int ModesEvaluated { get; init; }

    // One logical snapshot object is materialized for every successful CreateSnapshot call.
    // This is distinct from a post-ensure-built runner reload, which remains internal setup work.
    public int SnapshotMaterializations { get; init; }

    // The session's lazy source-file fact index can materialize at most once for a retained
    // snapshot; source scan counters make that invariant observable to analysis-profile/v1.
    public int FactIndexMaterializations { get; init; }

    public int SourceScanPasses { get; init; }

    public int SourceFilesScanned { get; init; }
}
