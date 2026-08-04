namespace ArchLinterNet.Core.BuildState;

// Shared symlink/junction and containment guard used by every component that resolves untrusted
// on-disk paths under a trusted root — EvaluatedBuildInputManifestCollector (build-input evidence)
// and AnalysisCacheStore (analysis-cache/v1 entries) both need the identical defense: reject a
// path whose resolution crosses a reparse point anywhere between the candidate and the root,
// checked immediately before I/O rather than relying on a lexical prefix check alone (a symlink or
// junction pre-created at any ancestor segment can make a lexical prefix match while the real
// filesystem target lives outside the root).
public static class FileSystemContainmentGuard
{
    // Lexical containment: candidate must be the root itself or nested under it. This alone is not
    // sufficient — see HasReparsePointAncestor — but is cheap enough to check first.
    public static bool IsContained(string path, string root) =>
        path.StartsWith(
            root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar,
            StringComparison.Ordinal)
        || PathsEqual(path, root);

    // Walks every existing ancestor directory segment between path and root (inclusive of both
    // ends), rejecting the path if any of them is itself a reparse point (symlink/junction). A
    // pre-created symlinked ancestor is exactly how a lexical-prefix-only containment check can be
    // defeated: the string comparison passes while the real filesystem target resolves outside
    // root. Only existing segments are inspected — a not-yet-created ancestor cannot be a reparse
    // point yet, so it does not block bounded population of new cache entries.
    //
    // The walk never climbs above root, whether or not root itself already exists on disk: a cache
    // root that has not been created yet (the common first-write case) has no existing ancestor of
    // its own to inspect, and continuing to climb past a non-existent root would start inspecting
    // pre-existing, non-attacker-controlled ancestors of the cache root's *parent* directory (e.g. a
    // platform temp-directory symlink such as macOS's /var -> /private/var) — those are outside this
    // guard's threat model and produce false positives, not real containment violations.
    public static bool HasReparsePointAncestor(string path, string root)
    {
        string canonicalRoot = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string? current = Directory.Exists(path) ? path : Path.GetDirectoryName(path);

        while (current != null && IsWithinOrEqual(current, canonicalRoot))
        {
            if (Directory.Exists(current) && IsReparsePoint(new DirectoryInfo(current)))
            {
                return true;
            }

            if (PathsEqual(current, canonicalRoot))
            {
                return false;
            }

            current = Path.GetDirectoryName(current);
        }

        return false;
    }

    private static bool IsWithinOrEqual(string path, string root) =>
        PathsEqual(path, root)
        || path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

    public static bool IsReparsePoint(FileSystemInfo info) =>
        (info.Attributes & FileAttributes.ReparsePoint) != 0;

    public static bool IsReparsePoint(string path)
    {
        // File.Exists/Directory.Exists return false for a broken symlink, which would turn a
        // pre-created dangling link into an apparent missing path and let a later CreateNew or
        // CreateDirectory cross it. For an existing path, probe it using its actual filesystem
        // shape first. FileAttributes.ReparsePoint describes the final filesystem object, unlike
        // LinkTarget on macOS, which can report an ancestor's /var alias as the target of an
        // ordinary child path. For a missing path, enumerate its direct parent: this detects a
        // dangling link without asking FileInfo/DirectoryInfo to resolve a non-existent child
        // through that alias.
        if (Directory.Exists(path))
        {
            return IsReparsePoint(new DirectoryInfo(path));
        }

        if (File.Exists(path))
        {
            return IsReparsePoint(new FileInfo(path));
        }

        string? parent = Path.GetDirectoryName(path);
        if (string.IsNullOrEmpty(parent) || !Directory.Exists(parent))
        {
            return false;
        }

        try
        {
            string fileName = Path.GetFileName(path);
            string? existingPath = Directory.EnumerateFileSystemEntries(parent, "*", SearchOption.TopDirectoryOnly)
                .SingleOrDefault(candidate => string.Equals(Path.GetFileName(candidate), fileName, StringComparison.Ordinal));
            if (existingPath is not null)
            {
                return (File.GetAttributes(existingPath) & FileAttributes.ReparsePoint) != 0;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // An unreadable candidate is handled by the caller as a typed cache rejection; do
            // not turn this defensive probe itself into a validation failure.
        }

        return false;
    }

    private static bool PathsEqual(string a, string b) =>
        string.Equals(
            a.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            b.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            StringComparison.OrdinalIgnoreCase);
}
