using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Resolution;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

public sealed partial class ArchitectureIgnoreMatcherPerformanceEvidenceTests
{
    [Test]
    [Explicit("Hardware-sensitive #1127 after-change matrix; compare with the checked-in before.json evidence.")]
    public void RunAfterMatcherEffectMatrix()
    {
        string root = new ArchitectureRepositoryRootResolver().Resolve();
        string beforePath = Path.Combine(root, "docs", "internal", "large-solution-benchmarking", "evidence", "issue-1127", "before.json");
        string afterPath = Path.Combine(root, "docs", "internal", "large-solution-benchmarking", "evidence", "issue-1127", "after.json");
        using var before = JsonDocument.Parse(File.ReadAllText(beforePath));
        string environment = $"{RuntimeInformation.FrameworkDescription}; {RuntimeInformation.OSDescription}; "
            + $"{RuntimeInformation.ProcessArchitecture}; processors={Environment.ProcessorCount}";
        var metadata = new
        {
            Evidence = "architecture-ignore-matching-after",
            Revision = CurrentRevision(root),
            BaselineRevision = before.RootElement.GetProperty("metadata").GetProperty("Revision").GetString(),
            RevisionState = "Revision records HEAD at measurement time; the implementation source SHA-256 identifies the measured matcher files.",
            ImplementationSourceSha256 = ImplementationSourceSha256(root),
            Environment = environment,
            WorkloadDefinition = "Same deterministic workload generator, fixture definitions, five-sample count, and tracker shape as before.json; canonical result hashes are checked against the baseline. This after artifact also records an SHA-256 for each serialized input; the historical before artifact predates per-input hashes.",
            PhaseShare = "100% of this focused inner workload is matcher calls; product validation phase share is not measured because the current profile does not attribute matcher time separately.",
            EndToEndBound = "A product-wide Amdahl bound is not measured. No full-validation speedup is inferred from this isolated workload.",
            RetainedMemoryMethod = "Managed-heap delta after full collections while a newly built per-contract index is held alive; excludes the input ignore list and is an estimate, not process peak working set.",
        };
        TestContext.Out.WriteLine(JsonSerializer.Serialize(metadata));

        List<object> evidence = [];
        foreach (MatcherWorkload workload in CreateWorkloads())
        {
            JsonElement baselineRow = FindBeforeRow(before.RootElement, workload.Name);
            string expectedChecksum = baselineRow.GetProperty("CanonicalResultSha256").GetString()!;
            string expectedInputChecksum = InputChecksum(workload);
            if (baselineRow.TryGetProperty("InputSha256", out JsonElement beforeInputChecksum))
            {
                Assert.That(expectedInputChecksum, Is.EqualTo(beforeInputChecksum.GetString()),
                    $"Input identity differs from the baseline for '{workload.Name}'.");
            }

            _ = RunIndexed(workload);
            var samples = new IndexedMeasurement[SampleCount];
            for (int index = 0; index < SampleCount; index++)
            {
                samples[index] = MeasureIndexed(workload);
            }

            Assert.That(samples.Select(sample => sample.ResultChecksum), Is.All.EqualTo(expectedChecksum),
                $"Indexed result differs from the pre-change ordered matcher for '{workload.Name}'.");

            ArchitectureIgnoreMatcherWorkCounter work = new();
            MatchResult countedResult = RunIndexed(workload, work);
            Assert.That(Checksum(countedResult), Is.EqualTo(expectedChecksum),
                $"Counted matcher result differs from the pre-change ordered matcher for '{workload.Name}'.");
            AssertWorkCounts(workload, work);

            long retainedIndexBytes = MeasureRetainedIndexBytes(workload);
            double beforeMilliseconds = baselineRow.GetProperty("MedianMatcherMilliseconds").GetDouble();
            var row = new
            {
                Workload = workload.Name,
                Kind = workload.Kind.ToString(),
                InputSha256 = expectedInputChecksum,
                IgnoreEntriesI = workload.Ignores.Count,
                FindingsF = workload.Findings.Length,
                RequiredMatchesK = workload.RequiredMatches,
                WildcardEntriesW = workload.WildcardCount,
                SampleCount,
                BeforeMedianMatcherMilliseconds = beforeMilliseconds,
                MedianIndexBuildMilliseconds = Median(samples.Select(sample => sample.IndexBuildMilliseconds)),
                MedianQueryMilliseconds = Median(samples.Select(sample => sample.QueryMilliseconds)),
                MedianTotalMilliseconds = Median(samples.Select(sample => sample.TotalMilliseconds)),
                MedianTotalAllocatedBytes = Median(samples.Select(sample => sample.AllocatedBytes)),
                RetainedIndexBytesEstimate = retainedIndexBytes,
                BeforeEntryEvaluations = baselineRow.GetProperty("LegacyEntryEvaluations").GetInt64(),
                BeforeFingerprintCreations = baselineRow.GetProperty("StructuredFingerprintCreations").GetInt64(),
                work.IndexEntriesProcessed,
                work.VersionedIdentityEntriesIndexed,
                work.VersionedFallbackEntriesProcessed,
                work.MissingIdentityEntriesScanned,
                work.OrderedLiveQueries,
                work.OrderedLiveEntriesScanned,
                work.OrderedLiveMatches,
                work.ExactLookupProbes,
                work.ExactCandidateMatches,
                work.LiveWaiverFingerprintsBuilt,
                work.WildcardEntriesEvaluated,
                work.MatchedOriginalIndices,
                MedianImproved = Median(samples.Select(sample => sample.TotalMilliseconds)) < beforeMilliseconds,
                CanonicalResultSha256 = expectedChecksum,
            };
            evidence.Add(row);
            TestContext.Out.WriteLine(JsonSerializer.Serialize(row));
        }

        File.WriteAllText(afterPath, JsonSerializer.Serialize(new { metadata, Workloads = evidence },
            new JsonSerializerOptions { WriteIndented = true }) + Environment.NewLine);
        TestContext.Progress.WriteLine($"Wrote after-change evidence to {Path.GetRelativePath(root, afterPath)}");
    }

    private static MatchResult RunIndexed(
        MatcherWorkload workload,
        ArchitectureIgnoreMatcherWorkCounter? workCounter = null)
    {
        ArchitectureIgnoreUsageTracker tracker = new();
        var index = ArchitectureIgnoreMatcherIndex.Build(
            workload.Ignores,
            cancellationToken: CancellationToken.None,
            workCounter: workCounter);
        bool[] ignored = new bool[workload.Findings.Length];
        bool[] loadedBaseline = new bool[workload.Findings.Length];

        for (int findingIndex = 0; findingIndex < workload.Findings.Length; findingIndex++)
        {
            ArchitectureViolationIdentity finding = workload.Findings[findingIndex];
            string? fingerprint = index.CreateLiveWaiverFingerprint(finding);
            ignored[findingIndex] = index.IsIgnored(
                finding.SourceType,
                $"Target.Type.{findingIndex}",
                tracker,
                finding,
                fingerprint,
                out loadedBaseline[findingIndex]);
        }

        return new MatchResult(ignored, loadedBaseline, tracker.MatchedIndexes.Order().ToArray());
    }

    private static IndexedMeasurement MeasureIndexed(MatcherWorkload workload)
    {
        ArchitectureIgnoreUsageTracker tracker = new();
        bool[] ignored = new bool[workload.Findings.Length];
        bool[] loadedBaseline = new bool[workload.Findings.Length];
        long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        var total = Stopwatch.StartNew();
        var indexBuild = Stopwatch.StartNew();
        var index = ArchitectureIgnoreMatcherIndex.Build(workload.Ignores);
        string? firstFindingFingerprint = workload.Findings.Length == 0
            ? null
            : index.CreateLiveWaiverFingerprint(workload.Findings[0]);
        indexBuild.Stop();
        var query = Stopwatch.StartNew();

        for (int findingIndex = 0; findingIndex < workload.Findings.Length; findingIndex++)
        {
            ArchitectureViolationIdentity finding = workload.Findings[findingIndex];
            string? fingerprint = findingIndex == 0
                ? firstFindingFingerprint
                : index.CreateLiveWaiverFingerprint(finding);
            ignored[findingIndex] = index.IsIgnored(
                finding.SourceType,
                $"Target.Type.{findingIndex}",
                tracker,
                finding,
                fingerprint,
                out loadedBaseline[findingIndex]);
        }

        query.Stop();
        total.Stop();
        long allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
        string checksum = Checksum(new MatchResult(ignored, loadedBaseline, tracker.MatchedIndexes.Order().ToArray()));
        return new IndexedMeasurement(indexBuild.Elapsed.TotalMilliseconds, query.Elapsed.TotalMilliseconds,
            total.Elapsed.TotalMilliseconds, allocatedBytes, checksum);
    }

    private static long MeasureRetainedIndexBytes(MatcherWorkload workload)
    {
        WarmLiveIdentityIndex(workload);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        long before = GC.GetTotalMemory(forceFullCollection: true);
        var index = ArchitectureIgnoreMatcherIndex.Build(workload.Ignores);
        MaterializeLiveIdentityIndex(workload, index);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        long after = GC.GetTotalMemory(forceFullCollection: true);
        GC.KeepAlive(index);
        return Math.Max(0, after - before);
    }

    private static void WarmLiveIdentityIndex(MatcherWorkload workload)
    {
        var index = ArchitectureIgnoreMatcherIndex.Build(workload.Ignores);
        MaterializeLiveIdentityIndex(workload, index);
    }

    private static void MaterializeLiveIdentityIndex(
        MatcherWorkload workload,
        ArchitectureIgnoreMatcherIndex index)
    {
        var tracker = new ArchitectureIgnoreUsageTracker();
        for (int findingIndex = 0; findingIndex < workload.Findings.Length; findingIndex++)
        {
            ArchitectureViolationIdentity finding = workload.Findings[findingIndex];
            string? fingerprint = index.CreateLiveWaiverFingerprint(finding);
            _ = index.IsIgnored(
                finding.SourceType,
                $"Target.Type.{findingIndex}",
                tracker,
                finding,
                fingerprint,
                out _);
        }
    }

    private static void AssertWorkCounts(MatcherWorkload workload, ArchitectureIgnoreMatcherWorkCounter work)
    {
        bool builtLiveIndex = work.IndexEntriesProcessed > 0;
        int expectedIndexedEntries = builtLiveIndex ? workload.Ignores.Count : 0;
        Assert.Multiple(() =>
        {
            Assert.That(work.IndexEntriesProcessed, Is.EqualTo(expectedIndexedEntries),
                $"Only indexed workloads should process entries while building the exact index for '{workload.Name}'.");
            int expectedVersionedIdentityEntries = builtLiveIndex && workload.Kind == MatcherKind.VersionedIdentity
                ? workload.Ignores.Count - workload.WildcardCount
                : 0;
            Assert.That(work.VersionedIdentityEntriesIndexed, Is.EqualTo(expectedVersionedIdentityEntries),
                $"Live-identity workloads must build the exact identity index only when queried for '{workload.Name}'.");
            Assert.That(work.VersionedFallbackEntriesProcessed, Is.Zero,
                $"Live-identity workloads must not build the deferred versioned fallback for '{workload.Name}'.");
            Assert.That(work.MissingIdentityEntriesScanned, Is.Zero,
                $"Live-identity workloads must not scan the missing-identity fallback for '{workload.Name}'.");
            Assert.That(work.ExactLookupProbes, Is.LessThanOrEqualTo(workload.Findings.Length * 3),
                $"Exact dictionary probes must remain constant per finding for '{workload.Name}'.");
            Assert.That(work.ExactLookupProbes + work.OrderedLiveQueries, Is.EqualTo(workload.Findings.Length),
                $"Each finding must use either an ordered scan or the prepared index for '{workload.Name}'.");
            Assert.That(work.ExactCandidateMatches + work.OrderedLiveMatches, Is.EqualTo(workload.RequiredMatches),
                $"Indexed and ordered exact matches must preserve required cardinality for '{workload.Name}'.");
            Assert.That(work.LiveWaiverFingerprintsBuilt,
                Is.EqualTo(workload.Kind == MatcherKind.StructuredWaiver ? workload.Findings.Length : 0),
                $"Structured waiver fingerprints must be built at most once per applicable finding for '{workload.Name}'.");
            Assert.That(work.WildcardEntriesEvaluated,
                Is.EqualTo(workload.WildcardCount * workload.Findings.Length),
                $"Wildcard candidates must remain separately attributable for '{workload.Name}'.");
            Assert.That(work.MatchedOriginalIndices, Is.EqualTo(workload.RequiredMatches),
                $"Every exact match index must be reported for '{workload.Name}'.");
        });
    }

    private static JsonElement FindBeforeRow(JsonElement before, string workloadName)
    {
        return before.GetProperty("Workloads").EnumerateArray()
            .Single(row => row.GetProperty("Workload").GetString() == workloadName);
    }

    private static string ImplementationSourceSha256(string root)
    {
        string[] relativePaths =
        [
            "src/ArchLinterNet.Core/Resolution/ArchitectureIgnoreMatcher.cs",
            "src/ArchLinterNet.Core/Resolution/ArchitectureIgnoreMatcherIndex.cs",
            "src/ArchLinterNet.Core/Resolution/ArchitectureIgnoreMatcherOrdered.cs",
            "src/ArchLinterNet.Core/Resolution/ArchitectureContractExecutionContext.cs",
            "src/ArchLinterNet.Core/Execution/ArchitectureAnalysisSession.cs",
        ];
        StringBuilder canonical = new();
        foreach (string relativePath in relativePaths)
        {
            string path = Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
            canonical.Append(relativePath).Append('\0')
                .Append(Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)))).Append('\n');
        }

        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString())));
    }

    private sealed record IndexedMeasurement(
        double IndexBuildMilliseconds,
        double QueryMilliseconds,
        double TotalMilliseconds,
        long AllocatedBytes,
        string ResultChecksum);
}
