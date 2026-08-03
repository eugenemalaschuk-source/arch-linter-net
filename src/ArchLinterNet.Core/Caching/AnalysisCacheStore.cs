using System.Text.Json;

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
    public static AnalysisCacheRejectReason? Put(
        AnalysisCacheLocation location,
        AnalysisCacheKey key,
        IReadOnlyList<AnalysisCacheProjectManifest> projectManifests,
        AnalysisCacheFactsV1 facts,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(location);
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(projectManifests);
        ArgumentNullException.ThrowIfNull(facts);

        if (projectManifests.Count == 0
            || projectManifests.Any(manifest => manifest.Eligibility != BuildState.CacheEligibility.VerifiedCacheEligible))
        {
            return AnalysisCacheRejectReason.IneligibleBuildInput;
        }

        string entryPath;
        try
        {
            entryPath = ResolveEntryPath(location, key.Digest);
        }
        catch (AnalysisCacheLocationRejectedException)
        {
            return AnalysisCacheRejectReason.PathUnsafe;
        }

        AnalysisCacheEntryV1 entryWithoutDigest = new()
        {
            FormatVersion = AnalysisCacheEnvelope.FormatVersion,
            KeyDigest = key.Digest,
            ToolVersion = AnalysisCacheEnvelope.ToolVersion,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            CompletionStatus = AnalysisCacheEntryCompletionStatus.Success,
            ProjectManifests = projectManifests.OrderBy(manifest => manifest.ProjectPath, StringComparer.Ordinal).ToArray(),
            Facts = facts,
            ContentDigest = string.Empty,
        };
        AnalysisCacheEntryV1 entry = entryWithoutDigest with { ContentDigest = AnalysisCacheContentDigest.Compute(entryWithoutDigest) };

        string directory = Path.GetDirectoryName(entryPath)!;
        Directory.CreateDirectory(directory);
        string tempPath = Path.Combine(directory, $".tmp-{Guid.NewGuid():N}.json");
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(entry, AnalysisCacheJson.Options);

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
                return AnalysisCacheRejectReason.Cancelled;
            }

            File.Move(tempPath, entryPath, overwrite: true);
            return null;
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
        foreach (string file in Directory.EnumerateFiles(canonicalRoot, "*.json", SearchOption.AllDirectories)
                     .Where(file => IsContained(file, canonicalRoot))
                     .OrderBy(file => file, StringComparer.Ordinal))
        {
            summaries.Add(BuildSummary(file, canonicalRoot));
        }

        return summaries;
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
                relativeName, Readable: true, entry.KeyDigest, entry.CreatedAtUtc, entry.ProjectManifests.Count, entry.Facts.Passed);
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

        foreach (string file in Directory.EnumerateFiles(canonicalRoot, "*", SearchOption.AllDirectories)
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
        string candidate = Path.GetFullPath(Path.Combine(canonicalRoot, shard, keyDigest + ".json"));

        if (!IsContained(candidate, canonicalRoot))
        {
            throw new AnalysisCacheLocationRejectedException("Resolved cache entry path escapes the cache root.");
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
