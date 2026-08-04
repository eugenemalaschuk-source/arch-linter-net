using ArchLinterNet.Core.Execution;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class BoundedParallelPartitionRunnerTests
{
    private readonly BoundedParallelPartitionRunner _runner = new();

    [Test]
    public void Run_EmptyItems_ReturnsEmptyArray()
    {
        int[] result = _runner.Run(
            Array.Empty<int>(), effectiveMaxParallelism: 4, static (item, _) => item, CancellationToken.None);

        Assert.That(result, Is.Empty);
    }

    [Test]
    public void Run_MaxParallelismOne_PreservesOriginalOrder()
    {
        List<int> items = Enumerable.Range(0, 20).ToList();

        int[] result = _runner.Run(
            items, effectiveMaxParallelism: 1, static (item, _) => item * 2, CancellationToken.None);

        Assert.That(result, Is.EqualTo(items.Select(i => i * 2)));
    }

    [Test]
    public void Run_BelowThreshold_PreservesOriginalOrderWithoutParallelScheduling()
    {
        List<int> items = Enumerable.Range(0, 2).ToList();
        var counters = new AnalysisSessionProfilingCounters();

        int[] result = _runner.Run(
            items, effectiveMaxParallelism: 8, static (item, _) => item * 2, CancellationToken.None, counters);

        Assert.That(result, Is.EqualTo(items.Select(i => i * 2)));
        Assert.That(counters.ParallelScheduledWorkItems, Is.EqualTo(0));
    }

    [Test]
    public void Run_AboveThreshold_ResultOrderMatchesOriginalIndexRegardlessOfCompletionOrder()
    {
        // Item 0 sleeps longest, so if output order followed completion order instead of
        // partition index it would land last instead of first.
        List<int> items = Enumerable.Range(0, 10).ToList();

        int[] result = _runner.Run(
            items,
            effectiveMaxParallelism: 8,
            (item, index) =>
            {
                if (index == 0)
                {
                    Thread.Sleep(50);
                }

                return item;
            },
            CancellationToken.None);

        Assert.That(result, Is.EqualTo(items));
    }

    [Test]
    public void Run_AboveThreshold_RecordsScheduledCompletedAndMergeCounters()
    {
        List<int> items = Enumerable.Range(0, 10).ToList();
        var counters = new AnalysisSessionProfilingCounters();

        _runner.Run(
            items, effectiveMaxParallelism: 4, static (item, _) => item, CancellationToken.None, counters);

        Assert.Multiple(() =>
        {
            Assert.That(counters.ParallelScheduledWorkItems, Is.EqualTo(10));
            Assert.That(counters.ParallelCompletedWorkItems, Is.EqualTo(10));
            Assert.That(counters.ParallelMergeOperations, Is.EqualTo(1));
            Assert.That(counters.ParallelObservedMaxConcurrency, Is.GreaterThanOrEqualTo(1));
        });
    }

    [Test]
    public void Run_PreCancelledTokenAboveThreshold_ThrowsOperationCanceledExceptionDirectly()
    {
        List<int> items = Enumerable.Range(0, 10).ToList();
        using CancellationTokenSource cts = new();
        cts.Cancel();

        Exception? thrown = null;
        try
        {
            _runner.Run(items, effectiveMaxParallelism: 4, static (item, _) => item, cts.Token);
        }
        catch (Exception ex)
        {
            thrown = ex;
        }

        Assert.That(thrown, Is.InstanceOf<OperationCanceledException>());
        Assert.That(thrown, Is.Not.InstanceOf<AggregateException>());
    }

    [Test]
    public void Run_PreCancelledTokenBelowThreshold_ThrowsOperationCanceledException()
    {
        List<int> items = Enumerable.Range(0, 2).ToList();
        using CancellationTokenSource cts = new();
        cts.Cancel();

        Assert.Throws<OperationCanceledException>(() =>
            _runner.Run(items, effectiveMaxParallelism: 8, static (item, _) => item, cts.Token));
    }

    // A real bug in one partition must never be discarded in favor of a coincidental cancellation
    // exception thrown by another partition — Parallel.For aggregates every iteration's exception,
    // and which one a naive "first cancellation wins" check picks depends on thread scheduling.
    [Test]
    public void Run_GenuineFailureAlongsideCancellationExceptions_SurfacesGenuineFailureWithOriginalInstance()
    {
        List<int> items = Enumerable.Range(0, 10).ToList();
        using CancellationTokenSource cts = new();
        InvalidOperationException? original = null;

        Exception? thrown = null;
        try
        {
            _runner.Run<int, int>(
                items,
                effectiveMaxParallelism: 8,
                (item, index) =>
                {
                    if (index == 0)
                    {
                        original = new InvalidOperationException("boom");
                        throw original;
                    }

                    // Simulates another partition observing cancellation for its own reason,
                    // without the caller's own token ever actually being cancelled.
                    throw new OperationCanceledException(cts.Token);
                },
                cts.Token);
        }
        catch (Exception ex)
        {
            thrown = ex;
        }

        Assert.That(thrown, Is.SameAs(original));
    }

    [Test]
    public void Run_OnlyGenuineFailure_IsNotWrappedInAggregateException()
    {
        List<int> items = Enumerable.Range(0, 10).ToList();

        Exception? thrown = null;
        try
        {
            _runner.Run(
                items,
                effectiveMaxParallelism: 8,
                (item, index) =>
                {
                    if (index == 5)
                    {
                        throw new InvalidOperationException("boom");
                    }

                    return item;
                },
                CancellationToken.None);
        }
        catch (Exception ex)
        {
            thrown = ex;
        }

        Assert.That(thrown, Is.InstanceOf<InvalidOperationException>());
        Assert.That(thrown, Is.Not.InstanceOf<AggregateException>());
    }

    // With more than one genuine partition failure, selection must depend only on partition
    // index, never on which thread happened to finish (or throw) first. Index 7 throws
    // immediately; index 2 throws only after a deliberate delay — a scheduling-order-dependent
    // selection (e.g. "first exception seen in AggregateException.InnerExceptions") would likely
    // surface index 7's failure instead, since it completes first.
    [Test]
    public void Run_MultipleGenuineFailures_DeterministicallySurfacesLowestPartitionIndex()
    {
        List<int> items = Enumerable.Range(0, 10).ToList();

        Exception? thrown = null;
        try
        {
            _runner.Run(
                items,
                effectiveMaxParallelism: 8,
                (item, index) =>
                {
                    if (index == 7)
                    {
                        throw new InvalidOperationException("from-7");
                    }

                    if (index == 2)
                    {
                        Thread.Sleep(75);
                        throw new InvalidOperationException("from-2");
                    }

                    return item;
                },
                CancellationToken.None);
        }
        catch (Exception ex)
        {
            thrown = ex;
        }

        Assert.That(thrown, Is.InstanceOf<InvalidOperationException>());
        Assert.That(((InvalidOperationException)thrown!).Message, Is.EqualTo("from-2"));
    }

    // Mirrors the exact shape ArchitectureTypeIndex's and ArchitectureSourceFileFactIndex's
    // per-partition delegates use: check cancellation first, only then perform the "long call"
    // that check exists to guard. This proves a partition whose delegate body has already started
    // running (Parallel.For already let it through — not merely never scheduled against an
    // already-cancelled token) still stops at its own check before entering that long call, using
    // deterministic signaling rather than a pre-cancelled token or sleep-based timing.
    [Test]
    public void Run_CancellationWhileAnotherPartitionIsStillRunning_UnstartedLongWorkIsNeverEntered()
    {
        List<int> items = new() { 0, 1 };
        using CancellationTokenSource cts = new();
        using CountdownEvent bothStarted = new(2);
        using ManualResetEventSlim releaseSlowPartition = new(false);
        int longWorkEntries = 0;

        Task canceller = Task.Run(() =>
        {
            Assert.That(bothStarted.Wait(TimeSpan.FromSeconds(5)), Is.True, "both partitions must start running");
            cts.Cancel();
        });

        Exception? thrown = null;
        try
        {
            _runner.Run<int, int>(
                items,
                effectiveMaxParallelism: 2,
                (item, index) =>
                {
                    bothStarted.Signal();
                    if (index == 0)
                    {
                        // Represents a partition already committed to its own long call when
                        // cancellation is requested elsewhere — allowed to finish undisturbed.
                        releaseSlowPartition.Wait(TimeSpan.FromSeconds(5));
                        return item;
                    }

                    // The delegate body has genuinely started (Parallel.For already scheduled it —
                    // this is not a pre-cancelled-token no-op), so only its own explicit check,
                    // not Parallel.For's own pre-iteration gate, can stop it here.
                    SpinWait.SpinUntil(() => cts.IsCancellationRequested, TimeSpan.FromSeconds(5));
                    cts.Token.ThrowIfCancellationRequested();
                    Interlocked.Increment(ref longWorkEntries);
                    return item;
                },
                cts.Token,
                parallelEligibilityThreshold: 1);
        }
        catch (Exception ex)
        {
            thrown = ex;
        }
        finally
        {
            releaseSlowPartition.Set();
        }

        Assert.That(canceller.Wait(TimeSpan.FromSeconds(5)), Is.True);
        Assert.That(thrown, Is.InstanceOf<OperationCanceledException>());
        Assert.That(longWorkEntries, Is.EqualTo(0));
    }

    // Issue #408 review: the "callers can substitute a fake" claim on
    // BoundedParallelPartitionRunner's own remarks must be genuinely true, not aspirational —
    // verified here by using a fake implementation of the same interface
    // ArchitectureTypeIndex/ArchitectureSourceFileFactIndex hold.
    [Test]
    public void IBoundedParallelPartitionRunner_FakeImplementation_IsGenuinelySubstitutable()
    {
        IBoundedParallelPartitionRunner fake = new SequentialFakePartitionRunner();
        List<int> items = Enumerable.Range(0, 10).ToList();

        int[] result = fake.Run(items, effectiveMaxParallelism: 4, static (item, _) => item * 2, CancellationToken.None);

        Assert.That(result, Is.EqualTo(items.Select(i => i * 2)));
    }

    private sealed class SequentialFakePartitionRunner : IBoundedParallelPartitionRunner
    {
        public TResult[] Run<TItem, TResult>(
            IReadOnlyList<TItem> items,
            int effectiveMaxParallelism,
            Func<TItem, int, TResult> computePartition,
            CancellationToken cancellationToken,
            AnalysisSessionProfilingCounters? profilingCounters = null,
            int parallelEligibilityThreshold = BoundedParallelPartitionRunner.DefaultParallelEligibilityThreshold)
        {
            TResult[] results = new TResult[items.Count];
            for (int i = 0; i < items.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                results[i] = computePartition(items[i], i);
            }

            return results;
        }
    }
}
