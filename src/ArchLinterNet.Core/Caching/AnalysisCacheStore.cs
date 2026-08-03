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
        ArgumentNullException.ThrowIfNull(location);
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(currentManifests);

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
        if (info.Length == 0)
        {
            return AnalysisCacheLookupResult.Reject(AnalysisCacheRejectReason.Truncated);
        }

        if (info.Length > MaxEntryBytes)
        {
            return AnalysisCacheLookupResult.Reject(AnalysisCacheRejectReason.SizeExceeded, info.Length);
        }

        byte[] bytes;
        try
        {
            bytes = File.ReadAllBytes(entryPath);
        }
        catch (IOException)
        {
            return AnalysisCacheLookupResult.Reject(AnalysisCacheRejectReason.Corrupt);
        }

        AnalysisCacheEntryV1? entry;
        try
        {
            entry = JsonSerializer.Deserialize<AnalysisCacheEntryV1>(bytes, AnalysisCacheJson.Options);
        }
        catch (JsonException)
        {
            return AnalysisCacheLookupResult.Reject(AnalysisCacheRejectReason.Corrupt, bytes.Length);
        }

        return entry is null
            ? AnalysisCacheLookupResult.Reject(AnalysisCacheRejectReason.Corrupt, bytes.Length)
            : Authorize(entry, key, currentManifests, bytes.Length);
    }

    private static AnalysisCacheLookupResult Authorize(
        AnalysisCacheEntryV1 entry,
        AnalysisCacheKey key,
        IReadOnlyList<AnalysisCacheProjectManifest> currentManifests,
        long bytesRead)
    {
        if (!string.Equals(entry.SchemaId, AnalysisCacheEnvelope.SchemaId, StringComparison.Ordinal))
        {
            return AnalysisCacheLookupResult.Reject(AnalysisCacheRejectReason.ForeignSchema, bytesRead);
        }

        if (entry.FormatVersion != AnalysisCacheEnvelope.FormatVersion)
        {
            return AnalysisCacheLookupResult.Reject(AnalysisCacheRejectReason.IncompatibleFormatVersion, bytesRead);
        }

        if (!string.Equals(entry.ContentDigest, AnalysisCacheContentDigest.Compute(entry), StringComparison.Ordinal))
        {
            return AnalysisCacheLookupResult.Reject(AnalysisCacheRejectReason.IntegrityMismatch, bytesRead);
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

        if (entry.ProjectManifests.Any(manifest => manifest.Eligibility != BuildState.CacheEligibility.VerifiedCacheEligible))
        {
            return AnalysisCacheLookupResult.Reject(AnalysisCacheRejectReason.IneligibleBuildInput, bytesRead);
        }

        return AnalysisCacheLookupResult.Hit(entry, bytesRead);
    }

    private static bool ProjectManifestsMatch(
        IReadOnlyList<AnalysisCacheProjectManifest> stored, IReadOnlyList<AnalysisCacheProjectManifest> current)
    {
        if (stored.Count != current.Count)
        {
            return false;
        }

        Dictionary<string, AnalysisCacheProjectManifest> byPath =
            current.ToDictionary(manifest => manifest.ProjectPath, StringComparer.Ordinal);

        foreach (AnalysisCacheProjectManifest storedManifest in stored)
        {
            if (!byPath.TryGetValue(storedManifest.ProjectPath, out AnalysisCacheProjectManifest? currentManifest))
            {
                return false;
            }

            if (!string.Equals(storedManifest.ManifestDigest, currentManifest.ManifestDigest, StringComparison.Ordinal)
                || storedManifest.Eligibility != currentManifest.Eligibility)
            {
                return false;
            }
        }

        return true;
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
        ArgumentNullException.ThrowIfNull(location);
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(projectManifests);
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
            Outcome = outcome,
            ContentDigest = string.Empty,
        };
        AnalysisCacheEntryV1 entry = entryWithoutDigest with { ContentDigest = AnalysisCacheContentDigest.Compute(entryWithoutDigest) };

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
        Directory.CreateDirectory(directory);
        if (FileSystemContainmentGuard.HasReparsePointAncestor(directory, canonicalRoot))
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
        catch
        {
            TryDelete(tempPath);
            throw;
        }
    }

    public static IReadOnlyList<AnalysisCacheEntrySummary> Inspect(AnalysisCacheLocation location)
    {
        ArgumentNullException.ThrowIfNull(location);

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
            if (entry is null || !string.Equals(entry.ContentDigest, AnalysisCacheContentDigest.Compute(entry), StringComparison.Ordinal))
            {
                return new AnalysisCacheEntrySummary(relativeName, Readable: false, null, null, null, null);
            }

            return new AnalysisCacheEntrySummary(
                relativeName, Readable: true, entry.KeyDigest, entry.CreatedAtUtc, entry.ProjectManifests.Count, entry.Outcome.Passed);
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return new AnalysisCacheEntrySummary(relativeName, Readable: false, null, null, null, null);
        }
    }

    public static void Clear(AnalysisCacheLocation location)
    {
        ArgumentNullException.ThrowIfNull(location);

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
