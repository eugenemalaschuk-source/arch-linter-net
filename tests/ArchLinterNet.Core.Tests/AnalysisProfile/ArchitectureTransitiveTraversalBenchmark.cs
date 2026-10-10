using System.Diagnostics;
using System.Reflection;
using System.Reflection.Emit;
using ArchLinterNet.Core.Execution;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

// A linked type chain makes the cost of eagerly copying every BFS path visible. The optimized
// traversal retains predecessor links and only constructs paths when a finding needs one.
[TestFixture]
[Category("Benchmark")]
public sealed class ArchitectureTransitiveTraversalBenchmark
{
    private const int ChainLength = 300;
    private const int SampleCount = 7;

    [Test]
    [Explicit("Hardware-sensitive timing benchmark; run manually when reviewing transitive traversal performance.")]
    public void MeasureTransitiveWalkWhenNoForbiddenPathsNeedMaterialization()
    {
        (_, Type root) = CreateChain(ChainLength);
        Func<Type, bool> traversePredicate = type => type.Namespace == "PerformanceFixtures";

        _ = MeasureWithPaths(root, traversePredicate);
        _ = MeasureWithoutPaths(root, traversePredicate);
        Measurement[] withPaths = Enumerable.Range(0, SampleCount)
            .Select(_ => MeasureWithPaths(root, traversePredicate)).ToArray();
        Measurement[] withoutPaths = Enumerable.Range(0, SampleCount)
            .Select(_ => MeasureWithoutPaths(root, traversePredicate)).ToArray();

        double withPathMilliseconds = Median(withPaths, sample => sample.ElapsedMilliseconds);
        double withoutPathMilliseconds = Median(withoutPaths, sample => sample.ElapsedMilliseconds);
        long withPathBytes = Median(withPaths, sample => sample.AllocatedBytes);
        long withoutPathBytes = Median(withoutPaths, sample => sample.AllocatedBytes);
        TestContext.Out.WriteLine(
            $"chain_length={ChainLength}; traversal_with_eager_paths_median_ms={withPathMilliseconds:F2}; "
            + $"traversal_with_lazy_paths_median_ms={withoutPathMilliseconds:F2}; "
            + $"speedup={withPathMilliseconds / withoutPathMilliseconds:F2}x; "
            + $"eager_paths_median_allocated_bytes={withPathBytes}; "
            + $"lazy_paths_median_allocated_bytes={withoutPathBytes}; "
            + $"allocation_reduction_percent={(withPathBytes - withoutPathBytes) * 100d / withPathBytes:F1}");
    }

    private static Measurement MeasureWithPaths(Type root, Func<Type, bool> traversePredicate) => Measure(() =>
    {
        ArchitectureReferenceGraph graph = new();
        int count = graph.GetTransitiveReferencedTypes(root, traversePredicate).Count();
        Assert.That(count, Is.GreaterThanOrEqualTo(ChainLength));
    });

    private static Measurement MeasureWithoutPaths(Type root, Func<Type, bool> traversePredicate) => Measure(() =>
    {
        ArchitectureReferenceGraph graph = new();
        int count = graph.EnumerateTransitiveReferencedTypes(root, traversePredicate).Count();
        Assert.That(count, Is.GreaterThanOrEqualTo(ChainLength));
    });

    private static (Assembly Assembly, Type Root) CreateChain(int length)
    {
        AssemblyBuilder assembly = AssemblyBuilder.DefineDynamicAssembly(
            new AssemblyName($"ArchitectureTransitiveTraversal.{Guid.NewGuid():N}"), AssemblyBuilderAccess.Run);
        ModuleBuilder module = assembly.DefineDynamicModule("chain");
        TypeBuilder[] builders = Enumerable.Range(0, length)
            .Select(index => module.DefineType(
                $"PerformanceFixtures.ChainNode{index}",
                TypeAttributes.Public | TypeAttributes.Class))
            .ToArray();
        for (int index = 0; index < builders.Length; index++)
        {
            Type target = index + 1 < builders.Length ? builders[index + 1] : typeof(string);
            builders[index].DefineField("Next", target, FieldAttributes.Public);
        }

        for (int index = builders.Length - 1; index >= 0; index--)
        {
            builders[index].CreateType();
        }

        return (assembly, assembly.GetType("PerformanceFixtures.ChainNode0", throwOnError: true)!);
    }

    private static Measurement Measure(Action action)
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        Stopwatch stopwatch = Stopwatch.StartNew();
        action();
        stopwatch.Stop();
        return new Measurement(stopwatch.Elapsed.TotalMilliseconds,
            GC.GetAllocatedBytesForCurrentThread() - allocatedBefore);
    }

    private static double Median(Measurement[] samples, Func<Measurement, double> selector)
    {
        double[] ordered = samples.Select(selector).Order().ToArray();
        return ordered[ordered.Length / 2];
    }

    private static long Median(Measurement[] samples, Func<Measurement, long> selector)
    {
        long[] ordered = samples.Select(selector).Order().ToArray();
        return ordered[ordered.Length / 2];
    }

    private sealed record Measurement(double ElapsedMilliseconds, long AllocatedBytes);
}
