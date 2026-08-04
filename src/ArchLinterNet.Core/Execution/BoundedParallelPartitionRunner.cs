using System.Runtime.ExceptionServices;

namespace ArchLinterNet.Core.Execution;

// Partition-then-deterministic-merge helper shared by ArchitectureTypeIndex.LoadAllTypes and
// ArchitectureSourceFileFactIndex's reflection/source-scan passes — see
// openspec/changes/archive/2026-08-04-bounded-parallel-scanning/design.md,
// "Partition-then-deterministic-merge, not concurrent-append". Every caller gets the exact same
// output for the same input at every parallelism level: partitions are computed independently (in
// any completion order) and always written into their own preallocated array slot, so the merge
// step never depends on scheduling. An instance, not a static utility, behind
// IBoundedParallelPartitionRunner: callers hold one instance (typed as the interface) so tests can
// substitute a fake that avoids real thread scheduling (Parallel.For) instead of being permanently
// coupled to it.
internal sealed class BoundedParallelPartitionRunner : IBoundedParallelPartitionRunner
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
        // Every partition's own exception (including one it observed via its own cancellation
        // check) is captured here at its own index, never allowed to escape the delegate. This is
        // what makes failure reporting deterministic: which exception gets surfaced below depends
        // only on partition index, never on which partition happened to finish (or throw) first —
        // Parallel.For's own AggregateException.InnerExceptions ordering is scheduling-dependent
        // and is deliberately never consulted for that decision.
        Exception?[] failures = new Exception?[count];
        int inFlight = 0;
        profilingCounters?.RecordParallelScheduled(count);

        ParallelOptions options = new()
        {
            MaxDegreeOfParallelism = effectiveMaxParallelism,
            CancellationToken = cancellationToken,
        };

        try
        {
            Parallel.For(0, count, options, i =>
            {
                int concurrentNow = Interlocked.Increment(ref inFlight);
                profilingCounters?.RecordParallelConcurrency(concurrentNow);
                try
                {
                    results[i] = computePartition(items[i], i);
                    profilingCounters?.RecordParallelCompleted();
                }
                catch (Exception ex)
                {
                    failures[i] = ex;
                }
                finally
                {
                    Interlocked.Decrement(ref inFlight);
                }
            });
        }
        catch (OperationCanceledException)
        {
            // Parallel.For's own scheduling loop can throw this directly (unwrapped) when it
            // observes cancellation between iterations, before ever invoking every delegate — the
            // deterministic scan below still runs and reports the correct outcome either way.
        }
        catch (AggregateException aggregate)
        {
            // Every exception a partition delegate can throw is already caught and recorded into
            // `failures` above, so Parallel.For itself throwing here should be unreachable in
            // practice — but silently swallowing it here would risk falling through to the merge
            // below and returning partial/default results as if the run had succeeded. Surface any
            // genuine (non-cancellation) failure the same deterministic way as a partition-captured
            // one; only let this be a no-op when every inner exception really is cancellation tied
            // to the caller's own token, in which case the check below reports it correctly.
            IReadOnlyCollection<Exception> outerInner = aggregate.Flatten().InnerExceptions;
            Exception? outerGenuineFailure = outerInner.FirstOrDefault(static e => e is not OperationCanceledException);
            if (outerGenuineFailure is not null)
            {
                ExceptionDispatchInfo.Capture(outerGenuineFailure).Throw();
            }

            if (!cancellationToken.IsCancellationRequested)
            {
                throw;
            }
        }

        // Deterministic: the lowest-index partition with a genuine (non-cancellation) failure
        // always wins, regardless of which partition's thread happened to finish or throw first.
        for (int i = 0; i < count; i++)
        {
            if (failures[i] is { } failure && failure is not OperationCanceledException)
            {
                ExceptionDispatchInfo.Capture(failure).Throw();
            }
        }

        if (cancellationToken.IsCancellationRequested || Array.Exists(failures, static f => f is OperationCanceledException))
        {
            throw new OperationCanceledException(cancellationToken);
        }

        // A merge only happens once every partition has published its own slot — a genuine failure
        // or cancellation observed above throws before this line, so no partial merge is ever
        // exposed.
        profilingCounters?.RecordParallelMerge();
        return results;
    }
}
