using System.Text.Json;
using ArchLinterNet.Core.BuildState;

namespace ArchLinterNet.Core.Caching;

// The analysis-cache/v1 persistence engine: bounded reads, atomic staged writes, full reuse
// authorization, and typed miss/reject reasons. See openspec/specs/analysis-cache/spec.md.
//
// Every operation resolves its target path under AnalysisCacheLocation.RootPath and verifies
// containment before touching the filesystem — a key digest can never escape the cache root
// because it is always a fixed-length lowercase hex SHA-256 string, but this defends the
// invariant explicitly rather than relying on that alone.
public static class AnalysisCacheStore
{
    private const long MaxEntryBytes = 8L * 1024 * 1024;

    // Bounds the manifest list too — an entry with an unbounded project-manifest count could still
    // stay under MaxEntryBytes for small per-project digests while still being an unreasonable
    // amount of reuse-authorization state to recompute on every lookup.
    private const int MaxProjectManifests = 4096;

    // Result of a Put attempt, including bytes actually written (0 when nothing was published) so
    // callers can populate real AnalysisProfileCacheCounters.BytesWritten instead of leaving it 0
    // for every successful populate (see openspec/specs/analysis-cache/spec.md and issue #365's
    // review finding on ValidateCommandHandler.Cache.cs's profile counters).
    public readonly record struct PutResult(AnalysisCacheRejectReason? RejectReason, long BytesWritten)
    {
        public static PutResult Success(long bytesWritten) => new(null, bytesWritten);

        public static PutResult Rejected(AnalysisCacheRejectReason reason) => new(reason, 0);
    }

    public static AnalysisCacheLookupResult TryGet(
        AnalysisCacheLocation location,
        AnalysisCacheKey key,
        IReadOnlyList<AnalysisCacheProjectManifest> currentManifests)
    {
        return TryGet(location, key, currentManifests, Array.Empty<AnalysisCacheArtifactManifest>());
    }

    public static AnalysisCacheLookupResult TryGet(
        AnalysisCacheLocation location,
        AnalysisCacheKey key,
        IReadOnlyList<AnalysisCacheProjectManifest> currentManifests,
        IReadOnlyList<AnalysisCacheArtifactManifest> currentArtifacts)
    {
        ArgumentNullException.ThrowIfNull(location);
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(currentManifests);
        ArgumentNullException.ThrowIfNull(currentArtifacts);

        if (IsRootUnsafe(location))
        {
            return AnalysisCacheLookupResult.Reject(AnalysisCacheRejectReason.PathUnsafe);
        }

        string entryPath;
        try
        {
            entryPath = ResolveEntryPath(location, key.Digest);
        }
        catch (AnalysisCacheLocationRejectedException)
        {
            return AnalysisCacheLookupResult.Reject(AnalysisCacheRejectReason.PathUnsafe);
        }

        string canonicalRoot = Path.GetFullPath(location.RootPath);
        if (FileSystemContainmentGuard.HasReparsePointAncestor(entryPath, canonicalRoot)
            || FileSystemContainmentGuard.IsReparsePoint(entryPath))
        {
            return AnalysisCacheLookupResult.Reject(AnalysisCacheRejectReason.PathUnsafe);
        }

        if (!File.Exists(entryPath))
        {
            return AnalysisCacheLookupResult.Miss(AnalysisCacheRejectReason.Missing);
        }

        FileInfo info = new(entryPath);
        long entryLength;
        try
        {
            entryLength = info.Length;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return AnalysisCacheLookupResult.Reject(AnalysisCacheRejectReason.Corrupt);
        }

        if (entryLength == 0)
        {
            return AnalysisCacheLookupResult.Reject(AnalysisCacheRejectReason.Truncated);
        }

        if (entryLength > MaxEntryBytes)
        {
            return AnalysisCacheLookupResult.Reject(AnalysisCacheRejectReason.SizeExceeded, entryLength);
        }

        byte[] bytes;
        try
        {
            bytes = File.ReadAllBytes(entryPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return AnalysisCacheLookupResult.Reject(AnalysisCacheRejectReason.Corrupt);
        }

        AnalysisCacheEntryV1? entry;
        try
        {
            entry = JsonSerializer.Deserialize<AnalysisCacheEntryV1>(bytes, AnalysisCacheJson.Options);
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            return AnalysisCacheLookupResult.Reject(AnalysisCacheRejectReason.Corrupt, bytes.Length);
        }

        return entry is null
            ? AnalysisCacheLookupResult.Reject(AnalysisCacheRejectReason.Corrupt, bytes.Length)
            : Authorize(entry, key, currentManifests, currentArtifacts, bytes.Length, canonicalRoot);
    }

    private static AnalysisCacheLookupResult Authorize(
        AnalysisCacheEntryV1 entry,
        AnalysisCacheKey key,
        IReadOnlyList<AnalysisCacheProjectManifest> currentManifests,
        IReadOnlyList<AnalysisCacheArtifactManifest> currentArtifacts,
        long bytesRead,
        string cacheRootPath)
    {
        if (!HasValidStructure(entry))
        {
            return AnalysisCacheLookupResult.Reject(AnalysisCacheRejectReason.Corrupt, bytesRead);
        }

        if (!string.Equals(entry.SchemaId, AnalysisCacheEnvelope.SchemaId, StringComparison.Ordinal))
        {
            return AnalysisCacheLookupResult.Reject(AnalysisCacheRejectReason.ForeignSchema, bytesRead);
        }

        if (entry.FormatVersion != AnalysisCacheEnvelope.FormatVersion)
        {
            return AnalysisCacheLookupResult.Reject(AnalysisCacheRejectReason.IncompatibleFormatVersion, bytesRead);
        }

        // Finding #1: a keyed HMAC verification, not a plain-hash recompute-and-compare — a
        // tampered/poisoned entry cannot forge a matching tag without also knowing this cache
        // root's local AnalysisCacheHmacKeyStore secret. Compared in constant time (see
        // AnalysisCacheContentDigest.Verify) to avoid a timing side channel on the check.
        if (!AnalysisCacheContentDigest.TryVerify(entry, cacheRootPath, entry.ContentDigest, out AnalysisCacheRejectReason digestFailure))
        {
            return AnalysisCacheLookupResult.Reject(digestFailure, bytesRead);
        }

        if (!string.Equals(entry.KeyDigest, key.Digest, StringComparison.Ordinal))
        {
            return AnalysisCacheLookupResult.Reject(AnalysisCacheRejectReason.KeyMismatch, bytesRead);
        }

        if (!string.Equals(entry.ToolVersion, AnalysisCacheEnvelope.ToolVersion, StringComparison.Ordinal))
        {
            return AnalysisCacheLookupResult.Reject(AnalysisCacheRejectReason.IncompatibleToolVersion, bytesRead);
        }

        if (entry.CompletionStatus != AnalysisCacheEntryCompletionStatus.Success)
        {
            return AnalysisCacheLookupResult.Reject(AnalysisCacheRejectReason.IncompleteOriginalRun, bytesRead);
        }

        if (!ProjectManifestsMatch(entry.ProjectManifests, currentManifests))
        {
            return AnalysisCacheLookupResult.Reject(AnalysisCacheRejectReason.ProjectSetMismatch, bytesRead);
        }

        if (!ArtifactManifestsMatch(entry.ArtifactManifests, currentArtifacts))
        {
            return AnalysisCacheLookupResult.Reject(AnalysisCacheRejectReason.ArtifactSetMismatch, bytesRead);
        }

        if (entry.ProjectManifests.Any(manifest => manifest.Eligibility != BuildState.CacheEligibility.VerifiedCacheEligible))
        {
            return AnalysisCacheLookupResult.Reject(AnalysisCacheRejectReason.IneligibleBuildInput, bytesRead);
        }

        return AnalysisCacheLookupResult.Hit(entry, bytesRead);
    }

    private static bool HasValidStructure(AnalysisCacheEntryV1 entry)
    {
        if (string.IsNullOrWhiteSpace(entry.SchemaId)
            || string.IsNullOrWhiteSpace(entry.KeyDigest)
            || string.IsNullOrWhiteSpace(entry.Mode)
            || string.IsNullOrWhiteSpace(entry.ToolVersion)
            || string.IsNullOrWhiteSpace(entry.ContentDigest)
            || entry.ProjectManifests is null
            || entry.ArtifactManifests is null
            || entry.Outcome is null)
        {
            return false;
        }

        return entry.ProjectManifests.All(manifest => manifest is not null
                && !string.IsNullOrWhiteSpace(manifest.ProjectPath)
                && !string.IsNullOrWhiteSpace(manifest.ManifestDigest))
            && entry.ArtifactManifests.All(manifest => manifest is not null
                && !string.IsNullOrWhiteSpace(manifest.ArtifactPath)
                && !string.IsNullOrWhiteSpace(manifest.ContentDigest))
            && HasValidOutcome(entry.Outcome);
    }

    private static bool HasValidOutcome(AnalysisCacheOutcomeV1 outcome) =>
        outcome.Violations is not null
        && outcome.Cycles is not null
        && outcome.CoverageFindings is not null
        && outcome.UnmatchedIgnoredViolations is not null
        && outcome.PolicyConsistencyFindings is not null
        && outcome.ClassificationConflicts is not null
        && outcome.ClassificationMetadataFailures is not null
        && outcome.ClassificationRoles is not null
        && outcome.CycleFindings is not null
        && outcome.CoverageSummaries is not null
        && outcome.SubtractiveMatcherParticipation is not null
        && outcome.CoverageConfig is not null
        && outcome.UnmatchedIgnoredViolationsConfig is not null
        && outcome.PolicyConsistencyConfig is not null;

    // Finding #4: this must be genuine set equivalence, not a lookup-and-hope. The prior
    // implementation dictionary-ized `current` by ProjectPath and looked up each `stored` entry in
    // it — a forged stored list with a duplicate path (e.g. [A, A] when current is [A, B]) has the
    // same Count and both stored entries resolve through byPath[A], so B is never verified and the
    // entry could still authorize as a Hit. Reject any duplicate ProjectPath on either side first
    // (fail closed, never throw — a malformed/tampered entry is just another Reject reason), then
    // compare as two canonical ordered collections of (ProjectPath, ManifestDigest, Eligibility)
    // for exact one-to-one equality.
    private static bool ProjectManifestsMatch(
        IReadOnlyList<AnalysisCacheProjectManifest> stored, IReadOnlyList<AnalysisCacheProjectManifest> current)
    {
        if (stored.Count != current.Count)
        {
            return false;
        }

        if (HasDuplicateProjectPath(stored) || HasDuplicateProjectPath(current))
        {
            return false;
        }

        var storedOrdered = stored
            .OrderBy(manifest => manifest.ProjectPath, StringComparer.Ordinal)
            .Select(manifest => (manifest.ProjectPath, manifest.ManifestDigest, manifest.Eligibility))
            .ToList();
        var currentOrdered = current
            .OrderBy(manifest => manifest.ProjectPath, StringComparer.Ordinal)
            .Select(manifest => (manifest.ProjectPath, manifest.ManifestDigest, manifest.Eligibility))
            .ToList();

        for (int i = 0; i < storedOrdered.Count; i++)
        {
            (string StoredPath, string StoredDigest, CacheEligibility StoredEligibility) storedEntry = storedOrdered[i];
            (string CurrentPath, string CurrentDigest, CacheEligibility CurrentEligibility) currentEntry = currentOrdered[i];

            if (!string.Equals(storedEntry.StoredPath, currentEntry.CurrentPath, StringComparison.Ordinal)
                || !string.Equals(storedEntry.StoredDigest, currentEntry.CurrentDigest, StringComparison.Ordinal)
                || storedEntry.StoredEligibility != currentEntry.CurrentEligibility)
            {
                return false;
            }
        }

        return true;
    }

    private static bool HasDuplicateProjectPath(IReadOnlyList<AnalysisCacheProjectManifest> manifests)
    {
        HashSet<string> seen = new(StringComparer.Ordinal);
        foreach (AnalysisCacheProjectManifest manifest in manifests)
        {
            if (!seen.Add(manifest.ProjectPath))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ArtifactManifestsMatch(
        IReadOnlyList<AnalysisCacheArtifactManifest> stored,
        IReadOnlyList<AnalysisCacheArtifactManifest> current)
    {
        if (stored.Count != current.Count || HasDuplicateArtifactPath(stored) || HasDuplicateArtifactPath(current))
        {
            return false;
        }

        return stored.OrderBy(manifest => manifest.ArtifactPath, StringComparer.Ordinal)
            .SequenceEqual(current.OrderBy(manifest => manifest.ArtifactPath, StringComparer.Ordinal));
    }

    private static bool HasDuplicateArtifactPath(IReadOnlyList<AnalysisCacheArtifactManifest> manifests)
    {
        HashSet<string> seen = new(StringComparer.Ordinal);
        return manifests.Any(manifest => !seen.Add(manifest.ArtifactPath));
    }

    // Publishes exactly once per entry via a same-directory temp file + atomic rename, observing
    // cancellation immediately before that rename (#375's cancellation-safe publication
    // requirement) so a cancelled populate call can never expose a reusable entry.
    public static PutResult Put(
        AnalysisCacheLocation location,
        AnalysisCacheKey key,
        IReadOnlyList<AnalysisCacheProjectManifest> projectManifests,
        AnalysisCacheOutcomeV1 outcome,
        CancellationToken cancellationToken = default)
    {
        return Put(location, key, projectManifests, Array.Empty<AnalysisCacheArtifactManifest>(), outcome,
            cancellationToken: cancellationToken);
    }

    public static PutResult Put(
        AnalysisCacheLocation location,
        AnalysisCacheKey key,
        IReadOnlyList<AnalysisCacheProjectManifest> projectManifests,
        IReadOnlyList<AnalysisCacheArtifactManifest> artifactManifests,
        AnalysisCacheOutcomeV1 outcome,
        AnalysisCacheWorkProvenanceV1? workProvenance = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(location);
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(projectManifests);
        ArgumentNullException.ThrowIfNull(artifactManifests);
        ArgumentNullException.ThrowIfNull(outcome);

        if (projectManifests.Count == 0
            || projectManifests.Any(manifest => manifest.Eligibility != BuildState.CacheEligibility.VerifiedCacheEligible))
        {
            return PutResult.Rejected(AnalysisCacheRejectReason.IneligibleBuildInput);
        }

        if (projectManifests.Count > MaxProjectManifests)
        {
            return PutResult.Rejected(AnalysisCacheRejectReason.SizeExceeded);
        }

        if (HasDuplicateProjectPath(projectManifests) || HasDuplicateArtifactPath(artifactManifests))
        {
            return PutResult.Rejected(AnalysisCacheRejectReason.ProjectSetMismatch);
        }

        if (IsRootUnsafe(location))
        {
            return PutResult.Rejected(AnalysisCacheRejectReason.PathUnsafe);
        }

        string entryPath;
        try
        {
            entryPath = ResolveEntryPath(location, key.Digest);
        }
        catch (AnalysisCacheLocationRejectedException)
        {
            return PutResult.Rejected(AnalysisCacheRejectReason.PathUnsafe);
        }

        AnalysisCacheEntryV1 entryWithoutDigest = new()
        {
            FormatVersion = AnalysisCacheEnvelope.FormatVersion,
            KeyDigest = key.Digest,
            Mode = key.Mode,
            ToolVersion = AnalysisCacheEnvelope.ToolVersion,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            CompletionStatus = AnalysisCacheEntryCompletionStatus.Success,
            ProjectManifests = projectManifests.OrderBy(manifest => manifest.ProjectPath, StringComparer.Ordinal).ToArray(),
            ArtifactManifests = artifactManifests.OrderBy(manifest => manifest.ArtifactPath, StringComparer.Ordinal).ToArray(),
            Outcome = outcome,
            WorkProvenance = workProvenance ?? new AnalysisCacheWorkProvenanceV1(0, 0, 0, 0, 0),
            ContentDigest = string.Empty,
        };
        if (!AnalysisCacheContentDigest.TryCompute(
                entryWithoutDigest, Path.GetFullPath(location.RootPath), out string? contentDigest,
                out AnalysisCacheRejectReason digestFailure))
        {
            return PutResult.Rejected(digestFailure);
        }

        AnalysisCacheEntryV1 entry = entryWithoutDigest with { ContentDigest = contentDigest! };

        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(entry, AnalysisCacheJson.Options);

        // Enforced before any I/O — a write that would only ever be rejected as SizeExceeded by
        // every subsequent TryGet must never reach the filesystem at all (see
        // openspec/specs/analysis-cache/spec.md, "Miss and reject outcomes are typed and fail safe").
        if (bytes.LongLength > MaxEntryBytes)
        {
            return PutResult.Rejected(AnalysisCacheRejectReason.SizeExceeded);
        }

        // Reparse-point ancestor check happens immediately before I/O, on the resolved entry path's
        // directory chain up to and including the cache root — mirrors
        // EvaluatedBuildInputManifestCollector's own symlink defense (see
        // FileSystemContainmentGuard). A lexical prefix match (already verified by ResolveEntryPath)
        // is not sufficient: a pre-created symlinked shard directory would still pass that check
        // while resolving outside the cache root on disk.
        string canonicalRoot = Path.GetFullPath(location.RootPath);
        string directory = Path.GetDirectoryName(entryPath)!;
        try
        {
            Directory.CreateDirectory(directory);
            if (FileSystemContainmentGuard.HasReparsePointAncestor(directory, canonicalRoot))
            {
                return PutResult.Rejected(AnalysisCacheRejectReason.PathUnsafe);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return PutResult.Rejected(AnalysisCacheRejectReason.PathUnsafe);
        }

        string tempPath = Path.Combine(directory, $".tmp-{Guid.NewGuid():N}.json");

        try
        {
            using (FileStream stream = new(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush(flushToDisk: true);
            }

            if (cancellationToken.IsCancellationRequested)
            {
                TryDelete(tempPath);
                return PutResult.Rejected(AnalysisCacheRejectReason.Cancelled);
            }

            File.Move(tempPath, entryPath, overwrite: true);
            return PutResult.Success(bytes.LongLength);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            TryDelete(tempPath);
            return PutResult.Rejected(AnalysisCacheRejectReason.PathUnsafe);
        }
    }

    public static IReadOnlyList<AnalysisCacheEntrySummary> Inspect(AnalysisCacheLocation location)
    {
        ArgumentNullException.ThrowIfNull(location);

        if (IsRootUnsafe(location))
        {
            return Array.Empty<AnalysisCacheEntrySummary>();
        }

        if (!Directory.Exists(location.RootPath))
        {
            return Array.Empty<AnalysisCacheEntrySummary>();
        }

        string canonicalRoot = Path.GetFullPath(location.RootPath);
        List<AnalysisCacheEntrySummary> summaries = new();
        foreach (string file in EnumerateFilesNotFollowingSymlinks(canonicalRoot, "*.json")
                     .Where(file => IsContained(file, canonicalRoot))
                     .OrderBy(file => file, StringComparer.Ordinal))
        {
            summaries.Add(BuildSummary(file, canonicalRoot));
        }

        return summaries;
    }

    // Manual recursive walk instead of Directory.EnumerateFiles(..., SearchOption.AllDirectories):
    // that overload follows symlinked/junction subdirectories, which would let a pre-created link
    // under the cache root make Inspect/Clear read or delete files outside the cache root entirely.
    // Every directory is checked for being a reparse point itself before this recurses into it —
    // its *contents* are never enumerated in that case, matching EvaluatedBuildInputManifestCollector's
    // "reject reparse points before I/O" pattern (FileSystemContainmentGuard).
    private static IEnumerable<string> EnumerateFilesNotFollowingSymlinks(string directory, string searchPattern)
    {
        foreach (string file in SafeEnumerateFiles(directory, searchPattern))
        {
            yield return file;
        }

        foreach (string subdirectory in SafeEnumerateDirectories(directory))
        {
            if (FileSystemContainmentGuard.IsReparsePoint(subdirectory))
            {
                continue;
            }

            foreach (string file in EnumerateFilesNotFollowingSymlinks(subdirectory, searchPattern))
            {
                yield return file;
            }
        }
    }

    private static IEnumerable<string> SafeEnumerateFiles(string directory, string searchPattern)
    {
        try
        {
            return Directory.EnumerateFiles(directory, searchPattern, SearchOption.TopDirectoryOnly);
        }
        catch (IOException)
        {
            return Array.Empty<string>();
        }
        catch (UnauthorizedAccessException)
        {
            return Array.Empty<string>();
        }
    }

    private static IEnumerable<string> SafeEnumerateDirectories(string directory)
    {
        try
        {
            return Directory.EnumerateDirectories(directory, "*", SearchOption.TopDirectoryOnly);
        }
        catch (IOException)
        {
            return Array.Empty<string>();
        }
        catch (UnauthorizedAccessException)
        {
            return Array.Empty<string>();
        }
    }

    private static AnalysisCacheEntrySummary BuildSummary(string file, string canonicalRoot)
    {
        string relativeName = Path.GetRelativePath(canonicalRoot, file).Replace(Path.DirectorySeparatorChar, '/');
        try
        {
            byte[] bytes = File.ReadAllBytes(file);
            if (bytes.Length == 0 || bytes.LongLength > MaxEntryBytes)
            {
                return new AnalysisCacheEntrySummary(relativeName, Readable: false, null, null, null, null);
            }

            AnalysisCacheEntryV1? entry = JsonSerializer.Deserialize<AnalysisCacheEntryV1>(bytes, AnalysisCacheJson.Options);
            if (entry is null || !HasValidStructure(entry)
                || !AnalysisCacheContentDigest.TryVerify(entry, canonicalRoot, entry.ContentDigest, out _))
            {
                return new AnalysisCacheEntrySummary(relativeName, Readable: false, null, null, null, null);
            }

            return new AnalysisCacheEntrySummary(
                relativeName, Readable: true, entry.KeyDigest, entry.CreatedAtUtc, entry.ProjectManifests.Count, entry.Outcome.Passed);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException
            or ArgumentException or InvalidOperationException)
        {
            return new AnalysisCacheEntrySummary(relativeName, Readable: false, null, null, null, null);
        }
    }

    public static void Clear(AnalysisCacheLocation location)
    {
        ArgumentNullException.ThrowIfNull(location);

        if (IsRootUnsafe(location))
        {
            throw new AnalysisCacheLocationRejectedException(
                $"Refusing to clear cache root '{location.RootPath}' because it is itself a symlink/reparse point.");
        }

        if (!Directory.Exists(location.RootPath))
        {
            return;
        }

        string canonicalRoot = Path.GetFullPath(location.RootPath);
        if (IsFileSystemRootLike(canonicalRoot))
        {
            throw new AnalysisCacheLocationRejectedException(
                $"Refusing to clear unsafe cache root '{location.RootPath}'.");
        }

        foreach (string file in EnumerateFilesNotFollowingSymlinks(canonicalRoot, "*")
                     .Where(file => IsContained(file, canonicalRoot)))
        {
            TryDelete(file);
        }
    }

    private static string ResolveEntryPath(AnalysisCacheLocation location, string keyDigest)
    {
        if (string.IsNullOrEmpty(keyDigest) || keyDigest.Length < 4 || !keyDigest.All(Uri.IsHexDigit))
        {
            throw new AnalysisCacheLocationRejectedException("Cache key digest is not a valid hex identity.");
        }

        string canonicalRoot = Path.GetFullPath(location.RootPath);
        string shard = keyDigest[..2];
        string shardDirectory = Path.Combine(canonicalRoot, shard);
        string candidate = Path.GetFullPath(Path.Combine(shardDirectory, keyDigest + ".json"));

        if (!IsContained(candidate, canonicalRoot))
        {
            throw new AnalysisCacheLocationRejectedException("Resolved cache entry path escapes the cache root.");
        }

        // A lexical prefix match is not sufficient on its own: a symlink/junction pre-created at
        // the shard directory (or any existing ancestor between it and the cache root) would still
        // pass IsContained above while resolving to a location outside the cache root on disk. Only
        // *existing* ancestors are inspected — a shard directory that does not exist yet (the normal
        // first-write case) cannot be a reparse point.
        if (FileSystemContainmentGuard.HasReparsePointAncestor(shardDirectory, canonicalRoot))
        {
            throw new AnalysisCacheLocationRejectedException(
                "Resolved cache entry path crosses a symlink or junction under the cache root.");
        }

        return candidate;
    }

    // Finding #5: the cache root itself must be rejected when it is a symlink/reparse-point
    // directory, in every mode — not only AnalysisCacheMode.ExplicitPath (the mode
    // AnalysisCacheLocationResolver.ResolveExplicitRoot already validates at resolution time).
    // `--cache auto`'s ResolveAutoRoot never runs that same symlink check, and AnalysisCacheStore
    // is a public API any caller can invoke directly with a hand-built AnalysisCacheLocation, so a
    // pre-created root-level symlink pointing outside the intended cache root must be caught here,
    // at the start of every operation, before Directory.Exists/enumeration/any I/O touches it —
    // TryGet, Put, Inspect, and Clear all call this first.
    private static bool IsRootUnsafe(AnalysisCacheLocation location)
    {
        string canonicalRoot = Path.GetFullPath(location.RootPath);
        return Directory.Exists(canonicalRoot) && FileSystemContainmentGuard.IsReparsePoint(canonicalRoot);
    }

    private static bool IsContained(string path, string root) =>
        path.StartsWith(root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.Ordinal)
        || string.Equals(path, root, StringComparison.Ordinal);

    private static bool IsFileSystemRootLike(string fullPath)
    {
        string? root = Path.GetPathRoot(fullPath);
        return !string.IsNullOrEmpty(root)
            && string.Equals(
                root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                StringComparison.OrdinalIgnoreCase);
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
            // Best-effort cleanup of a temp/staged file; a leftover .tmp-* file never masquerades
            // as a published entry since Inspect/TryGet only ever look for the final key-digest name.
        }
    }
}
