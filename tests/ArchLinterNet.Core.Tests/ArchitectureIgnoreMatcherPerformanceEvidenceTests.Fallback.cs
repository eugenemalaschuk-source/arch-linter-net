using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ArchLinterNet.Core.Resolution;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

public sealed partial class ArchitectureIgnoreMatcherPerformanceEvidenceTests
{
    [Test]
    [Explicit("Hardware-sensitive #1127 deferred versioned-baseline fallback evidence; excluded from normal tests and CI.")]
    public void RunDeferredFallbackMatcherBenchmark()
    {
        string root = new ArchitectureRepositoryRootResolver().Resolve();
        MatcherWorkload workload = CreateWorkload(
            "deferred-fallback-i1024-f64-k64",
            MatcherKind.VersionedIdentity,
            exactIgnoreCount: 1_024,
            findingCount: 64,
            duplicatesPerFinding: 1,
            wildcardCount: 0);
        FallbackQuery[] queries = workload.Findings
            .Select((finding, index) => new FallbackQuery(finding.SourceType, $"Target.Type.{index}"))
            .ToArray();

        FallbackMeasurement orderedWarmup = MeasureOrderedFallback(workload, queries);
        FallbackMeasurement indexedWarmup = MeasureIndexedFallback(workload, queries);
        Assert.That(indexedWarmup.ResultChecksum, Is.EqualTo(orderedWarmup.ResultChecksum));

        FallbackMeasurement[] orderedSamples = new FallbackMeasurement[SampleCount];
        FallbackMeasurement[] indexedSamples = new FallbackMeasurement[SampleCount];
        for (int sample = 0; sample < SampleCount; sample++)
        {
            orderedSamples[sample] = MeasureOrderedFallback(workload, queries);
            indexedSamples[sample] = MeasureIndexedFallback(workload, queries);
            Assert.That(indexedSamples[sample].ResultChecksum, Is.EqualTo(orderedWarmup.ResultChecksum),
                $"Deferred fallback changed matching or provenance in sample {sample}.");
        }

        ArchitectureIgnoreMatcherWorkCounter work = new();
        ArchitectureIgnoreMatcherIndex countedIndex = ArchitectureIgnoreMatcherIndex.Build(
            workload.Ignores,
            workCounter: work);
        Assert.That(work.VersionedFallbackEntriesProcessed, Is.Zero);
        _ = countedIndex.IsIgnored(
            queries[0].SourceType,
            queries[0].ForbiddenReference,
            null,
            null,
            null,
            out _);
        Assert.That(work.VersionedFallbackEntriesProcessed, Is.EqualTo(workload.Ignores.Count));
        _ = countedIndex.IsIgnored(
            queries[1].SourceType,
            queries[1].ForbiddenReference,
            null,
            null,
            null,
            out _);
        Assert.That(work.VersionedFallbackEntriesProcessed, Is.EqualTo(workload.Ignores.Count));

        var evidence = new
        {
            Evidence = "architecture-ignore-matching-deferred-fallback",
            Revision = CurrentRevision(root),
            RevisionState = "The implementation source hash identifies the measured matcher code if source changes are uncommitted.",
            ImplementationSourceSha256 = ImplementationSourceSha256(root),
            OrderedReferenceSourceSha256 = OrderedReferenceSourceSha256(root),
            Environment = $"{RuntimeInformation.FrameworkDescription}; {RuntimeInformation.OSDescription}; "
                + $"{RuntimeInformation.ProcessArchitecture}; processors={Environment.ProcessorCount}",
            InputSha256 = InputChecksum(workload),
            IgnoreEntriesI = workload.Ignores.Count,
            FindingsF = queries.Length,
            SampleCount,
            Baseline = new
            {
                FirstQueryMedianMilliseconds = Median(orderedSamples.Select(sample => sample.FirstQueryMilliseconds)),
                SubsequentQueryMedianMillisecondsPerCall = Median(orderedSamples.Select(sample => sample.SubsequentQueryMillisecondsPerCall)),
                TotalQueryMedianMilliseconds = Median(orderedSamples.Select(sample => sample.TotalMilliseconds)),
                TotalAllocatedBytes = Median(orderedSamples.Select(sample => sample.TotalAllocatedBytes)),
            },
            Indexed = new
            {
                IndexBuildMedianMilliseconds = Median(indexedSamples.Select(sample => sample.IndexBuildMilliseconds)),
                FirstQueryIncludingFallbackBuildMedianMilliseconds = Median(indexedSamples.Select(sample => sample.FirstQueryMilliseconds)),
                SubsequentQueryMedianMillisecondsPerCall = Median(indexedSamples.Select(sample => sample.SubsequentQueryMillisecondsPerCall)),
                TotalBuildAndQueryMedianMilliseconds = Median(indexedSamples.Select(sample => sample.TotalMilliseconds)),
                TotalAllocatedBytes = Median(indexedSamples.Select(sample => sample.TotalAllocatedBytes)),
            },
            VersionedFallbackEntriesProcessedAfterFirstQuery = work.VersionedFallbackEntriesProcessed,
            CanonicalResultSha256 = orderedWarmup.ResultChecksum,
        };

        TestContext.Out.WriteLine(JsonSerializer.Serialize(evidence));
        string evidencePath = Path.Combine(
            root,
            "docs",
            "internal",
            "large-solution-benchmarking",
            "evidence",
            "issue-1127",
            "fallback.json");
        File.WriteAllText(evidencePath, JsonSerializer.Serialize(evidence,
            new JsonSerializerOptions { WriteIndented = true }) + Environment.NewLine);
        TestContext.Progress.WriteLine($"Wrote deferred fallback evidence to {Path.GetRelativePath(root, evidencePath)}");
    }

    private static FallbackMeasurement MeasureOrderedFallback(
        MatcherWorkload workload,
        IReadOnlyList<FallbackQuery> queries)
    {
        bool[] suppressed = new bool[queries.Count];
        bool[] loadedBaseline = new bool[queries.Count];
        long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        Stopwatch total = Stopwatch.StartNew();

        Stopwatch firstQuery = Stopwatch.StartNew();
        suppressed[0] = ArchitectureIgnoreMatcherTestReference.IsIgnored(
            queries[0].SourceType,
            queries[0].ForbiddenReference,
            workload.Ignores,
            null,
            null,
            out loadedBaseline[0]);
        firstQuery.Stop();

        Stopwatch subsequentQueries = Stopwatch.StartNew();
        for (int index = 1; index < queries.Count; index++)
        {
            suppressed[index] = ArchitectureIgnoreMatcherTestReference.IsIgnored(
                queries[index].SourceType,
                queries[index].ForbiddenReference,
                workload.Ignores,
                null,
                null,
                out loadedBaseline[index]);
        }

        subsequentQueries.Stop();
        total.Stop();
        long allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
        string checksum = Checksum(new MatchResult(suppressed, loadedBaseline, Array.Empty<int>()));
        return new FallbackMeasurement(
            0,
            firstQuery.Elapsed.TotalMilliseconds,
            subsequentQueries.Elapsed.TotalMilliseconds / (queries.Count - 1),
            total.Elapsed.TotalMilliseconds,
            allocatedBytes,
            checksum);
    }

    private static FallbackMeasurement MeasureIndexedFallback(
        MatcherWorkload workload,
        IReadOnlyList<FallbackQuery> queries)
    {
        bool[] suppressed = new bool[queries.Count];
        bool[] loadedBaseline = new bool[queries.Count];
        long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        Stopwatch total = Stopwatch.StartNew();
        Stopwatch indexBuild = Stopwatch.StartNew();
        ArchitectureIgnoreMatcherIndex index = ArchitectureIgnoreMatcherIndex.Build(workload.Ignores);
        indexBuild.Stop();

        Stopwatch firstQuery = Stopwatch.StartNew();
        suppressed[0] = index.IsIgnored(
            queries[0].SourceType,
            queries[0].ForbiddenReference,
            null,
            null,
            null,
            out loadedBaseline[0]);
        firstQuery.Stop();

        Stopwatch subsequentQueries = Stopwatch.StartNew();
        for (int queryIndex = 1; queryIndex < queries.Count; queryIndex++)
        {
            suppressed[queryIndex] = index.IsIgnored(
                queries[queryIndex].SourceType,
                queries[queryIndex].ForbiddenReference,
                null,
                null,
                null,
                out loadedBaseline[queryIndex]);
        }

        subsequentQueries.Stop();
        total.Stop();
        long allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
        string checksum = Checksum(new MatchResult(suppressed, loadedBaseline, Array.Empty<int>()));
        return new FallbackMeasurement(
            indexBuild.Elapsed.TotalMilliseconds,
            firstQuery.Elapsed.TotalMilliseconds,
            subsequentQueries.Elapsed.TotalMilliseconds / (queries.Count - 1),
            total.Elapsed.TotalMilliseconds,
            allocatedBytes,
            checksum);
    }

    private sealed record FallbackQuery(string SourceType, string ForbiddenReference);

    private sealed record FallbackMeasurement(
        double IndexBuildMilliseconds,
        double FirstQueryMilliseconds,
        double SubsequentQueryMillisecondsPerCall,
        double TotalMilliseconds,
        long TotalAllocatedBytes,
        string ResultChecksum);
}
