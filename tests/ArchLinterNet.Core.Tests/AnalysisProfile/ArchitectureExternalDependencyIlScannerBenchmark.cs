using System.Diagnostics;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Resolution;
using ArchLinterNet.Core.Scanning;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

// Measures repeated external-group scans over one scanner instance, matching graph seeding and
// contracts that check several external groups. Run against the base and candidate revisions for
// before/after evidence; timings are never CI thresholds.
[TestFixture]
[Category("Benchmark")]
public sealed class ArchitectureExternalDependencyIlScannerBenchmark
{
    private const int SourceTypeCopies = 24;
    private const int SampleCount = 7;

    private static readonly Type[] _representativeTypes =
    {
        typeof(ExternalDependencyContractTestsFixtures.Core.CoreTypeWithMethodCall),
        typeof(ExternalDependencyContractTestsFixtures.Core.CoreTypeWithConstructorCall),
        typeof(ExternalDependencyContractTestsFixtures.Core.CoreTypeWithPropertyAccess),
        typeof(ExternalDependencyContractTestsFixtures.Core.CoreTypeWithGenericOnlyInBody),
        typeof(ExternalDependencyContractTestsFixtures.Core.CoreGenericTypeWithVendorCall<>),
        typeof(ExternalDependencyContractTestsFixtures.Core.CoreTypeWithGenericMethodVendorCall),
        typeof(ExternalDependencyContractTestsFixtures.Core.PureCoreType),
        typeof(ExternalDependencyContractTestsFixtures.UnityStyle.CoreTypeWithUnityMethodBody)
    };

    [Test]
    [Explicit("Hardware-sensitive timing benchmark; run manually when reviewing external scanner performance.")]
    public void MeasureRepeatedExternalGroupScans()
    {
        Type[] sourceTypes = Enumerable.Range(0, SourceTypeCopies)
            .SelectMany(_ => _representativeTypes)
            .ToArray();
        ArchitectureExternalDependencyGroup[] groups = CreateGroups();
        ArchitectureContractExecutionContext executionContext = new(
            "benchmark", null, Array.Empty<ArchitectureIgnoredViolation>(), false, null, null);

        Measurement[] uncached = Enumerable.Range(0, SampleCount)
            .Select(_ => Measure(sourceTypes, groups, executionContext))
            .ToArray();

        double medianMilliseconds = Median(uncached, sample => sample.ElapsedMilliseconds);
        long medianBytes = Median(uncached, sample => sample.AllocatedBytes);
        TestContext.Out.WriteLine(
            $"source_types={sourceTypes.Length}; groups={groups.Length}; "
            + $"median_ms={medianMilliseconds:F2}; median_allocated_bytes={medianBytes}");
    }

    private static ArchitectureExternalDependencyGroup[] CreateGroups()
    {
        return
        [
            new() { NamespacePrefixes = ["ExternalDependencyContractTestsFixtures.VendorSdk"] },
            new() { NamespacePrefixes = ["UnityEngine"] },
            new() { NamespacePrefixes = ["Benchmark.Missing.One"] },
            new() { NamespacePrefixes = ["Benchmark.Missing.Two"] },
            new() { NamespacePrefixes = ["Benchmark.Missing.Three"] },
            new() { TypePrefixes = ["Benchmark.Missing.Four"] }
        ];
    }

    private static Measurement Measure(
        Type[] sourceTypes,
        ArchitectureExternalDependencyGroup[] groups,
        ArchitectureContractExecutionContext executionContext)
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        Stopwatch stopwatch = Stopwatch.StartNew();
        ArchitectureExternalDependencyIlScanner scanner = new();
        for (int index = 0; index < groups.Length; index++)
        {
            _ = scanner.FindMethodBodyViolations(
                sourceTypes, $"group-{index}", groups[index], executionContext).ToArray();
        }

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
