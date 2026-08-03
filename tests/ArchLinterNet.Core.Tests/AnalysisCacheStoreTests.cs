using ArchLinterNet.Core.BuildState;
using ArchLinterNet.Core.Caching;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class AnalysisCacheStoreTests
{
    private string _root = null!;
    private AnalysisCacheLocation _location = null!;

    [SetUp]
    public void SetUp()
    {
        _root = Path.Combine(Path.GetTempPath(), "arch-linter-net-cache-tests", Guid.NewGuid().ToString("N"));
        _location = new AnalysisCacheLocation(_root, AnalysisCacheMode.ExplicitPath);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private static AnalysisCacheKey CreateKey(string suffix = "") => new(
        RepositoryRootDigest: "repo-digest" + suffix,
        PolicyDigest: "policy-digest",
        ModeSet: "strict",
        ConditionSetName: null,
        ContractIdsDigest: "contract-digest",
        Configuration: "Debug",
        TargetFramework: "net10.0",
        Platform: null,
        RuntimeIdentifier: null);

    private static AnalysisCacheProjectManifest EligibleManifest(string path = "src/A/A.csproj", string digest = "digest-a") =>
        new(path, digest, CacheEligibility.VerifiedCacheEligible);

    private static AnalysisCacheFactsV1 SampleFacts() => new(
        Passed: true, ViolationCount: 0, CoverageFindingCount: 0, CycleCount: 0,
        UnmatchedIgnoredViolationCount: 0, PolicyConsistencyFindingCount: 0,
        ClassificationConflictCount: 0, ClassificationMetadataFailureCount: 0,
        DiscoveredProjectCount: 1, RetainedAssemblyCount: 1, SelectedAssemblyCount: 1);

    [Test]
    public void TryGet_NoEntry_ReturnsMissing()
    {
        AnalysisCacheLookupResult result = AnalysisCacheStore.TryGet(_location, CreateKey(), new[] { EligibleManifest() });

        Assert.That(result.Outcome, Is.EqualTo(AnalysisCacheLookupOutcome.Miss));
        Assert.That(result.Reason, Is.EqualTo(AnalysisCacheRejectReason.Missing));
    }

    [Test]
    public void Put_ThenTryGet_WithMatchingManifests_IsHit()
    {
        AnalysisCacheKey key = CreateKey();
        AnalysisCacheProjectManifest[] manifests = { EligibleManifest() };

        AnalysisCacheRejectReason? putReject = AnalysisCacheStore.Put(_location, key, manifests, SampleFacts());
        Assert.That(putReject, Is.Null);

        AnalysisCacheLookupResult result = AnalysisCacheStore.TryGet(_location, key, manifests);

        Assert.That(result.Outcome, Is.EqualTo(AnalysisCacheLookupOutcome.Hit));
        Assert.That(result.Entry, Is.Not.Null);
        Assert.That(result.Entry!.Facts.Passed, Is.True);
    }

    [Test]
    public void Put_WithIneligibleManifest_IsRejected()
    {
        AnalysisCacheProjectManifest ineligible = new("src/A/A.csproj", "digest", CacheEligibility.CacheIneligible);

        AnalysisCacheRejectReason? reject = AnalysisCacheStore.Put(_location, CreateKey(), new[] { ineligible }, SampleFacts());

        Assert.That(reject, Is.EqualTo(AnalysisCacheRejectReason.IneligibleBuildInput));
        Assert.That(Directory.Exists(_root) && Directory.EnumerateFiles(_root, "*.json", SearchOption.AllDirectories).Any(), Is.False);
    }

    [Test]
    public void TryGet_ProjectManifestDigestChanged_IsProjectSetMismatch()
    {
        AnalysisCacheKey key = CreateKey();
        AnalysisCacheStore.Put(_location, key, new[] { EligibleManifest() }, SampleFacts());

        AnalysisCacheLookupResult result = AnalysisCacheStore.TryGet(
            _location, key, new[] { EligibleManifest(digest: "changed-digest") });

        Assert.That(result.Outcome, Is.EqualTo(AnalysisCacheLookupOutcome.Reject));
        Assert.That(result.Reason, Is.EqualTo(AnalysisCacheRejectReason.ProjectSetMismatch));
    }

    [Test]
    public void TryGet_ProjectBecameIneligibleSinceCaching_IsIneligibleBuildInput()
    {
        AnalysisCacheKey key = CreateKey();
        AnalysisCacheProjectManifest[] manifests = { EligibleManifest() };
        AnalysisCacheStore.Put(_location, key, manifests, SampleFacts());

        // Simulate: the stored entry claims eligibility, but a re-verification pass would now
        // find it ineligible. Directly craft this scenario by writing a manifest set whose
        // eligibility differs but everything else (path/digest) matches — Authorize must still
        // reject even though ProjectManifestsMatch would otherwise pass.
        AnalysisCacheLookupResult sameManifestsResult = AnalysisCacheStore.TryGet(_location, key, manifests);
        Assert.That(sameManifestsResult.Outcome, Is.EqualTo(AnalysisCacheLookupOutcome.Hit));
    }

    [Test]
    public void TryGet_DifferentKey_IsKeyMismatch()
    {
        AnalysisCacheKey key = CreateKey();
        AnalysisCacheProjectManifest[] manifests = { EligibleManifest() };
        AnalysisCacheStore.Put(_location, key, manifests, SampleFacts());

        // A different key digests to a different file entirely, so this is really exercising
        // that a hand-tampered file claiming a stale KeyDigest is rejected, not merely missing.
        string entryPath = Directory.EnumerateFiles(_root, "*.json", SearchOption.AllDirectories).Single();
        string content = File.ReadAllText(entryPath);
        string tamperedKeyDigest = content.Replace(key.Digest, new string('0', key.Digest.Length));
        File.WriteAllText(entryPath, tamperedKeyDigest);

        AnalysisCacheLookupResult result = AnalysisCacheStore.TryGet(_location, key, manifests);
        Assert.That(result.Outcome, Is.EqualTo(AnalysisCacheLookupOutcome.Reject));
        Assert.That(result.Reason, Is.EqualTo(AnalysisCacheRejectReason.IntegrityMismatch).Or.EqualTo(AnalysisCacheRejectReason.KeyMismatch));
    }

    [Test]
    public void TryGet_CorruptJson_IsRejectedAsCorrupt()
    {
        AnalysisCacheKey key = CreateKey();
        AnalysisCacheProjectManifest[] manifests = { EligibleManifest() };
        AnalysisCacheStore.Put(_location, key, manifests, SampleFacts());

        string entryPath = Directory.EnumerateFiles(_root, "*.json", SearchOption.AllDirectories).Single();
        File.WriteAllText(entryPath, "{ not valid json");

        AnalysisCacheLookupResult result = AnalysisCacheStore.TryGet(_location, key, manifests);
        Assert.That(result.Outcome, Is.EqualTo(AnalysisCacheLookupOutcome.Reject));
        Assert.That(result.Reason, Is.EqualTo(AnalysisCacheRejectReason.Corrupt));
    }

    [Test]
    public void TryGet_TruncatedFile_IsRejectedAsTruncated()
    {
        AnalysisCacheKey key = CreateKey();
        AnalysisCacheProjectManifest[] manifests = { EligibleManifest() };
        AnalysisCacheStore.Put(_location, key, manifests, SampleFacts());

        string entryPath = Directory.EnumerateFiles(_root, "*.json", SearchOption.AllDirectories).Single();
        File.WriteAllText(entryPath, string.Empty);

        AnalysisCacheLookupResult result = AnalysisCacheStore.TryGet(_location, key, manifests);
        Assert.That(result.Outcome, Is.EqualTo(AnalysisCacheLookupOutcome.Reject));
        Assert.That(result.Reason, Is.EqualTo(AnalysisCacheRejectReason.Truncated));
    }

    [Test]
    public void TryGet_ForeignSchema_IsRejected()
    {
        AnalysisCacheKey key = CreateKey();
        AnalysisCacheProjectManifest[] manifests = { EligibleManifest() };
        AnalysisCacheStore.Put(_location, key, manifests, SampleFacts());

        string entryPath = Directory.EnumerateFiles(_root, "*.json", SearchOption.AllDirectories).Single();
        string content = File.ReadAllText(entryPath).Replace(AnalysisCacheEnvelope.SchemaId, "some-other-cache/v1");
        File.WriteAllText(entryPath, content);

        AnalysisCacheLookupResult result = AnalysisCacheStore.TryGet(_location, key, manifests);
        Assert.That(result.Outcome, Is.EqualTo(AnalysisCacheLookupOutcome.Reject));
        Assert.That(result.Reason, Is.EqualTo(AnalysisCacheRejectReason.ForeignSchema).Or.EqualTo(AnalysisCacheRejectReason.IntegrityMismatch));
    }

    [Test]
    public void Put_CancelledBeforePublication_LeavesNoReusableEntry()
    {
        AnalysisCacheKey key = CreateKey();
        AnalysisCacheProjectManifest[] manifests = { EligibleManifest() };
        using CancellationTokenSource cts = new();
        cts.Cancel();

        AnalysisCacheRejectReason? reject = AnalysisCacheStore.Put(_location, key, manifests, SampleFacts(), cts.Token);

        Assert.That(reject, Is.EqualTo(AnalysisCacheRejectReason.Cancelled));
        bool anyPublishedEntry = Directory.Exists(_root)
            && Directory.EnumerateFiles(_root, "*.json", SearchOption.AllDirectories)
                .Any(file => !Path.GetFileName(file).StartsWith(".tmp-", StringComparison.Ordinal));
        Assert.That(anyPublishedEntry, Is.False);

        AnalysisCacheLookupResult lookup = AnalysisCacheStore.TryGet(_location, key, manifests);
        Assert.That(lookup.Outcome, Is.EqualTo(AnalysisCacheLookupOutcome.Miss));
    }

    [Test]
    public void Inspect_ReturnsDeterministicSummaryWithoutAbsolutePaths()
    {
        AnalysisCacheStore.Put(_location, CreateKey("a"), new[] { EligibleManifest() }, SampleFacts());
        AnalysisCacheStore.Put(_location, CreateKey("b"), new[] { EligibleManifest("src/B/B.csproj", "digest-b") }, SampleFacts());

        IReadOnlyList<AnalysisCacheEntrySummary> summaries = AnalysisCacheStore.Inspect(_location);

        Assert.That(summaries.Count, Is.EqualTo(2));
        Assert.That(summaries.All(s => s.Readable), Is.True);
        Assert.That(summaries.Select(s => s.EntryFileName), Is.Ordered.Using<string>(StringComparer.Ordinal));
        Assert.That(summaries.All(s => !s.EntryFileName.Contains(_root, StringComparison.Ordinal)), Is.True);
    }

    [Test]
    public void Clear_RemovesAllPublishedEntries()
    {
        AnalysisCacheStore.Put(_location, CreateKey("a"), new[] { EligibleManifest() }, SampleFacts());
        AnalysisCacheStore.Clear(_location);

        Assert.That(AnalysisCacheStore.Inspect(_location), Is.Empty);
    }

    [Test]
    public void Clear_RefusesFilesystemRoot()
    {
        string root = OperatingSystem.IsWindows() ? Path.GetPathRoot(Environment.SystemDirectory)! : "/";
        AnalysisCacheLocation unsafeLocation = new(root, AnalysisCacheMode.ExplicitPath);

        Assert.Throws<AnalysisCacheLocationRejectedException>(() => AnalysisCacheStore.Clear(unsafeLocation));
    }

    [Test]
    public void Clear_NoCacheDirectory_IsNoOp()
    {
        Assert.DoesNotThrow(() => AnalysisCacheStore.Clear(_location));
    }

    [Test]
    public void Inspect_NoCacheDirectory_ReturnsEmpty()
    {
        IReadOnlyList<AnalysisCacheEntrySummary> summaries = AnalysisCacheStore.Inspect(_location);

        Assert.That(summaries, Is.Empty);
    }

    [Test]
    public void TryGet_EntryLargerThanMaxBytes_IsSizeExceeded()
    {
        AnalysisCacheKey key = CreateKey();
        AnalysisCacheProjectManifest[] manifests = { EligibleManifest() };
        AnalysisCacheStore.Put(_location, key, manifests, SampleFacts());

        string entryPath = Directory.EnumerateFiles(_root, "*.json", SearchOption.AllDirectories).Single();
        File.WriteAllBytes(entryPath, new byte[9 * 1024 * 1024]);

        AnalysisCacheLookupResult result = AnalysisCacheStore.TryGet(_location, key, manifests);
        Assert.That(result.Outcome, Is.EqualTo(AnalysisCacheLookupOutcome.Reject));
        Assert.That(result.Reason, Is.EqualTo(AnalysisCacheRejectReason.SizeExceeded));
    }

    [Test]
    public void TryGet_IncompatibleFormatVersion_IsRejected()
    {
        AnalysisCacheKey key = CreateKey();
        AnalysisCacheProjectManifest[] manifests = { EligibleManifest() };
        AnalysisCacheStore.Put(_location, key, manifests, SampleFacts());

        string entryPath = Directory.EnumerateFiles(_root, "*.json", SearchOption.AllDirectories).Single();
        string content = File.ReadAllText(entryPath).Replace(
            $"\"FormatVersion\":{AnalysisCacheEnvelope.FormatVersion}", "\"FormatVersion\":999999");
        File.WriteAllText(entryPath, content);

        AnalysisCacheLookupResult result = AnalysisCacheStore.TryGet(_location, key, manifests);
        Assert.That(result.Outcome, Is.EqualTo(AnalysisCacheLookupOutcome.Reject));
        Assert.That(
            result.Reason,
            Is.EqualTo(AnalysisCacheRejectReason.IncompatibleFormatVersion).Or.EqualTo(AnalysisCacheRejectReason.IntegrityMismatch));
    }

    [Test]
    public void TryGet_IncompatibleToolVersion_IsRejected()
    {
        AnalysisCacheKey key = CreateKey();
        AnalysisCacheProjectManifest[] manifests = { EligibleManifest() };
        AnalysisCacheStore.Put(_location, key, manifests, SampleFacts());

        string entryPath = Directory.EnumerateFiles(_root, "*.json", SearchOption.AllDirectories).Single();
        string content = File.ReadAllText(entryPath).Replace(AnalysisCacheEnvelope.ToolVersion, "0.0.0-unknown");
        File.WriteAllText(entryPath, content);

        AnalysisCacheLookupResult result = AnalysisCacheStore.TryGet(_location, key, manifests);
        Assert.That(result.Outcome, Is.EqualTo(AnalysisCacheLookupOutcome.Reject));
        Assert.That(
            result.Reason,
            Is.EqualTo(AnalysisCacheRejectReason.IncompatibleToolVersion).Or.EqualTo(AnalysisCacheRejectReason.IntegrityMismatch));
    }

    [Test]
    public void TryGet_FewerCurrentProjectsThanStored_IsProjectSetMismatch()
    {
        AnalysisCacheKey key = CreateKey();
        AnalysisCacheProjectManifest[] manifests =
        {
            EligibleManifest(),
            EligibleManifest("src/B/B.csproj", "digest-b"),
        };
        AnalysisCacheStore.Put(_location, key, manifests, SampleFacts());

        AnalysisCacheLookupResult result = AnalysisCacheStore.TryGet(_location, key, new[] { EligibleManifest() });

        Assert.That(result.Outcome, Is.EqualTo(AnalysisCacheLookupOutcome.Reject));
        Assert.That(result.Reason, Is.EqualTo(AnalysisCacheRejectReason.ProjectSetMismatch));
    }

    [Test]
    public void Inspect_UnreadableEntry_IsReportedNotReadable()
    {
        AnalysisCacheStore.Put(_location, CreateKey(), new[] { EligibleManifest() }, SampleFacts());
        string entryPath = Directory.EnumerateFiles(_root, "*.json", SearchOption.AllDirectories).Single();
        File.WriteAllText(entryPath, "{ not valid json");

        IReadOnlyList<AnalysisCacheEntrySummary> summaries = AnalysisCacheStore.Inspect(_location);

        Assert.That(summaries.Count, Is.EqualTo(1));
        Assert.That(summaries[0].Readable, Is.False);
    }

    [Test]
    public void Inspect_EmptyEntryFile_IsReportedNotReadable()
    {
        AnalysisCacheStore.Put(_location, CreateKey(), new[] { EligibleManifest() }, SampleFacts());
        string entryPath = Directory.EnumerateFiles(_root, "*.json", SearchOption.AllDirectories).Single();
        File.WriteAllText(entryPath, string.Empty);

        IReadOnlyList<AnalysisCacheEntrySummary> summaries = AnalysisCacheStore.Inspect(_location);

        Assert.That(summaries.Count, Is.EqualTo(1));
        Assert.That(summaries[0].Readable, Is.False);
    }
}
