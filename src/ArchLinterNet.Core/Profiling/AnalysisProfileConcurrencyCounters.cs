namespace ArchLinterNet.Core.Profiling;

// Issue #408's bounded parallel scanning instrumentation. Status stays NotApplicable (all fields
// 0) unless at least one scanning phase in the run actually took the bounded-parallel code path —
// see openspec/specs/analysis-profile/spec.md, "Cache and concurrency fields are populated when
// their capability is active", and docs/internal/analysis-profile-dictionary.md.
public sealed record AnalysisProfileConcurrencyCounters
{
    public AnalysisProfileReservedFieldStatus Status { get; init; } = AnalysisProfileReservedFieldStatus.NotApplicable;

    // The resolved effective degree of parallelism for this run (see MaxParallelismResolver),
    // whether or not any phase actually took the parallel path.
    public int MaxParallelism { get; init; }

    public int ScheduledWorkItems { get; init; }

    public int CompletedWorkItems { get; init; }

    // The highest number of partition workers observed executing concurrently across every
    // parallel-eligible phase in this run.
    public int ObservedMaxConcurrency { get; init; }

    // Number of deterministic merge operations performed (one per parallel-eligible phase that
    // actually ran in parallel).
    public int MergeOperations { get; init; }
}
