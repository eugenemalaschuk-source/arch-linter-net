using System.Text.Json.Nodes;
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

    private static void DeleteDirectoryLink(string path)
    {
        try
        {
            // Directory.CreateSymbolicLink creates a directory link. DirectoryInfo.Delete removes
            // that link itself without recursively following its target, unlike the File.Delete
            // cleanup that throws on Linux/macOS for this shape.
            new DirectoryInfo(path).Delete();
        }
        catch (DirectoryNotFoundException)
        {
            // A rejected cache operation must not remove the link, but this keeps cleanup safe if
            // a platform has already removed the link while preserving the target assertion.
        }
    }

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
    public void TryGet_ArtifactFingerprintChanged_IsArtifactSetMismatch()
    {
        AnalysisCacheKey key = CreateKey();
        AnalysisCacheProjectManifest[] manifests = { EligibleManifest() };
        AnalysisCacheArtifactManifest[] originalArtifacts =
        {
            new("src/A/bin/Debug/net10.0/A.dll", "pe-before"),
            new("src/A/bin/Debug/net10.0/A.pdb", "pdb-before"),
            new("src/A/bin/Debug/net10.0/A.dll.archlinternet-receipt.json", "receipt-before"),
        };

        AnalysisCacheStore.PutResult put = AnalysisCacheStore.Put(_location, key, manifests, originalArtifacts, SampleOutcome());
        Assert.That(put.RejectReason, Is.Null);

        AnalysisCacheArtifactManifest[] changedArtifacts = originalArtifacts
            .Select(manifest => manifest.ArtifactPath.EndsWith(".dll", StringComparison.Ordinal)
                ? manifest with { ContentDigest = "pe-after" }
                : manifest)
            .ToArray();
        AnalysisCacheLookupResult result = AnalysisCacheStore.TryGet(_location, key, manifests, changedArtifacts);

        Assert.That(result.Outcome, Is.EqualTo(AnalysisCacheLookupOutcome.Reject));
        Assert.That(result.Reason, Is.EqualTo(AnalysisCacheRejectReason.ArtifactSetMismatch));
    }

    [Test]
    public void ArtifactManifest_ArtifactBytesChange_ChangesTheContentDigest()
    {
        string artifactPath = Path.Combine(_root, "Sample.dll");
        Directory.CreateDirectory(_root);
        File.WriteAllBytes(artifactPath, new byte[] { 1, 2, 3 });

        AnalysisCacheArtifactManifest before = AnalysisCacheArtifactManifest.FromPath(artifactPath, _root);
        File.WriteAllBytes(artifactPath, new byte[] { 1, 2, 4 });
        AnalysisCacheArtifactManifest after = AnalysisCacheArtifactManifest.FromPath(artifactPath, _root);

        Assert.Multiple(() =>
        {
            Assert.That(after.ArtifactPath, Is.EqualTo(before.ArtifactPath));
            Assert.That(after.ContentDigest, Is.Not.EqualTo(before.ContentDigest));
        });
    }

    [Test]
    public void TryGet_SyntacticallyValidEntryWithNullOutcome_IsTypedCorruptReject()
    {
        AnalysisCacheKey key = CreateKey();
        AnalysisCacheProjectManifest[] manifests = { EligibleManifest() };
        AnalysisCacheStore.Put(_location, key, manifests, SampleOutcome());

        string entryPath = Directory.EnumerateFiles(_root, "*.json", SearchOption.AllDirectories).Single();
        JsonObject entry = JsonNode.Parse(File.ReadAllText(entryPath))!.AsObject();
        entry["Outcome"] = null;
        File.WriteAllText(entryPath, entry.ToJsonString());

        Assert.DoesNotThrow(() =>
        {
            AnalysisCacheLookupResult result = AnalysisCacheStore.TryGet(_location, key, manifests);
            Assert.That(result.Outcome, Is.EqualTo(AnalysisCacheLookupOutcome.Reject));
            Assert.That(result.Reason, Is.EqualTo(AnalysisCacheRejectReason.Corrupt));
        });
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

    // Finding #1: HMAC authentication. A hand-tampered entry (flipping Passed from false to true,
    // without knowing this cache root's real AnalysisCacheHmacKeyStore secret) can no longer forge
    // a matching tag — this is exactly the "poisoned CI cache" scenario the review named: "store
    // Passed = true with empty findings, recompute the digest, pass Authorize, and make Evaluate
    // skip all contracts". It must now be rejected as IntegrityMismatch, never a Hit.
    [Test]
    public void TryGet_TamperedPassedFieldWithoutRealKey_IsIntegrityMismatchNotHit()
    {
        AnalysisCacheKey key = CreateKey();
        AnalysisCacheProjectManifest[] manifests = { EligibleManifest() };
        AnalysisCacheOutcomeV1 failingOutcome = SampleOutcome() with
        {
            Passed = false,
            Violations = new[]
            {
                new ArchitectureViolation(
                    "no_infra_from_domain", "R001", "MyApp.Domain.Order", "MyApp.Infrastructure",
                    new[] { "MyApp.Infrastructure.Db.OrderRepository" }),
            },
        };
        AnalysisCacheStore.Put(_location, key, manifests, failingOutcome);

        string entryPath = Directory.EnumerateFiles(_root, "*.json", SearchOption.AllDirectories).Single();
        string content = File.ReadAllText(entryPath);
        // Flip Passed to true — an attacker who can write the entry file but does not know the real
        // HMAC key, attempting to poison this entry into a false success. The ContentDigest field
        // itself is left as-is (an unkeyed-hash attacker could previously just recompute a matching
        // plain hash over the tampered bytes; a keyed attacker without the real secret cannot).
        string tampered = content.Replace("\"Passed\":false", "\"Passed\":true");
        Assert.That(tampered, Is.Not.EqualTo(content), "the tamper itself must actually change the bytes");
        File.WriteAllText(entryPath, tampered);

        AnalysisCacheLookupResult result = AnalysisCacheStore.TryGet(_location, key, manifests);

        Assert.That(result.Outcome, Is.EqualTo(AnalysisCacheLookupOutcome.Reject));
        Assert.That(result.Reason, Is.EqualTo(AnalysisCacheRejectReason.IntegrityMismatch));
    }

    [Test]
    public void TryGet_GenuinePutThenTryGetOnSameCacheRoot_StillAuthenticatesAndHits()
    {
        AnalysisCacheKey key = CreateKey();
        AnalysisCacheProjectManifest[] manifests = { EligibleManifest() };

        AnalysisCacheStore.Put(_location, key, manifests, SampleOutcome());
        AnalysisCacheLookupResult result = AnalysisCacheStore.TryGet(_location, key, manifests);

        Assert.That(result.Outcome, Is.EqualTo(AnalysisCacheLookupOutcome.Hit));
    }

    // Finding #1: the HMAC key is generated independently per cache root — an entry authenticated
    // under one root's key must not authenticate against a different root (proving the key isn't
    // hardcoded/global/derivable from the entry's own content).
    [Test]
    public void TryGet_EntryMovedToDifferentCacheRoot_IsIntegrityMismatch()
    {
        AnalysisCacheKey key = CreateKey();
        AnalysisCacheProjectManifest[] manifests = { EligibleManifest() };
        AnalysisCacheStore.Put(_location, key, manifests, SampleOutcome());
        string entryPath = Directory.EnumerateFiles(_root, "*.json", SearchOption.AllDirectories).Single();
        string entryBytes = File.ReadAllText(entryPath);

        string otherRoot = Path.Combine(Path.GetTempPath(), "arch-linter-net-cache-tests-other-root", Guid.NewGuid().ToString("N"));
        AnalysisCacheLocation otherLocation = new(otherRoot, AnalysisCacheMode.ExplicitPath);
        try
        {
            string shard = key.Digest[..2];
            string otherShardDir = Path.Combine(otherRoot, shard);
            Directory.CreateDirectory(otherShardDir);
            File.WriteAllText(Path.Combine(otherShardDir, key.Digest + ".json"), entryBytes);

            AnalysisCacheLookupResult result = AnalysisCacheStore.TryGet(otherLocation, key, manifests);

            Assert.That(result.Outcome, Is.EqualTo(AnalysisCacheLookupOutcome.Reject));
            Assert.That(result.Reason, Is.EqualTo(AnalysisCacheRejectReason.IntegrityMismatch));
        }
        finally
        {
            if (Directory.Exists(otherRoot))
            {
                Directory.Delete(otherRoot, recursive: true);
            }
        }
    }

    // Finding #4: a forged stored manifest list with a duplicate path ([A, A]) must never authorize
    // against a genuinely different current set ([A, B]) just because Count matches and both stored
    // entries alias through the same dictionary key.
    [Test]
    public void TryGet_DuplicateStoredProjectPathAgainstDistinctCurrentSet_IsProjectSetMismatch()
    {
        AnalysisCacheKey key = CreateKey();
        AnalysisCacheProjectManifest[] currentManifests =
        {
            EligibleManifest("src/A/A.csproj", "digest-a"),
            EligibleManifest("src/B/B.csproj", "digest-b"),
        };
        AnalysisCacheStore.Put(_location, key, currentManifests, SampleOutcome());

        string entryPath = Directory.EnumerateFiles(_root, "*.json", SearchOption.AllDirectories).Single();
        string content = File.ReadAllText(entryPath);
        // Hand-forge a stored ProjectManifests array of [A, A] (duplicate path) in place of [A, B].
        // We cannot recompute a valid ContentDigest for arbitrary tampering without the real HMAC
        // key, so this also proves the read path rejects a duplicate-path manifest set outright
        // (ProjectSetMismatch) rather than ever reaching a Hit through the aliasing bug.
        string forged = content.Replace(
            "\"src/B/B.csproj\"", "\"src/A/A.csproj\"");
        File.WriteAllText(entryPath, forged);

        AnalysisCacheLookupResult result = AnalysisCacheStore.TryGet(_location, key, currentManifests);

        Assert.That(result.Outcome, Is.EqualTo(AnalysisCacheLookupOutcome.Reject));
        Assert.That(
            result.Reason,
            Is.EqualTo(AnalysisCacheRejectReason.ProjectSetMismatch).Or.EqualTo(AnalysisCacheRejectReason.IntegrityMismatch));
    }

    // Finding #5: the cache root itself being a pre-created symlink/junction must be rejected by
    // every operation, not only nested paths under it, and not only in ExplicitPath mode (--cache
    // auto skips AnalysisCacheLocationResolver's own explicit-path symlink validation, and
    // AnalysisCacheStore is public API any caller can invoke with a hand-built location).
    [Test]
    public void Inspect_CacheRootItselfIsSymlink_IsRejectedNotFollowed()
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Ignore("Symlink creation requires elevated privileges on Windows by default.");
            return;
        }

        string outsideTarget = Path.Combine(Path.GetTempPath(), "arch-linter-net-cache-root-outside-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outsideTarget);
        File.WriteAllText(Path.Combine(outsideTarget, "secret.json"), "{}");
        try
        {
            Directory.CreateSymbolicLink(_root, outsideTarget);
            AnalysisCacheLocation autoLikeLocation = new(_root, AnalysisCacheMode.Auto);

            IReadOnlyList<AnalysisCacheEntrySummary> summaries = AnalysisCacheStore.Inspect(autoLikeLocation);

            Assert.That(summaries, Is.Empty);
        }
        finally
        {
            DeleteDirectoryLink(_root);
            Directory.Delete(outsideTarget, recursive: true);
        }
    }

    [Test]
    public void Clear_CacheRootItselfIsSymlink_IsRejectedAndTargetUntouched()
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Ignore("Symlink creation requires elevated privileges on Windows by default.");
            return;
        }

        string outsideTarget = Path.Combine(Path.GetTempPath(), "arch-linter-net-cache-root-outside-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outsideTarget);
        File.WriteAllText(Path.Combine(outsideTarget, "secret.json"), "{}");
        try
        {
            Directory.CreateSymbolicLink(_root, outsideTarget);
            AnalysisCacheLocation autoLikeLocation = new(_root, AnalysisCacheMode.Auto);

            Assert.Throws<AnalysisCacheLocationRejectedException>(() => AnalysisCacheStore.Clear(autoLikeLocation));
            Assert.That(Directory.EnumerateFileSystemEntries(outsideTarget), Is.Not.Empty);
        }
        finally
        {
            DeleteDirectoryLink(_root);
            Directory.Delete(outsideTarget, recursive: true);
        }
    }

    [Test]
    public void Put_CacheRootItselfIsSymlink_IsRejectedAsPathUnsafe()
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Ignore("Symlink creation requires elevated privileges on Windows by default.");
            return;
        }

        string outsideTarget = Path.Combine(Path.GetTempPath(), "arch-linter-net-cache-root-outside-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outsideTarget);
        try
        {
            Directory.CreateSymbolicLink(_root, outsideTarget);
            AnalysisCacheLocation autoLikeLocation = new(_root, AnalysisCacheMode.Auto);

            AnalysisCacheStore.PutResult putResult =
                AnalysisCacheStore.Put(autoLikeLocation, CreateKey(), new[] { EligibleManifest() }, SampleOutcome());

            Assert.That(putResult.RejectReason, Is.EqualTo(AnalysisCacheRejectReason.PathUnsafe));
            Assert.That(Directory.EnumerateFileSystemEntries(outsideTarget), Is.Empty);
        }
        finally
        {
            DeleteDirectoryLink(_root);
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
