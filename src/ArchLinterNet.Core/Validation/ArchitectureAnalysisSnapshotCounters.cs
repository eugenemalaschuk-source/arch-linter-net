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
}
