using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ArchLinterNet.Core.Scanning;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
[Explicit("Environment-labelled detector-local evidence for issue #1124; select this test explicitly.")]
[Category("Benchmark")]
public sealed class ArchitectureCycleDetectorBenchmarkHarness
{
    private static readonly int[] _defaultSizes = [8, 12, 16, 20];

    private static readonly int[] _defaultSccSizes = [4, 6, 8];

    [Test]
    public void RunCompleteOrderedDagMatrix()
    {
        int[] sizes = ParseSizes(Environment.GetEnvironmentVariable("ARCHLINTER_CYCLE_BENCHMARK_SIZES"));
        List<object> measurements = [];

        foreach (int size in sizes)
        {
            Dictionary<string, HashSet<string>> graph = CreateCompleteOrderedDag(size);
            _ = ArchitectureCycleDetector.FindCycles(CreateCompleteOrderedDag(4));

            int sampleCount = size <= 12 ? 5 : size <= 16 ? 3 : 1;
            List<double> elapsedMilliseconds = [];
            List<long> allocatedBytes = [];
            long retainedBefore = GC.GetTotalMemory(forceFullCollection: true);
            long peakWorkingSetBefore = Process.GetCurrentProcess().PeakWorkingSet64;

            for (int sample = 0; sample < sampleCount; sample++)
            {
                long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
                Stopwatch stopwatch = Stopwatch.StartNew();
                IReadOnlyCollection<string> cycles = ArchitectureCycleDetector.FindCycles(graph);
                stopwatch.Stop();
                allocatedBytes.Add(GC.GetAllocatedBytesForCurrentThread() - allocatedBefore);
                elapsedMilliseconds.Add(stopwatch.Elapsed.TotalMilliseconds);

                Assert.That(cycles, Is.Empty, $"D({size}, {size * (size - 1) / 2}) must be acyclic.");
            }

            long peakWorkingSetAfter = Process.GetCurrentProcess().PeakWorkingSet64;
            ArchitectureCycleDetectorWorkCounter work = new();
            IReadOnlyCollection<string> observedCycles = ArchitectureCycleDetector.FindCycles(graph, work);
            Assert.That(observedCycles, Is.Empty, $"D({size}, {size * (size - 1) / 2}) must be acyclic.");
            int cycleCount = observedCycles.Count;
            string cycleSha256 = Sha256(string.Join("\n", observedCycles));
            observedCycles = Array.Empty<string>();
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            long retainedAfter = GC.GetTotalMemory(forceFullCollection: true);
            measurements.Add(new
            {
                Vertices = size,
                Edges = size * (size - 1) / 2,
                BaselineRecursiveVisitModel = ((BigInteger.One << size) - BigInteger.One)
                    .ToString(CultureInfo.InvariantCulture),
                BaselineEdgeCandidateModel = ((BigInteger.One << size) - size - 1)
                    .ToString(CultureInfo.InvariantCulture),
                Samples = sampleCount,
                ElapsedMilliseconds = elapsedMilliseconds,
                MedianElapsedMilliseconds = Median(elapsedMilliseconds),
                AllocatedBytes = allocatedBytes,
                MedianAllocatedBytes = Median(allocatedBytes),
                RetainedManagedBytesBefore = retainedBefore,
                RetainedManagedBytesAfter = retainedAfter,
                PeakWorkingSetBytesBefore = peakWorkingSetBefore,
                PeakWorkingSetBytesAfter = peakWorkingSetAfter,
                PeakWorkingSetDeltaBytes = Math.Max(0, peakWorkingSetAfter - peakWorkingSetBefore),
                CycleCount = cycleCount,
                CycleSha256 = cycleSha256,
                Work = new
                {
                    work.PreparedVertexCount,
                    work.PreparedEdgeCount,
                    work.AdjacencyListsSorted,
                    work.ReverseDegreeEdgeVisits,
                    work.ReverseFillEdgeVisits,
                    work.FirstPassVertexVisits,
                    work.FirstPassEdgeVisits,
                    work.SecondPassVertexVisits,
                    work.SecondPassEdgeVisits,
                    work.CycleAdjacencyFilterEdgeVisits,
                    work.CycleAdjacencyCopyEdgeVisits,
                    work.CycleSearchVertexVisits,
                    work.CycleSearchEdgeCandidates,
                    work.EmittedCycleCount,
                },
            });
        }

        TestContext.Progress.WriteLine(JsonSerializer.Serialize(new
        {
            Runtime = RuntimeInformation.FrameworkDescription,
            OperatingSystem = RuntimeInformation.OSDescription,
            Architecture = RuntimeInformation.ProcessArchitecture.ToString(),
            Configuration = "Release",
            SourceIdentity = Environment.GetEnvironmentVariable("ARCHLINTER_CYCLE_SOURCE_IDENTITY") ?? "unreported",
            Measurements = measurements,
        }, new JsonSerializerOptions { WriteIndented = true }));
    }

    [Test]
    public void RunCompleteStrongComponentMatrix()
    {
        string? configuredSizes = Environment.GetEnvironmentVariable("ARCHLINTER_CYCLE_SCC_BENCHMARK_SIZES");
        int[] sizes = string.IsNullOrWhiteSpace(configuredSizes) ? _defaultSccSizes : ParseSizes(configuredSizes);
        if (sizes.Any(size => size > 8))
        {
            throw new ArgumentOutOfRangeException(
                nameof(configuredSizes),
                "Complete SCC timing samples are bounded to eight vertices because required output grows factorially.");
        }

        List<object> measurements = [];

        foreach (int size in sizes)
        {
            Dictionary<string, HashSet<string>> graph = CreateCompleteDirectedScc(size);
            _ = ArchitectureCycleDetector.FindCycles(CreateCompleteDirectedScc(3));
            long expectedCycleCount = (long)CompleteStrongComponentCycleCount(size);

            int sampleCount = size <= 6 ? 5 : 1;
            List<double> elapsedMilliseconds = [];
            List<long> allocatedBytes = [];
            long retainedBefore = GC.GetTotalMemory(forceFullCollection: true);
            long peakWorkingSetBefore = Process.GetCurrentProcess().PeakWorkingSet64;

            for (int sample = 0; sample < sampleCount; sample++)
            {
                long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
                Stopwatch stopwatch = Stopwatch.StartNew();
                IReadOnlyCollection<string> cycles = ArchitectureCycleDetector.FindCycles(graph);
                stopwatch.Stop();
                allocatedBytes.Add(GC.GetAllocatedBytesForCurrentThread() - allocatedBefore);
                elapsedMilliseconds.Add(stopwatch.Elapsed.TotalMilliseconds);
                Assert.That(cycles, Has.Count.EqualTo(expectedCycleCount));
            }

            long peakWorkingSetAfter = Process.GetCurrentProcess().PeakWorkingSet64;
            ArchitectureCycleDetectorWorkCounter work = new();
            IReadOnlyCollection<string> observedCycles = ArchitectureCycleDetector.FindCycles(graph, work);
            Assert.That(observedCycles, Has.Count.EqualTo(expectedCycleCount));
            int cycleCount = observedCycles.Count;
            string cycleSha256 = Sha256(string.Join("\n", observedCycles));
            observedCycles = Array.Empty<string>();
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            long retainedAfter = GC.GetTotalMemory(forceFullCollection: true);
            measurements.Add(new
            {
                Vertices = size,
                Edges = size * (size - 1),
                BaselineRecursiveVisitModel = CompleteStrongComponentVisitCount(size).ToString(CultureInfo.InvariantCulture),
                BaselineEdgeCandidateModel = (CompleteStrongComponentVisitCount(size) * (size - 1))
                    .ToString(CultureInfo.InvariantCulture),
                Samples = sampleCount,
                ElapsedMilliseconds = elapsedMilliseconds,
                MedianElapsedMilliseconds = Median(elapsedMilliseconds),
                AllocatedBytes = allocatedBytes,
                MedianAllocatedBytes = Median(allocatedBytes),
                RetainedManagedBytesBefore = retainedBefore,
                RetainedManagedBytesAfter = retainedAfter,
                PeakWorkingSetBytesBefore = peakWorkingSetBefore,
                PeakWorkingSetBytesAfter = peakWorkingSetAfter,
                PeakWorkingSetDeltaBytes = Math.Max(0, peakWorkingSetAfter - peakWorkingSetBefore),
                CycleCount = cycleCount,
                CycleSha256 = cycleSha256,
                Work = new
                {
                    work.PreparedVertexCount,
                    work.PreparedEdgeCount,
                    work.AdjacencyListsSorted,
                    work.ReverseDegreeEdgeVisits,
                    work.ReverseFillEdgeVisits,
                    work.FirstPassVertexVisits,
                    work.FirstPassEdgeVisits,
                    work.SecondPassVertexVisits,
                    work.SecondPassEdgeVisits,
                    work.CycleAdjacencyFilterEdgeVisits,
                    work.CycleAdjacencyCopyEdgeVisits,
                    work.CycleSearchVertexVisits,
                    work.CycleSearchEdgeCandidates,
                    work.EmittedCycleCount,
                },
            });
        }

        TestContext.Progress.WriteLine(JsonSerializer.Serialize(new
        {
            Runtime = RuntimeInformation.FrameworkDescription,
            OperatingSystem = RuntimeInformation.OSDescription,
            Architecture = RuntimeInformation.ProcessArchitecture.ToString(),
            Configuration = "Release",
            SourceIdentity = Environment.GetEnvironmentVariable("ARCHLINTER_CYCLE_SOURCE_IDENTITY") ?? "unreported",
            GraphShape = "complete directed SCC without self-edges",
            Measurements = measurements,
        }, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static int[] ParseSizes(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return _defaultSizes;
        }

        int[] sizes = value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(size => int.Parse(size, CultureInfo.InvariantCulture))
            .ToArray();
        if (sizes.Length == 0 || sizes.Any(size => size <= 0))
        {
            throw new ArgumentException("Benchmark sizes must be positive comma-separated integers.", nameof(value));
        }

        return sizes;
    }

    private static Dictionary<string, HashSet<string>> CreateCompleteOrderedDag(int vertexCount)
    {
        Dictionary<string, HashSet<string>> graph = new(StringComparer.Ordinal);
        for (int source = 0; source < vertexCount; source++)
        {
            HashSet<string> targets = new(StringComparer.Ordinal);
            for (int target = source + 1; target < vertexCount; target++)
            {
                targets.Add(NodeName(target));
            }

            graph.Add(NodeName(source), targets);
        }

        return graph;
    }

    private static Dictionary<string, HashSet<string>> CreateCompleteDirectedScc(int vertexCount)
    {
        Dictionary<string, HashSet<string>> graph = new(StringComparer.Ordinal);
        for (int source = 0; source < vertexCount; source++)
        {
            HashSet<string> targets = new(StringComparer.Ordinal);
            for (int target = 0; target < vertexCount; target++)
            {
                if (target != source)
                {
                    targets.Add(NodeName(target));
                }
            }

            graph.Add(NodeName(source), targets);
        }

        return graph;
    }

    private static string NodeName(int index) => $"N{index:D2}";

    private static double Median(IReadOnlyList<double> values)
    {
        double[] ordered = values.Order().ToArray();
        return ordered[ordered.Length / 2];
    }

    private static long Median(IReadOnlyList<long> values)
    {
        long[] ordered = values.Order().ToArray();
        return ordered[ordered.Length / 2];
    }

    private static BigInteger CompleteStrongComponentVisitCount(int vertexCount)
    {
        BigInteger visitsPerRoot = BigInteger.One;
        BigInteger pathCount = BigInteger.One;
        for (int pathLength = 1; pathLength < vertexCount; pathLength++)
        {
            pathCount *= vertexCount - pathLength;
            visitsPerRoot += pathCount;
        }

        return visitsPerRoot * vertexCount;
    }

    private static BigInteger CompleteStrongComponentCycleCount(int vertexCount) =>
        CompleteStrongComponentVisitCount(vertexCount) - vertexCount;

    private static string Sha256(string value) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
