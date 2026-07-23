using System.Security.Cryptography;
using System.Text;

namespace ArchLinterNet.Core.BuildState;

// Computes the `analysis-build-state/v1` build-input fingerprint (SHA-256, lowercase hexadecimal)
// per openspec/specs/analysis-build-state-fingerprints/spec.md. v1 does not attempt to prove that
// a project/import content change was semantically harmless, so this hashes raw content digests
// of every selected source/project/import file rather than reconstructing a full MSBuild
// evaluation manifest — the spec explicitly allows this coarser granularity for v1.
public static class BuildStateCanonicalHasher
{
    // "compile" extensions relevant to build-input identity. Content outside these under a
    // project directory (e.g. markdown, generated bin/obj output) does not affect the digest.
    private static readonly string[] _relevantExtensions =
        { ".cs", ".csproj", ".props", ".targets", ".rsp", ".editorconfig" };

    // Implicitly-imported MSBuild files that live above the project directory and are not
    // discoverable by scanning under it — every selected source/project/import content change
    // must invalidate the fingerprint, and these are relevant imported build inputs whenever
    // present anywhere between the project and the repository root.
    private static readonly string[] _ancestorImportFileNames =
        { "Directory.Build.props", "Directory.Build.targets", "Directory.Build.rsp", "Directory.Packages.props" };

    public static string ComputeBuildInputFingerprint(string projectPath, string repositoryRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryRoot);

        // ArchitectureDiscoveredProject.Path is repository-relative (see
        // ArchitectureProjectDiscoveryService.BuildDiscoveredProject) — it must be resolved
        // against repositoryRoot, not the current process's working directory, or this silently
        // reads the wrong files (or none) whenever the process CWD differs from the repo root.
        string absoluteProjectPath = BuildStatePathResolution.ResolveAbsoluteProjectPath(repositoryRoot, projectPath);
        string projectDirectory = Path.GetDirectoryName(absoluteProjectPath)
            ?? throw new InvalidOperationException($"Cannot determine project directory for '{projectPath}'.");

        List<(string RepoRelativePath, byte[] Digest)> entries = new();

        foreach (string file in EnumerateRelevantFiles(projectDirectory))
        {
            string repoRelative = ToRepositoryRelativePath(file, repositoryRoot);
            byte[] digest = SHA256.HashData(File.ReadAllBytes(file));
            entries.Add((repoRelative, digest));
        }

        foreach (string file in EnumerateAncestorImportFiles(projectDirectory, repositoryRoot))
        {
            string repoRelative = ToRepositoryRelativePath(file, repositoryRoot);
            byte[] digest = SHA256.HashData(File.ReadAllBytes(file));
            entries.Add((repoRelative, digest));
        }

        // Canonical envelope rule: set-like arrays are sorted by a declared canonical key —
        // here, ordinal repository-relative path.
        entries.Sort((a, b) => string.CompareOrdinal(a.RepoRelativePath, b.RepoRelativePath));

        using MemoryStream buffer = new();
        using (StreamWriter writer = new(buffer, Encoding.UTF8, leaveOpen: true))
        {
            writer.Write("analysis-build-state/v1");
            foreach ((string repoRelativePath, byte[] digest) in entries)
            {
                writer.Write('\n');
                writer.Write(repoRelativePath);
                writer.Write(':');
                writer.Write(Convert.ToHexStringLower(digest));
            }
        }

        buffer.Position = 0;
        return Convert.ToHexStringLower(SHA256.HashData(buffer.ToArray()));
    }

    public static string ComputeContentDigest(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        return Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(filePath)));
    }

    private static IEnumerable<string> EnumerateRelevantFiles(string projectDirectory)
    {
        if (!Directory.Exists(projectDirectory))
        {
            yield break;
        }

        foreach (string file in Directory.EnumerateFiles(projectDirectory, "*", SearchOption.AllDirectories))
        {
            if (IsUnderBuildOutputDirectory(file, projectDirectory))
            {
                continue;
            }

            if (!_relevantExtensions.Contains(Path.GetExtension(file), StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            yield return file;
        }
    }

    private static IEnumerable<string> EnumerateAncestorImportFiles(string projectDirectory, string repositoryRoot)
    {
        string repositoryRootFull = Path.GetFullPath(repositoryRoot);
        DirectoryInfo? current = new(Path.GetFullPath(projectDirectory));

        while (current != null)
        {
            foreach (string fileName in _ancestorImportFileNames)
            {
                string candidate = Path.Combine(current.FullName, fileName);
                if (File.Exists(candidate))
                {
                    yield return candidate;
                }
            }

            if (string.Equals(current.FullName.TrimEnd(Path.DirectorySeparatorChar),
                repositoryRootFull.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase))
            {
                yield break;
            }

            current = current.Parent;
        }
    }

    private static bool IsUnderBuildOutputDirectory(string file, string projectDirectory)
    {
        string relative = Path.GetRelativePath(projectDirectory, file);
        string[] segments = relative.Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar });
        return segments.Length > 0
            && (string.Equals(segments[0], "bin", StringComparison.OrdinalIgnoreCase)
                || string.Equals(segments[0], "obj", StringComparison.OrdinalIgnoreCase));
    }

    private static string ToRepositoryRelativePath(string absolutePath, string repositoryRoot)
    {
        string full = Path.GetFullPath(absolutePath);
        string rootFull = Path.GetFullPath(repositoryRoot);
        string relative = Path.GetRelativePath(rootFull, full);
        return relative.Replace(Path.DirectorySeparatorChar, '/').Replace(Path.AltDirectorySeparatorChar, '/');
    }
}
