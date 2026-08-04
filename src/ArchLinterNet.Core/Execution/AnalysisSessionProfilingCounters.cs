using System.Threading;

namespace ArchLinterNet.Core.Execution;

// Mutable instrumentation owned by one analysis session. It is deliberately separate from the
// immutable public snapshot-counter record: lazy fact-index work happens after the snapshot has
// been created, while callers must still observe its final values through Snapshot.Counters.
internal sealed class AnalysisSessionProfilingCounters
{
    private readonly object _gate = new();
    private readonly Dictionary<string, int> _contractFamilyResultCounts = new(StringComparer.Ordinal);
    private int _factIndexMaterializations;
    private int _sourceScanPasses;
    private int _sourceFilesScanned;
    private int _parallelScheduledWorkItems;
    private int _parallelCompletedWorkItems;
    private int _parallelObservedMaxConcurrency;
    private int _parallelMergeOperations;

    public int FactIndexMaterializations => Volatile.Read(ref _factIndexMaterializations);

    public int SourceScanPasses => Volatile.Read(ref _sourceScanPasses);

    public int SourceFilesScanned => Volatile.Read(ref _sourceFilesScanned);

    // Bounded parallel scanning (issue #408) instrumentation — see BoundedParallelPartitionRunner,
    // the sole writer of these fields. Zero for a run whose scanning phases all took the
    // sequential path (small work sets, or --max-parallelism 1).
    public int ParallelScheduledWorkItems => Volatile.Read(ref _parallelScheduledWorkItems);

    public int ParallelCompletedWorkItems => Volatile.Read(ref _parallelCompletedWorkItems);

    public int ParallelObservedMaxConcurrency => Volatile.Read(ref _parallelObservedMaxConcurrency);

    public int ParallelMergeOperations => Volatile.Read(ref _parallelMergeOperations);

    public IReadOnlyDictionary<string, int> GetContractFamilyResultCounts()
    {
        lock (_gate)
        {
            return new Dictionary<string, int>(_contractFamilyResultCounts, StringComparer.Ordinal);
        }
    }

    public void RecordFactIndexMaterialization() => Interlocked.Increment(ref _factIndexMaterializations);

    public void RecordSourceScanPass() => Interlocked.Increment(ref _sourceScanPasses);

    public void RecordSourceFileScanned() => Interlocked.Increment(ref _sourceFilesScanned);

    public void RecordParallelScheduled(int workItemCount) =>
        Interlocked.Add(ref _parallelScheduledWorkItems, workItemCount);

    public void RecordParallelCompleted() => Interlocked.Increment(ref _parallelCompletedWorkItems);

    public void RecordParallelMerge() => Interlocked.Increment(ref _parallelMergeOperations);

    // Lock-free running maximum: only ever raises the recorded value, never lowers it, and is
    // safe under concurrent callers each reporting their own momentary in-flight worker count.
    public void RecordParallelConcurrency(int observedConcurrent)
    {
        int current = Volatile.Read(ref _parallelObservedMaxConcurrency);
        while (observedConcurrent > current)
        {
            int previous = Interlocked.CompareExchange(ref _parallelObservedMaxConcurrency, observedConcurrent, current);
            if (previous == current)
            {
                return;
            }

            current = previous;
        }
    }

    // Contract execution can observe cancellation after one family has completed but before the
    // executor returns its aggregate result. Recording each completed contract here keeps a
    // cancelled snapshot's profile truthful rather than losing that completed work.
    public void RecordContractFamilyResults(string family, int resultCount)
    {
        lock (_gate)
        {
            _contractFamilyResultCounts.TryGetValue(family, out int current);
            _contractFamilyResultCounts[family] = current + resultCount;
        }
    }

    // The recorder is scoped to the one executor invocation currently in flight. A completed
    // invocation is folded into ArchitectureAnalysisSnapshotCounters; leaving it here would make
    // the snapshot add its totals a second time when another mode is evaluated.
    public void ResetContractFamilyResultCounts()
    {
        lock (_gate)
        {
            _contractFamilyResultCounts.Clear();
        }
    }
}
