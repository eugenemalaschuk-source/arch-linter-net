using System.Diagnostics;
using System.Reflection;
using System.Reflection.Emit;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Execution;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

// Measures the extra role-index pass introduced by canonical annotation discovery when no
// classification mappings are configured. This is intentionally manual: durations are evidence,
// not hardware-dependent CI assertions. The cold series includes type-universe materialization;
// the warm-type series isolates role-index construction over an already materialized universe.
[TestFixture]
[Category("Benchmark")]
public sealed class ArchitectureRoleIndexEmptyConfigurationBenchmark
{
    private const int TypeCount = 5_000;
    private const int SampleCount = 7;
    private const int WarmLookupCount = 20_000;
    private const long WarmIndexAllocationBudgetPerType = 1_024;

    [Test]
    [Category("PerformanceRegression")]
    public void EmptyConfiguration_WithManyUnannotatedTypes_StaysWithinAllocationBudget()
    {
        Assembly assembly = CreateUnannotatedAssembly(TypeCount);
        PrimeClassifierCode();
        ArchitectureTypeIndex typeIndex = new([assembly]);
        Assert.That(typeIndex.AllTypes(), Has.Length.EqualTo(TypeCount));

        Measurement sample = Measure(() => AssertEmptyClassification(
            new ArchitectureRoleIndex(new ArchitectureClassificationConfiguration(), typeIndex)));

        Assert.That(sample.AllocatedBytes, Is.LessThanOrEqualTo(TypeCount * WarmIndexAllocationBudgetPerType),
            "A warm type universe with no annotations should stay within the per-type allocation budget.");
    }

    [Test]
    [Explicit("Hardware-sensitive timing benchmark; run manually when reviewing classifier performance.")]
    public void MeasureEmptyConfigurationColdAndWarmAnalysis()
    {
        Assembly assembly = CreateUnannotatedAssembly(TypeCount);
        PrimeClassifierCode();

        Measurement[] coldSamples = Enumerable.Range(0, SampleCount)
            .Select(_ => Measure(() => AssertEmptyClassification(
                new ArchitectureRoleIndex(new ArchitectureClassificationConfiguration(),
                    new ArchitectureTypeIndex([assembly])))))
            .ToArray();

        ArchitectureTypeIndex warmTypeIndex = new([assembly]);
        Assert.That(warmTypeIndex.AllTypes(), Has.Length.EqualTo(TypeCount));
        Measurement[] warmTypeUniverseSamples = Enumerable.Range(0, SampleCount)
            .Select(_ => Measure(() => AssertEmptyClassification(
                new ArchitectureRoleIndex(new ArchitectureClassificationConfiguration(), warmTypeIndex))))
            .ToArray();
        long warmIndexAllocatedBytes = MedianAllocatedBytes(warmTypeUniverseSamples);
        Assert.That(warmIndexAllocatedBytes, Is.LessThanOrEqualTo(TypeCount * WarmIndexAllocationBudgetPerType),
            "A warm type universe with no annotations should stay within the per-type allocation budget.");

        ArchitectureRoleIndex warmIndex = new(new ArchitectureClassificationConfiguration(), warmTypeIndex);
        AssertEmptyClassification(warmIndex);
        int warmLookupHits = 0;
        Measurement warmCache = Measure(() =>
        {
            for (int index = 0; index < WarmLookupCount; index++)
            {
                if (warmIndex.TryGetRole(typeof(UnannotatedSentinel), out _))
                {
                    warmLookupHits++;
                }
            }
        });
        Assert.That(warmLookupHits, Is.Zero);

        TestContext.Out.WriteLine(
            $"unannotated_types={TypeCount}; cold_index_median_ms={Median(coldSamples, sample => sample.ElapsedMilliseconds):F2}; "
            + $"cold_index_median_allocated_bytes={Median(coldSamples, sample => sample.AllocatedBytes)}; "
            + $"warm_type_universe_index_median_ms={Median(warmTypeUniverseSamples, sample => sample.ElapsedMilliseconds):F2}; "
            + $"warm_type_universe_index_median_allocated_bytes={warmIndexAllocatedBytes}; "
            + $"warm_cache_{WarmLookupCount}_lookups_ms={warmCache.ElapsedMilliseconds:F2}; "
            + $"warm_cache_allocated_bytes={warmCache.AllocatedBytes}");
    }

    private static void PrimeClassifierCode()
    {
        ArchitectureRoleIndex index = new(
            new ArchitectureClassificationConfiguration(), new ArchitectureTypeIndex(Array.Empty<Assembly>()));
        Assert.That(index.ClassifiedTypes(), Is.Empty);
        Assert.That(index.CanonicalAnnotationDiagnostics, Is.Empty);
    }

    private static void AssertEmptyClassification(ArchitectureRoleIndex index)
    {
        Assert.That(index.ClassifiedTypes(), Is.Empty);
        Assert.That(index.CanonicalAnnotationDiagnostics, Is.Empty);
    }

    private static Assembly CreateUnannotatedAssembly(int typeCount)
    {
        AssemblyBuilder assembly = AssemblyBuilder.DefineDynamicAssembly(
            new AssemblyName($"ArchitectureRoleIndexUnannotated.{Guid.NewGuid():N}"), AssemblyBuilderAccess.Run);
        ModuleBuilder module = assembly.DefineDynamicModule("unannotated");
        for (int index = 0; index < typeCount; index++)
        {
            module.DefineType(
                    $"PerformanceFixtures.UnannotatedType{index}",
                    TypeAttributes.Public | TypeAttributes.Class)
                .CreateType();
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

    private static long MedianAllocatedBytes(Measurement[] samples)
    {
        long[] ordered = samples.Select(sample => sample.AllocatedBytes).Order().ToArray();
        return ordered[ordered.Length / 2];
    }

    private sealed record Measurement(double ElapsedMilliseconds, long AllocatedBytes);

    private sealed class UnannotatedSentinel;
}
