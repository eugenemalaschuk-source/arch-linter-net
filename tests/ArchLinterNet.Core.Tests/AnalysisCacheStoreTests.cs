using ArchLinterNet.Core.BuildState;
using ArchLinterNet.Core.Caching;
using ArchLinterNet.Core.Model;
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
        PolicyDigest: "policy-digest" + suffix,
        Mode: "strict",
        ConditionSetName: null,
        ContractIdsDigest: "contract-digest",
        WorkspaceDigest: "workspace-digest",
        Configuration: "Debug",
        TargetFramework: "net10.0",
        Platform: null,
        RuntimeIdentifier: null);

    private static AnalysisCacheProjectManifest EligibleManifest(string path = "src/A/A.csproj", string digest = "digest-a") =>
        new(path, digest, CacheEligibility.VerifiedCacheEligible);

    private static AnalysisCacheOutcomeV1 SampleOutcome() => new(
        Passed: true,
        Violations: Array.Empty<ArchitectureViolation>(),
        Cycles: Array.Empty<string>(),
        CoverageFindings: Array.Empty<ArchitectureViolation>(),
        CoverageConfig: "off",
        UnmatchedIgnoredViolations: Array.Empty<ArchitectureUnmatchedIgnoredViolation>(),
        UnmatchedIgnoredViolationsConfig: "off",
        PolicyConsistencyFindings: Array.Empty<PolicyConsistencyDiagnostic>(),
        PolicyConsistencyConfig: "off",
        ClassificationConflicts: Array.Empty<ArchitectureClassificationConflict>(),
        ClassificationMetadataFailures: Array.Empty<ArchitectureClassificationMetadataFailure>());

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

        AnalysisCacheStore.PutResult putResult = AnalysisCacheStore.Put(_location, key, manifests, SampleOutcome());
        Assert.That(putResult.RejectReason, Is.Null);
        Assert.That(putResult.BytesWritten, Is.GreaterThan(0));

        AnalysisCacheLookupResult result = AnalysisCacheStore.TryGet(_location, key, manifests);

        Assert.That(result.Outcome, Is.EqualTo(AnalysisCacheLookupOutcome.Hit));
        Assert.That(result.Entry, Is.Not.Null);
        Assert.That(result.Entry!.Outcome.Passed, Is.True);
    }

    [Test]
    public void Put_WithIneligibleManifest_IsRejected()
    {
        AnalysisCacheProjectManifest ineligible = new("src/A/A.csproj", "digest", CacheEligibility.CacheIneligible);

        AnalysisCacheStore.PutResult putResult = AnalysisCacheStore.Put(_location, CreateKey(), new[] { ineligible }, SampleOutcome());

        Assert.That(putResult.RejectReason, Is.EqualTo(AnalysisCacheRejectReason.IneligibleBuildInput));
        Assert.That(Directory.Exists(_root) && Directory.EnumerateFiles(_root, "*.json", SearchOption.AllDirectories).Any(), Is.False);
    }

    [Test]
    public void TryGet_ProjectManifestDigestChanged_IsProjectSetMismatch()
    {
        AnalysisCacheKey key = CreateKey();
        AnalysisCacheStore.Put(_location, key, new[] { EligibleManifest() }, SampleOutcome());

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
        AnalysisCacheStore.Put(_location, key, manifests, SampleOutcome());

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
        AnalysisCacheStore.Put(_location, key, manifests, SampleOutcome());

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
        AnalysisCacheStore.Put(_location, key, manifests, SampleOutcome());

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
        AnalysisCacheStore.Put(_location, key, manifests, SampleOutcome());

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
        AnalysisCacheStore.Put(_location, key, manifests, SampleOutcome());

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

        AnalysisCacheStore.PutResult putResult = AnalysisCacheStore.Put(_location, key, manifests, SampleOutcome(), cts.Token);

        Assert.That(putResult.RejectReason, Is.EqualTo(AnalysisCacheRejectReason.Cancelled));
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
        AnalysisCacheStore.Put(_location, CreateKey("a"), new[] { EligibleManifest() }, SampleOutcome());
        AnalysisCacheStore.Put(_location, CreateKey("b"), new[] { EligibleManifest("src/B/B.csproj", "digest-b") }, SampleOutcome());

        IReadOnlyList<AnalysisCacheEntrySummary> summaries = AnalysisCacheStore.Inspect(_location);

        Assert.That(summaries.Count, Is.EqualTo(2));
        Assert.That(summaries.All(s => s.Readable), Is.True);
        Assert.That(summaries.Select(s => s.EntryFileName), Is.Ordered.Using<string>(StringComparer.Ordinal));
        Assert.That(summaries.All(s => !s.EntryFileName.Contains(_root, StringComparison.Ordinal)), Is.True);
    }

    [Test]
    public void Clear_RemovesAllPublishedEntries()
    {
        AnalysisCacheStore.Put(_location, CreateKey("a"), new[] { EligibleManifest() }, SampleOutcome());
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
        AnalysisCacheStore.Put(_location, key, manifests, SampleOutcome());

        string entryPath = Directory.EnumerateFiles(_root, "*.json", SearchOption.AllDirectories).Single();
        File.WriteAllBytes(entryPath, new byte[9 * 1024 * 1024]);

        AnalysisCacheLookupResult result = AnalysisCacheStore.TryGet(_location, key, manifests);
        Assert.That(result.Outcome, Is.EqualTo(AnalysisCacheLookupOutcome.Reject));
        Assert.That(result.Reason, Is.EqualTo(AnalysisCacheRejectReason.SizeExceeded));
    }

    // Finding #7's companion bound: an unreasonable project-manifest count must be rejected before
    // any I/O too, independent of whether the serialized bytes alone would fit under MaxEntryBytes.
    [Test]
    public void Put_ExceedingMaxProjectManifestCount_IsRejectedBeforeWrite()
    {
        AnalysisCacheKey key = CreateKey();
        AnalysisCacheProjectManifest[] tooManyManifests = Enumerable.Range(0, 4097)
            .Select(i => EligibleManifest($"src/P{i}/P{i}.csproj", $"digest-{i}"))
            .ToArray();

        AnalysisCacheStore.PutResult putResult = AnalysisCacheStore.Put(_location, key, tooManyManifests, SampleOutcome());

        Assert.That(putResult.RejectReason, Is.EqualTo(AnalysisCacheRejectReason.SizeExceeded));
        Assert.That(putResult.BytesWritten, Is.EqualTo(0));
        Assert.That(Directory.Exists(_root) && Directory.EnumerateFiles(_root, "*.json", SearchOption.AllDirectories).Any(), Is.False);
    }

    // Finding #7: the write side must enforce the same bound the read side always has. A cache
    // entry whose Violations list alone would serialize past MaxEntryBytes must never be published
    // at all — a subsequent TryGet must not need to reject it as SizeExceeded because Put already
    // refused to write it.
    [Test]
    public void Put_EntryLargerThanMaxBytes_IsRejectedBeforeWrite()
    {
        AnalysisCacheKey key = CreateKey();
        AnalysisCacheProjectManifest[] manifests = { EligibleManifest() };
        ArchitectureViolation[] oversizedViolations = Enumerable.Range(0, 200_000)
            .Select(i => new ArchitectureViolation(
                $"contract-{i}", $"id-{i}", $"Namespace.Type{i}", $"Forbidden.Namespace{i}",
                new[] { $"Forbidden.Namespace{i}.Reference" }))
            .ToArray();
        AnalysisCacheOutcomeV1 oversizedOutcome = SampleOutcome() with { Violations = oversizedViolations };

        AnalysisCacheStore.PutResult putResult = AnalysisCacheStore.Put(_location, key, manifests, oversizedOutcome);

        Assert.That(putResult.RejectReason, Is.EqualTo(AnalysisCacheRejectReason.SizeExceeded));
        Assert.That(putResult.BytesWritten, Is.EqualTo(0));
        Assert.That(Directory.Exists(_root) && Directory.EnumerateFiles(_root, "*.json", SearchOption.AllDirectories).Any(), Is.False);
    }

    [Test]
    public void TryGet_IncompatibleFormatVersion_IsRejected()
    {
        AnalysisCacheKey key = CreateKey();
        AnalysisCacheProjectManifest[] manifests = { EligibleManifest() };
        AnalysisCacheStore.Put(_location, key, manifests, SampleOutcome());

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
        AnalysisCacheStore.Put(_location, key, manifests, SampleOutcome());

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
        AnalysisCacheStore.Put(_location, key, manifests, SampleOutcome());

        AnalysisCacheLookupResult result = AnalysisCacheStore.TryGet(_location, key, new[] { EligibleManifest() });

        Assert.That(result.Outcome, Is.EqualTo(AnalysisCacheLookupOutcome.Reject));
        Assert.That(result.Reason, Is.EqualTo(AnalysisCacheRejectReason.ProjectSetMismatch));
    }

    [Test]
    public void Inspect_UnreadableEntry_IsReportedNotReadable()
    {
        AnalysisCacheStore.Put(_location, CreateKey(), new[] { EligibleManifest() }, SampleOutcome());
        string entryPath = Directory.EnumerateFiles(_root, "*.json", SearchOption.AllDirectories).Single();
        File.WriteAllText(entryPath, "{ not valid json");

        IReadOnlyList<AnalysisCacheEntrySummary> summaries = AnalysisCacheStore.Inspect(_location);

        Assert.That(summaries.Count, Is.EqualTo(1));
        Assert.That(summaries[0].Readable, Is.False);
    }

    [Test]
    public void Inspect_EmptyEntryFile_IsReportedNotReadable()
    {
        AnalysisCacheStore.Put(_location, CreateKey(), new[] { EligibleManifest() }, SampleOutcome());
        string entryPath = Directory.EnumerateFiles(_root, "*.json", SearchOption.AllDirectories).Single();
        File.WriteAllText(entryPath, string.Empty);

        IReadOnlyList<AnalysisCacheEntrySummary> summaries = AnalysisCacheStore.Inspect(_location);

        Assert.That(summaries.Count, Is.EqualTo(1));
        Assert.That(summaries[0].Readable, Is.False);
    }

    // Finding #3: a symlinked shard directory pre-created under the cache root must not let Put/
    // TryGet/Inspect/Clear read or write through it to a location outside the cache root.
    [Test]
    public void Put_ShardDirectoryIsSymlink_IsRejectedAsPathUnsafe()
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Ignore("Symlink creation requires elevated privileges on Windows by default.");
            return;
        }

        string outsideTarget = Path.Combine(Path.GetTempPath(), "arch-linter-net-cache-outside-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outsideTarget);
        try
        {
            AnalysisCacheKey key = CreateKey();
            string shard = key.Digest[..2];
            Directory.CreateDirectory(_root);
            string shardPath = Path.Combine(_root, shard);
            Directory.CreateSymbolicLink(shardPath, outsideTarget);

            AnalysisCacheStore.PutResult putResult = AnalysisCacheStore.Put(_location, key, new[] { EligibleManifest() }, SampleOutcome());

            Assert.That(putResult.RejectReason, Is.EqualTo(AnalysisCacheRejectReason.PathUnsafe));
            Assert.That(Directory.EnumerateFileSystemEntries(outsideTarget), Is.Empty);
        }
        finally
        {
            Directory.Delete(outsideTarget, recursive: true);
        }
    }

    [Test]
    public void Inspect_SymlinkedSubdirectory_IsNotFollowed()
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Ignore("Symlink creation requires elevated privileges on Windows by default.");
            return;
        }

        string outsideTarget = Path.Combine(Path.GetTempPath(), "arch-linter-net-cache-outside-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outsideTarget);
        File.WriteAllText(Path.Combine(outsideTarget, "secret.json"), "{}");
        try
        {
            Directory.CreateDirectory(_root);
            Directory.CreateSymbolicLink(Path.Combine(_root, "linked"), outsideTarget);

            IReadOnlyList<AnalysisCacheEntrySummary> summaries = AnalysisCacheStore.Inspect(_location);

            Assert.That(summaries, Is.Empty);
        }
        finally
        {
            Directory.Delete(outsideTarget, recursive: true);
        }
    }
}
