using System.Reflection;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Tests.SourceFactFixtures;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

// Issue #408: bounded parallel fact-index materialization must produce byte-identical output to
// the sequential path at every parallelism level, publish nothing on cancellation, and preserve
// the once-per-snapshot laziness guarantee. See openspec/specs/bounded-parallel-scanning/spec.md.
[TestFixture]
public sealed class ArchitectureSourceFileFactIndexBoundedParallelTests
{
    private static readonly Assembly _testAssembly = typeof(ArchitectureSourceFileFactIndexBoundedParallelTests).Assembly;
    private const string TestAssemblyName = "ArchLinterNet.Core.Tests";

    private static ArchitectureSourceFileFactIndex BuildMultiRootIndex(
        int maxParallelism,
        AnalysisSessionProfilingCounters? profilingCounters = null,
        int? thresholdOverride = null,
        CancellationToken cancellationToken = default)
    {
        string repoRoot = FakePaths.Root("/fake/repo");
        var fs = new FakeArchitectureFileSystem();
        string[] roots = ["src/A", "src/B", "src/C", "src/D"];
        Dictionary<string, string> ownership = new(StringComparer.Ordinal);

        foreach (string root in roots)
        {
            string absoluteRoot = repoRoot + "/" + root;
            fs.AddDirectory(absoluteRoot);
            string typeName = "Fixture" + root[^1];
            string content =
                $"namespace ArchLinterNet.Core.Tests.SourceFactFixtures {{ public sealed class {typeName} {{ }} }}";
            fs.AddFile(absoluteRoot + "/" + typeName + ".cs", content, DateTime.UtcNow);
            ownership[root] = TestAssemblyName;
        }

        return new ArchitectureSourceFileFactIndex(
            new[] { _testAssembly },
            repoRoot,
            roots,
            preprocessorSymbols: null,
            fs,
            new ArchitectureSourceFileFactIndex.ProjectOwnership(
                ProjectDiscovery: null,
                SourceRootAssemblyOwnership: ownership),
            new ArchitectureSourceFileFactIndex.ConstructionOptions(
                profilingCounters, cancellationToken, maxParallelism, thresholdOverride));
    }

    [Test]
    public void AllFacts_SequentialAndParallel_ProduceIdenticalOrderAndContent()
    {
        ArchitectureSourceFileFactIndex sequential = BuildMultiRootIndex(maxParallelism: 1);
        ArchitectureSourceFileFactIndex parallel = BuildMultiRootIndex(maxParallelism: 4);

        var sequentialFacts = sequential.AllFacts.Select(f => (f.AssemblyName, f.FullTypeName, f.SourceFilePath)).ToList();
        var parallelFacts = parallel.AllFacts.Select(f => (f.AssemblyName, f.FullTypeName, f.SourceFilePath)).ToList();

        Assert.That(parallelFacts, Is.EqualTo(sequentialFacts));
    }

    [Test]
    public void AllFacts_ForcedParallelPathOnSmallFixture_MatchesSequentialOutput()
    {
        // Force the parallel code path even on this small 4-root fixture by overriding the
        // eligibility threshold down to 2 — proves merge-order correctness independent of
        // completion order, not just on fixtures large enough to hit the real default threshold.
        ArchitectureSourceFileFactIndex sequential = BuildMultiRootIndex(maxParallelism: 1);
        ArchitectureSourceFileFactIndex forcedParallel = BuildMultiRootIndex(maxParallelism: 4, thresholdOverride: 2);

        var sequentialFacts = sequential.AllFacts.Select(f => (f.AssemblyName, f.FullTypeName)).ToList();
        var parallelFacts = forcedParallel.AllFacts.Select(f => (f.AssemblyName, f.FullTypeName)).ToList();

        Assert.That(parallelFacts, Is.EqualTo(sequentialFacts));
    }

    [Test]
    public void AllFacts_RepeatedParallelRuns_AreByteStable()
    {
        ArchitectureSourceFileFactIndex first = BuildMultiRootIndex(maxParallelism: 4, thresholdOverride: 2);
        ArchitectureSourceFileFactIndex second = BuildMultiRootIndex(maxParallelism: 4, thresholdOverride: 2);

        var firstFacts = first.AllFacts.Select(f => (f.AssemblyName, f.FullTypeName, f.SourceFilePath)).ToList();
        var secondFacts = second.AllFacts.Select(f => (f.AssemblyName, f.FullTypeName, f.SourceFilePath)).ToList();

        Assert.That(secondFacts, Is.EqualTo(firstFacts));
    }

    [Test]
    public void AllFacts_ForcedParallelPath_RecordsConcurrencyInstrumentation()
    {
        var counters = new AnalysisSessionProfilingCounters();
        ArchitectureSourceFileFactIndex index = BuildMultiRootIndex(
            maxParallelism: 4, counters, thresholdOverride: 2);

        _ = index.AllFacts;

        Assert.Multiple(() =>
        {
            Assert.That(counters.ParallelScheduledWorkItems, Is.GreaterThan(0));
            Assert.That(counters.ParallelCompletedWorkItems, Is.EqualTo(counters.ParallelScheduledWorkItems));
            Assert.That(counters.ParallelMergeOperations, Is.GreaterThan(0));
        });
    }

    [Test]
    public void AllFacts_MaxParallelismOne_NeverRecordsParallelInstrumentation()
    {
        var counters = new AnalysisSessionProfilingCounters();
        ArchitectureSourceFileFactIndex index = BuildMultiRootIndex(
            maxParallelism: 1, counters, thresholdOverride: 2);

        _ = index.AllFacts;

        Assert.That(counters.ParallelScheduledWorkItems, Is.EqualTo(0));
    }

    [Test]
    public void AllFacts_ForcedParallelPath_PreCancelledToken_ThrowsOperationCanceledExceptionDirectly()
    {
        using CancellationTokenSource cts = new();
        cts.Cancel();
        ArchitectureSourceFileFactIndex index = BuildMultiRootIndex(
            maxParallelism: 4, thresholdOverride: 2, cancellationToken: cts.Token);

        Exception? thrown = null;
        try
        {
            _ = index.AllFacts;
        }
        catch (Exception ex)
        {
            thrown = ex;
        }

        Assert.That(thrown, Is.InstanceOf<OperationCanceledException>());
        Assert.That(thrown, Is.Not.InstanceOf<AggregateException>());
    }

    [Test]
    public void AllFacts_RepeatedAccessAfterParallelMaterialization_DoesNotRematerialize()
    {
        var counters = new AnalysisSessionProfilingCounters();
        ArchitectureSourceFileFactIndex index = BuildMultiRootIndex(
            maxParallelism: 4, counters, thresholdOverride: 2);

        _ = index.AllFacts;
        int scheduledAfterFirst = counters.ParallelScheduledWorkItems;
        _ = index.AllFacts;
        _ = index.Ambiguities;

        Assert.That(counters.ParallelScheduledWorkItems, Is.EqualTo(scheduledAfterFirst));
        Assert.That(counters.FactIndexMaterializations, Is.EqualTo(1));
    }
}
