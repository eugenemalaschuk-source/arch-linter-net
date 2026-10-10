using System.Diagnostics;
using System.Reflection;
using System.Reflection.Emit;
using ArchLinterNet.Core.Execution;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

// Models multiple contract checkers consuming the same compiled type universe. The baseline scans
// direct references independently on every pass; the optimized path shares the session graph.
[TestFixture]
[Category("Benchmark")]
public sealed class ArchitectureReferenceGraphReuseBenchmark
{
    private const int TypeCount = 2_000;
    private const int PassCount = 3;
    private const int SampleCount = 7;

    [Test]
    [Explicit("Hardware-sensitive timing benchmark; run manually when reviewing reference-scan performance.")]
    public void MeasureRepeatedDirectReferenceScansWithAndWithoutSessionGraph()
    {
        Assembly assembly = CreateAssembly(TypeCount);
        Type[] types = assembly.GetTypes();

        _ = MeasureDirect(types);
        _ = MeasureGraph(types);
        Measurement[] direct = Enumerable.Range(0, SampleCount).Select(_ => MeasureDirect(types)).ToArray();
        Measurement[] graph = Enumerable.Range(0, SampleCount).Select(_ => MeasureGraph(types)).ToArray();

        double directMilliseconds = Median(direct, sample => sample.ElapsedMilliseconds);
        double graphMilliseconds = Median(graph, sample => sample.ElapsedMilliseconds);
        long directBytes = Median(direct, sample => sample.AllocatedBytes);
        long graphBytes = Median(graph, sample => sample.AllocatedBytes);
        TestContext.Out.WriteLine(
            $"types={TypeCount}; passes={PassCount}; direct_scan_median_ms={directMilliseconds:F2}; "
            + $"session_graph_median_ms={graphMilliseconds:F2}; speedup={directMilliseconds / graphMilliseconds:F2}x; "
            + $"direct_scan_median_allocated_bytes={directBytes}; "
            + $"session_graph_median_allocated_bytes={graphBytes}; "
            + $"allocation_reduction_percent={(directBytes - graphBytes) * 100d / directBytes:F1}");
    }

    private static Measurement MeasureDirect(Type[] types) => Measure(() =>
    {
        int count = 0;
        for (int pass = 0; pass < PassCount; pass++)
        {
            foreach (Type type in types)
            {
                count += ArchLinterNet.Core.Scanning.ArchitectureReferenceScanner
                    .GetReferencedTypes(type).Count();
            }
        }

        Assert.That(count, Is.GreaterThan(0));
    });

    private static Measurement MeasureGraph(Type[] types) => Measure(() =>
    {
        ArchitectureReferenceGraph graph = new();
        int count = 0;
        for (int pass = 0; pass < PassCount; pass++)
        {
            foreach (Type type in types)
            {
                count += graph.GetReferencedTypes(type).Count;
            }
        }

        Assert.That(count, Is.GreaterThan(0));
    });

    private static Assembly CreateAssembly(int typeCount)
    {
        AssemblyBuilder assembly = AssemblyBuilder.DefineDynamicAssembly(
            new AssemblyName($"ArchitectureReferenceGraphReuse.{Guid.NewGuid():N}"), AssemblyBuilderAccess.Run);
        ModuleBuilder module = assembly.DefineDynamicModule("references");
        for (int index = 0; index < typeCount; index++)
        {
            TypeBuilder type = module.DefineType(
                $"PerformanceFixtures.ReferencedType{index}",
                TypeAttributes.Public | TypeAttributes.Class);
            type.DefineField("Payload", typeof(List<string>), FieldAttributes.Public);
            type.CreateType();
        }

        return assembly;
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
