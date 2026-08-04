using System.Reflection;
using ArchLinterNet.Core.Execution;
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
}
