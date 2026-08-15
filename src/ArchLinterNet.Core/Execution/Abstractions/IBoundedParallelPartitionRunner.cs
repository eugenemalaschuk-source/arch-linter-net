namespace ArchLinterNet.Core.Execution;

// Extracted so ArchitectureTypeIndex/ArchitectureSourceFileFactIndex can hold this as an
// interface-typed field and tests can substitute a deterministic fake instead of depending on real
// Parallel.For thread scheduling — see BoundedParallelPartitionRunner's own remarks.
internal interface IBoundedParallelPartitionRunner
{
    TResult[] Run<TItem, TResult>(
        IReadOnlyList<TItem> items,
        int effectiveMaxParallelism,
        Func<TItem, int, TResult> computePartition,
        CancellationToken cancellationToken,
        AnalysisSessionProfilingCounters? profilingCounters = null,
        int parallelEligibilityThreshold = BoundedParallelPartitionRunner.DefaultParallelEligibilityThreshold);
}
