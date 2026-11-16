using System.Runtime.InteropServices;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Profiling;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests.Benchmarking;

[TestFixture]
[NonParallelizable]
[Explicit("Hardware-sensitive retention evidence for many distinct method-body layer contexts.")]
[Category("Benchmark")]
public sealed class MethodBodyDistinctLayerRetentionBenchmark
{
    private const string NamespaceRoot = "ArchLinterNet.Core.Tests.DistinctLayerPreparation";

    private const int SampleCount = 3;

    [Test]
    [CancelAfter(300_000)]
    public void MeasureRetainedHeapAcrossDistinctLayerInputs()
    {
        var repositoryRoot = Path.Combine(
            Path.GetTempPath(), $"arch-linter-distinct-layer-retention-{Guid.NewGuid():N}");
        string sourceRoot = Path.Combine(repositoryRoot, "src", "Layers");
        Directory.CreateDirectory(sourceRoot);

        try
        {
            WriteLayerSources(sourceRoot);
            _ = MeasureSample(repositoryRoot, 20);

            TestContext.Out.WriteLine($"runtime={RuntimeInformation.FrameworkDescription}");
            TestContext.Out.WriteLine($"os={RuntimeInformation.OSDescription}");
            TestContext.Out.WriteLine(
                $"processors={Environment.ProcessorCount}; gc_server={System.Runtime.GCSettings.IsServerGC}");
            TestContext.Out.WriteLine("shape: one source per distinct namespace suffix; default Roslyn references; builds excluded.");

            int[] contextCounts = [1, 5, 20];
            foreach (int contextCount in contextCounts)
            {
                List<RetentionSample> samples = new(SampleCount);
                for (int sampleOrdinal = 0; sampleOrdinal < SampleCount; sampleOrdinal++)
                {
                    samples.Add(MeasureSample(repositoryRoot, contextCount));
                }

                Assert.That(samples.All(sample => sample.CompilationCount == contextCount), Is.True);
                Assert.That(samples.All(sample => sample.SyntaxTreeParseCount == contextCount), Is.True);
                TestContext.Out.WriteLine(
                    $"distinct_contexts={contextCount}; " +
                    $"allocated_bytes_median={Median(samples, sample => sample.AllocatedBytes)}; " +
                    $"live_heap_delta_after_evaluation_bytes_median={Median(samples, sample => sample.LiveHeapDeltaBytes)}; " +
                    $"compilations={contextCount}; parses={contextCount}");
                foreach (RetentionSample sample in samples)
                {
                    TestContext.Out.WriteLine(
                        $"  allocated_bytes={sample.AllocatedBytes}; " +
                        $"live_heap_delta_bytes={sample.LiveHeapDeltaBytes}; " +
                        $"compilations={sample.CompilationCount}; parses={sample.SyntaxTreeParseCount}");
                }
            }
        }
        finally
        {
            if (Directory.Exists(repositoryRoot))
            {
                Directory.Delete(repositoryRoot, recursive: true);
            }
        }
    }

    private static RetentionSample MeasureSample(string repositoryRoot, int contextCount)
    {
        CollectGarbage();
        long heapBefore = GC.GetTotalMemory(forceFullCollection: true);
        long allocatedBefore = GC.GetTotalAllocatedBytes(precise: true);
        ArchitectureMethodBodyPreparationCache cache = new();
        BenchmarkWorkObserver observer = new();
        try
        {
            for (int index = 0; index < contextCount; index++)
            {
                ArchitectureLayer layer = new()
                {
                    Namespace = NamespaceRoot,
                    NamespaceSuffix = $"Layer{index:D2}",
                };
                ArchitectureMethodBodyPreparation preparation = cache.Prepare(
                    repositoryRoot,
                    layer,
                    ["src/Layers"],
                    preprocessorSymbols: null,
                    projectDiscovery: null,
                    buildConfiguration: "Debug",
                    cancellationToken: CancellationToken.None,
                    benchmarkWorkObserver: observer);
                Assert.That(preparation.SourceFilePaths, Has.Count.EqualTo(1));
            }

            long allocatedAfter = GC.GetTotalAllocatedBytes(precise: true);
            long heapAfter = GC.GetTotalMemory(forceFullCollection: true);
            return new RetentionSample(
                allocatedAfter - allocatedBefore,
                heapAfter - heapBefore,
                observer.MethodBodyCompilationCalls,
                observer.MethodBodySyntaxTreeParseCalls);
        }
        finally
        {
            cache.Dispose();
        }
    }

    private static void WriteLayerSources(string sourceRoot)
    {
        for (int index = 0; index < 20; index++)
        {
            var suffix = $"Layer{index:D2}";
            var source = $$"""
                namespace {{NamespaceRoot}}.{{suffix}};

                public sealed class {{suffix}}Caller
                {
                    public void Write() { System.Console.WriteLine("distinct"); }
                }
                """;
            File.WriteAllText(Path.Combine(sourceRoot, $"{suffix}.cs"), source);
        }
    }

    private static void CollectGarbage()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }

    private static long Median(IReadOnlyList<RetentionSample> samples, Func<RetentionSample, long> select)
    {
        return samples.Select(select).Order().ElementAt(samples.Count / 2);
    }

    private sealed record RetentionSample(
        long AllocatedBytes,
        long LiveHeapDeltaBytes,
        long CompilationCount,
        long SyntaxTreeParseCount);
}
