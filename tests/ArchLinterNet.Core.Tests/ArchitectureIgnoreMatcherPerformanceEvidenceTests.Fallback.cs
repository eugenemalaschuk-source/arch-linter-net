using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using ArchLinterNet.Core.Resolution;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

public sealed partial class ArchitectureIgnoreMatcherPerformanceEvidenceTests
{
    private const int MissWorkloadFallbackIndexBuildQueryNumber = 5;

    [Test]
    [Explicit("Hardware-sensitive #1127 deferred versioned-baseline fallback evidence; excluded from normal tests and CI.")]
    public void RunDeferredFallbackMatcherBenchmark()
    {
        string root = new ArchitectureRepositoryRootResolver().Resolve();
        FallbackScenario[] scenarios =
        [
            CreateFallbackScenario("early-matches-i1024-f64-k64", duplicatesPerFinding: 1, indexBuildQueryNumber: 0),
            CreateFallbackScenario("no-matches-i1024-f64-k0", duplicatesPerFinding: 0,
                indexBuildQueryNumber: MissWorkloadFallbackIndexBuildQueryNumber),
        ];
        List<object> scenarioEvidence = [];

        foreach (FallbackScenario scenario in scenarios)
        {
            FallbackMeasurement orderedWarmup = MeasureOrderedFallback(scenario.Workload, scenario.Queries);
            FallbackMeasurement indexedWarmup = MeasureIndexedFallback(scenario);
            Assert.That(indexedWarmup.ResultChecksum, Is.EqualTo(orderedWarmup.ResultChecksum));

            var orderedSamples = new FallbackMeasurement[SampleCount];
            var indexedSamples = new FallbackMeasurement[SampleCount];
            for (int sample = 0; sample < SampleCount; sample++)
            {
                orderedSamples[sample] = MeasureOrderedFallback(scenario.Workload, scenario.Queries);
                indexedSamples[sample] = MeasureIndexedFallback(scenario);
                Assert.That(indexedSamples[sample].ResultChecksum, Is.EqualTo(orderedWarmup.ResultChecksum),
                    $"Deferred fallback changed matching or provenance for '{scenario.Name}' in sample {sample}.");
            }

            FallbackWorkEvidence work = MeasureFallbackWork(scenario);
            var row = new
            {
                Scenario = scenario.Name,
                InputSha256 = InputChecksum(scenario.Workload),
                IgnoreEntriesI = scenario.Workload.Ignores.Count,
                FindingsF = scenario.Queries.Length,
                RequiredMatchesK = scenario.Workload.RequiredMatches,
                ExpectedFallbackIndexBuildQueryNumber = scenario.IndexBuildQueryNumber,
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
                    FirstQueryMedianMilliseconds = Median(indexedSamples.Select(sample => sample.FirstQueryMilliseconds)),
                    LazyFallbackIndexQueryMedianMilliseconds = Median(indexedSamples.Select(sample => sample.LazyFallbackIndexQueryMilliseconds)),
                    SubsequentQueryMedianMillisecondsPerCall = Median(indexedSamples.Select(sample => sample.SubsequentQueryMillisecondsPerCall)),
                    TotalBuildAndQueryMedianMilliseconds = Median(indexedSamples.Select(sample => sample.TotalMilliseconds)),
                    TotalAllocatedBytes = Median(indexedSamples.Select(sample => sample.TotalAllocatedBytes)),
                },
                work.VersionedIdentityEntriesIndexed,
                work.MissingIdentityEntriesScanned,
                work.VersionedFallbackEntriesProcessed,
                CanonicalResultSha256 = orderedWarmup.ResultChecksum,
            };
            scenarioEvidence.Add(row);
            TestContext.Out.WriteLine(JsonSerializer.Serialize(row));
        }

        var evidence = new
        {
            Evidence = "architecture-ignore-matching-deferred-fallback",
            Revision = CurrentRevision(root),
            RevisionState = "Revision records HEAD at measurement time; the implementation source SHA-256 identifies the measured matcher code.",
            ImplementationSourceSha256 = ImplementationSourceSha256(root),
            OrderedReferenceSourceSha256 = OrderedReferenceSourceSha256(root),
            Environment = $"{RuntimeInformation.FrameworkDescription}; {RuntimeInformation.OSDescription}; "
                + $"{RuntimeInformation.ProcessArchitecture}; processors={Environment.ProcessorCount}",
            SampleCount,
            Scenarios = scenarioEvidence,
        };
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

    private static FallbackScenario CreateFallbackScenario(
        string name,
        int duplicatesPerFinding,
        int indexBuildQueryNumber)
    {
        MatcherWorkload workload = CreateWorkload(
            name,
            MatcherKind.VersionedIdentity,
            exactIgnoreCount: 1_024,
            findingCount: 64,
            duplicatesPerFinding,
            wildcardCount: 0);
        FallbackQuery[] queries = workload.Findings
            .Select((finding, index) => new FallbackQuery(finding.SourceType, $"Target.Type.{index}"))
            .ToArray();
        return new FallbackScenario(name, workload, queries, indexBuildQueryNumber);
    }

    private static FallbackMeasurement MeasureOrderedFallback(
        MatcherWorkload workload,
        IReadOnlyList<FallbackQuery> queries)
    {
        bool[] suppressed = new bool[queries.Count];
        bool[] loadedBaseline = new bool[queries.Count];
        long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        var total = Stopwatch.StartNew();

        var firstQuery = Stopwatch.StartNew();
        suppressed[0] = ArchitectureIgnoreMatcherTestReference.IsIgnored(
            queries[0].SourceType,
            queries[0].ForbiddenReference,
            workload.Ignores,
            null,
            null,
            out loadedBaseline[0]);
        firstQuery.Stop();

        var subsequentQueries = Stopwatch.StartNew();
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
            0,
            subsequentQueries.Elapsed.TotalMilliseconds / (queries.Count - 1),
            total.Elapsed.TotalMilliseconds,
            allocatedBytes,
            checksum);
    }

    private static FallbackMeasurement MeasureIndexedFallback(FallbackScenario scenario)
    {
        bool[] suppressed = new bool[scenario.Queries.Length];
        bool[] loadedBaseline = new bool[scenario.Queries.Length];
        long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        var total = Stopwatch.StartNew();
        var indexBuild = Stopwatch.StartNew();
        var index = ArchitectureIgnoreMatcherIndex.Build(scenario.Workload.Ignores);
        indexBuild.Stop();

        var firstQuery = Stopwatch.StartNew();
        RunIndexedFallbackQuery(index, scenario.Queries[0], out suppressed[0], out loadedBaseline[0]);
        firstQuery.Stop();

        var subsequentQueries = Stopwatch.StartNew();
        double lazyFallbackIndexQueryMilliseconds = 0;
        int subsequentQueryCount = 0;
        for (int queryIndex = 1; queryIndex < scenario.Queries.Length; queryIndex++)
        {
            if (queryIndex + 1 == scenario.IndexBuildQueryNumber)
            {
                subsequentQueries.Stop();
                var lazyFallbackQuery = Stopwatch.StartNew();
                RunIndexedFallbackQuery(index, scenario.Queries[queryIndex], out suppressed[queryIndex], out loadedBaseline[queryIndex]);
                lazyFallbackQuery.Stop();
                lazyFallbackIndexQueryMilliseconds = lazyFallbackQuery.Elapsed.TotalMilliseconds;
                subsequentQueries.Start();
                continue;
            }

            RunIndexedFallbackQuery(index, scenario.Queries[queryIndex], out suppressed[queryIndex], out loadedBaseline[queryIndex]);
            subsequentQueryCount++;
        }

        subsequentQueries.Stop();
        total.Stop();
        long allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
        string checksum = Checksum(new MatchResult(suppressed, loadedBaseline, Array.Empty<int>()));
        return new FallbackMeasurement(
            indexBuild.Elapsed.TotalMilliseconds,
            firstQuery.Elapsed.TotalMilliseconds,
            lazyFallbackIndexQueryMilliseconds,
            subsequentQueryCount == 0 ? 0 : subsequentQueries.Elapsed.TotalMilliseconds / subsequentQueryCount,
            total.Elapsed.TotalMilliseconds,
            allocatedBytes,
            checksum);
    }

    private static void RunIndexedFallbackQuery(
        ArchitectureIgnoreMatcherIndex index,
        FallbackQuery query,
        out bool suppressed,
        out bool loadedBaseline)
    {
        suppressed = index.IsIgnored(
            query.SourceType,
            query.ForbiddenReference,
            null,
            null,
            null,
            out loadedBaseline);
    }

    private static FallbackWorkEvidence MeasureFallbackWork(FallbackScenario scenario)
    {
        ArchitectureIgnoreMatcherWorkCounter work = new();
        var index = ArchitectureIgnoreMatcherIndex.Build(
            scenario.Workload.Ignores,
            workCounter: work);

        if (scenario.IndexBuildQueryNumber == 0)
        {
            foreach (FallbackQuery query in scenario.Queries)
            {
                bool match = index.IsIgnored(query.SourceType, query.ForbiddenReference, null, null, null, out _);
                Assert.That(match, Is.True);
            }

            Assert.That(work.VersionedFallbackEntriesProcessed, Is.Zero,
                "Early ordered matches should not materialize the fallback index.");
        }
        else
        {
            for (int queryIndex = 0; queryIndex < scenario.IndexBuildQueryNumber - 1; queryIndex++)
            {
                bool match = index.IsIgnored(
                    scenario.Queries[queryIndex].SourceType,
                    scenario.Queries[queryIndex].ForbiddenReference,
                    null,
                    null,
                    null,
                    out _);
                Assert.That(match, Is.False);
                Assert.That(work.VersionedFallbackEntriesProcessed, Is.Zero);
            }

            Assert.That(work.MissingIdentityEntriesScanned,
                Is.EqualTo((scenario.IndexBuildQueryNumber - 1) * scenario.Workload.Ignores.Count));
            _ = index.IsIgnored(
                scenario.Queries[scenario.IndexBuildQueryNumber - 1].SourceType,
                scenario.Queries[scenario.IndexBuildQueryNumber - 1].ForbiddenReference,
                null,
                null,
                null,
                out _);
            Assert.That(work.VersionedFallbackEntriesProcessed, Is.EqualTo(scenario.Workload.Ignores.Count));
            _ = index.IsIgnored(
                scenario.Queries[scenario.IndexBuildQueryNumber].SourceType,
                scenario.Queries[scenario.IndexBuildQueryNumber].ForbiddenReference,
                null,
                null,
                null,
                out _);
            Assert.That(work.VersionedFallbackEntriesProcessed, Is.EqualTo(scenario.Workload.Ignores.Count),
                "The fallback index should be built once and reused.");
        }

        Assert.That(work.VersionedIdentityEntriesIndexed, Is.Zero,
            "Fallback-only scenarios must not build the exact live-identity index.");
        return new FallbackWorkEvidence(
            work.VersionedIdentityEntriesIndexed,
            work.MissingIdentityEntriesScanned,
            work.VersionedFallbackEntriesProcessed);
    }

    private sealed record FallbackScenario(
        string Name,
        MatcherWorkload Workload,
        FallbackQuery[] Queries,
        int IndexBuildQueryNumber);

    private sealed record FallbackQuery(string SourceType, string ForbiddenReference);

    private sealed record FallbackMeasurement(
        double IndexBuildMilliseconds,
        double FirstQueryMilliseconds,
        double LazyFallbackIndexQueryMilliseconds,
        double SubsequentQueryMillisecondsPerCall,
        double TotalMilliseconds,
        long TotalAllocatedBytes,
        string ResultChecksum);

    private sealed record FallbackWorkEvidence(
        long VersionedIdentityEntriesIndexed,
        long MissingIdentityEntriesScanned,
        long VersionedFallbackEntriesProcessed);
}
