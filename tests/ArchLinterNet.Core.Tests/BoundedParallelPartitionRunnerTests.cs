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
}
