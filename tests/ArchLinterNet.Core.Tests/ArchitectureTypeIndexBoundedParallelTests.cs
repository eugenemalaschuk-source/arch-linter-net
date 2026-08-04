using System.Reflection;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Scanning;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

// Issue #408: bounded parallel type loading must produce byte-identical output to the sequential
// path at every parallelism level, publish nothing on cancellation, and never leak
// AggregateException past LoadAllTypes(). See
// openspec/specs/bounded-parallel-scanning/spec.md.
[TestFixture]
public sealed class ArchitectureTypeIndexBoundedParallelTests
{
    // Enough distinct, already-loaded assemblies to exceed BoundedParallelPartitionRunner's
    // small-work-set threshold and genuinely exercise the parallel code path.
    private static readonly Assembly[] _manyAssemblies = AppDomain.CurrentDomain.GetAssemblies()
        .Where(static a => !a.IsDynamic)
        .Distinct()
        .Take(8)
        .ToArray();

    [Test]
    public void AllTypes_SequentialAndParallel_ProduceIdenticalOrderAndContent()
    {
        Assume.That(_manyAssemblies.Length, Is.GreaterThanOrEqualTo(4));

        var sequential = new ArchitectureTypeIndex(
            _manyAssemblies, maxParallelism: 1, profilingCounters: null);
        var parallel = new ArchitectureTypeIndex(
            _manyAssemblies, maxParallelism: 4, profilingCounters: null);

        Type[] sequentialTypes = sequential.AllTypes();
        Type[] parallelTypes = parallel.AllTypes();

        Assert.That(parallelTypes, Is.EqualTo(sequentialTypes));
    }

    [Test]
    public void AllTypes_RepeatedParallelRuns_AreByteStable()
    {
        Assume.That(_manyAssemblies.Length, Is.GreaterThanOrEqualTo(4));

        var first = new ArchitectureTypeIndex(_manyAssemblies, maxParallelism: 4, profilingCounters: null);
        var second = new ArchitectureTypeIndex(_manyAssemblies, maxParallelism: 4, profilingCounters: null);

        Assert.That(second.AllTypes(), Is.EqualTo(first.AllTypes()));
    }

    [Test]
    public void AllTypes_ParallelPath_RecordsConcurrencyInstrumentation()
    {
        Assume.That(_manyAssemblies.Length, Is.GreaterThanOrEqualTo(4));

        var counters = new AnalysisSessionProfilingCounters();
        var index = new ArchitectureTypeIndex(_manyAssemblies, maxParallelism: 4, counters);

        index.AllTypes();

        Assert.Multiple(() =>
        {
            Assert.That(counters.ParallelScheduledWorkItems, Is.EqualTo(_manyAssemblies.Length));
            Assert.That(counters.ParallelCompletedWorkItems, Is.EqualTo(_manyAssemblies.Length));
            Assert.That(counters.ParallelMergeOperations, Is.EqualTo(1));
        });
    }

    [Test]
    public void AllTypes_SmallAssemblySet_DoesNotRecordParallelInstrumentation()
    {
        var singleAssembly = new[] { typeof(ArchitectureTypeIndexBoundedParallelTests).Assembly };
        var counters = new AnalysisSessionProfilingCounters();
        var index = new ArchitectureTypeIndex(singleAssembly, maxParallelism: 4, counters);

        index.AllTypes();

        Assert.That(counters.ParallelScheduledWorkItems, Is.EqualTo(0));
    }

    [Test]
    public void AllTypes_MaxParallelismOne_NeverRecordsParallelInstrumentation()
    {
        Assume.That(_manyAssemblies.Length, Is.GreaterThanOrEqualTo(4));

        var counters = new AnalysisSessionProfilingCounters();
        var index = new ArchitectureTypeIndex(_manyAssemblies, maxParallelism: 1, counters);

        index.AllTypes();

        Assert.That(counters.ParallelScheduledWorkItems, Is.EqualTo(0));
    }

    [Test]
    public void AllTypes_ParallelPath_PreCancelledToken_ThrowsOperationCanceledExceptionDirectly()
    {
        Assume.That(_manyAssemblies.Length, Is.GreaterThanOrEqualTo(4));

        using CancellationTokenSource cts = new();
        cts.Cancel();
        var index = new ArchitectureTypeIndex(_manyAssemblies, maxParallelism: 4, profilingCounters: null, cts.Token);

        Exception? thrown = null;
        try
        {
            index.AllTypes();
        }
        catch (Exception ex)
        {
            thrown = ex;
        }

        Assert.That(thrown, Is.InstanceOf<OperationCanceledException>());
        Assert.That(thrown, Is.Not.InstanceOf<AggregateException>());
    }

    // Issue #408 review: a pre-cancelled token never actually reaches the per-partition delegate's
    // own check, since BoundedParallelPartitionRunner's real Run() (sequential or parallel path)
    // already refuses to schedule any iteration against an already-cancelled token — the test
    // above proves overall behavior but not this specific boundary. This test isolates the real
    // per-partition delegate LoadAllTypes() builds (the actual production lambda, not a hand-rolled
    // stand-in) using a fake IBoundedParallelPartitionRunner that performs NO cancellation check of
    // its own before invoking it, so the delegate's own
    // `_cancellationToken.ThrowIfCancellationRequested()` is the only thing that can stop it — and
    // an injectable type-loading provider proves it was never invoked, not merely that the overall
    // call eventually threw (GetLoadableTypes' own later per-type check would also eventually throw
    // for a non-empty assembly, which would let a removed boundary check go undetected by a
    // black-box "did it throw" assertion alone).
    [Test]
    public void AllTypes_RealPerPartitionDelegate_ChecksCancellationBeforeInvokingTypeProvider()
    {
        using CancellationTokenSource cts = new();
        cts.Cancel();
        bool providerInvoked = false;

        var index = new ArchitectureTypeIndex(
            _manyAssemblies,
            maxParallelism: 4,
            profilingCounters: null,
            cts.Token,
            partitionRunner: new NoCancellationCheckPartitionRunner(),
            loadableTypesProvider: (assembly, token) =>
            {
                providerInvoked = true;
                return ArchitectureTypeScanner.GetLoadableTypes(assembly, token);
            });

        Assert.Throws<OperationCanceledException>(() => index.AllTypes());
        Assert.That(providerInvoked, Is.False);
    }

    private sealed class NoCancellationCheckPartitionRunner : IBoundedParallelPartitionRunner
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
                // Deliberately no cancellation check here — the point of this fake is to isolate
                // whether the caller-supplied delegate observes cancellation on its own, rather
                // than inheriting that guarantee from the runner's own pre-iteration check.
                results[i] = computePartition(items[i], i);
            }

            return results;
        }
    }
}
