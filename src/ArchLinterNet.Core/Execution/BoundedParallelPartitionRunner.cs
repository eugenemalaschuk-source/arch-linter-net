using System.Runtime.ExceptionServices;

namespace ArchLinterNet.Core.Execution;

// Partition-then-deterministic-merge helper shared by ArchitectureTypeIndex.LoadAllTypes and
// ArchitectureSourceFileFactIndex's reflection/source-scan passes — see
// openspec/changes/bounded-parallel-scanning/design.md, "Partition-then-deterministic-merge, not
// concurrent-append". Every caller gets the exact same output for the same input at every
// parallelism level: partitions are computed independently (in any completion order) and always
// written into their own preallocated array slot, so the merge step never depends on scheduling.
// An instance, not a static utility: callers hold one instance so tests can substitute a fake that
// avoids real thread scheduling (Parallel.For) instead of being permanently coupled to it.
internal sealed class BoundedParallelPartitionRunner
{
    // Below this many partitions, Parallel.For's own scheduling/task overhead is disproportionate
    // to the work — run the sequential loop directly instead. See
    // openspec/specs/bounded-parallel-scanning/spec.md, "Small work sets and sequential mode skip
    // parallel scheduling overhead".
    internal const int DefaultParallelEligibilityThreshold = 4;

    public TResult[] Run<TItem, TResult>(
        IReadOnlyList<TItem> items,
        int effectiveMaxParallelism,
        Func<TItem, int, TResult> computePartition,
        CancellationToken cancellationToken,
        AnalysisSessionProfilingCounters? profilingCounters = null,
        int parallelEligibilityThreshold = DefaultParallelEligibilityThreshold)
    {
        int count = items.Count;
        if (count == 0)
        {
            return Array.Empty<TResult>();
        }

        if (effectiveMaxParallelism <= 1 || count < parallelEligibilityThreshold)
        {
            TResult[] sequentialResults = new TResult[count];
            for (int i = 0; i < count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                sequentialResults[i] = computePartition(items[i], i);
            }

            return sequentialResults;
        }

        TResult[] results = new TResult[count];
        int inFlight = 0;
        profilingCounters?.RecordParallelScheduled(count);

        try
        {
            ParallelOptions options = new()
            {
                MaxDegreeOfParallelism = effectiveMaxParallelism,
                CancellationToken = cancellationToken,
            };

            Parallel.For(0, count, options, i =>
            {
                int concurrentNow = Interlocked.Increment(ref inFlight);
                profilingCounters?.RecordParallelConcurrency(concurrentNow);
                try
                {
                    results[i] = computePartition(items[i], i);
                    profilingCounters?.RecordParallelCompleted();
                }
                finally
                {
                    Interlocked.Decrement(ref inFlight);
                }
            });
        }
        catch (AggregateException aggregate)
        {
            // A genuine partition failure must never be masked as cancellation just because another
            // concurrently-running partition also observed cancellation and threw
            // OperationCanceledException — thread scheduling determines which inner exception a
            // naive "first cancellation wins" check would pick, which could silently discard a real
            // bug. Surface any non-cancellation failure first, deterministically and with its
            // original stack trace preserved; only report cancellation once every inner exception is
            // confirmed to be one, and only using the caller's own token (never a stray
            // OperationCanceledException a partition happened to throw for an unrelated reason).
            IReadOnlyCollection<Exception> inner = aggregate.Flatten().InnerExceptions;
            Exception? genuineFailure = inner.FirstOrDefault(static e => e is not OperationCanceledException);
            if (genuineFailure is not null)
            {
                ExceptionDispatchInfo.Capture(genuineFailure).Throw();
            }

            if (cancellationToken.IsCancellationRequested)
            {
                throw new OperationCanceledException(cancellationToken);
            }

            throw;
        }

        // A merge only happens once every partition has published its own slot — cancellation
        // observed above throws before this line, so no partial merge is ever exposed.
        cancellationToken.ThrowIfCancellationRequested();
        profilingCounters?.RecordParallelMerge();
        return results;
    }
}
