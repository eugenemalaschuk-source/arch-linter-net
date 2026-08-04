using System.Security.Cryptography;
using System.Text;
using ArchLinterNet.Core.BuildState;

namespace ArchLinterNet.Core.Caching;

// Exact byte identity of an artifact consumed by analysis. Project-input manifests alone prove
// how a project was configured, not which PE/PDB/receipt bytes were actually loaded. This list is
// captured before execution and must match again immediately before publication and on cache hit.
public sealed record AnalysisCacheArtifactManifest(string ArtifactPath, string ContentDigest)
{
    public static AnalysisCacheArtifactManifest FromPath(
        string artifactPath,
        string repositoryRoot,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string fullPath = Path.GetFullPath(artifactPath);
        string identity = BuildPortableIdentity(fullPath, repositoryRoot);
        string digest = File.Exists(fullPath)
            ? BuildStateCanonicalHasher.ComputeContentDigest(fullPath, cancellationToken)
            : "missing";
        return new AnalysisCacheArtifactManifest(identity, digest);
    }

    internal static AnalysisCacheArtifactManifest FromContentDigest(
        string artifactPath,
        string repositoryRoot,
        string contentDigest)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contentDigest);
        string fullPath = Path.GetFullPath(artifactPath);
        return new AnalysisCacheArtifactManifest(BuildPortableIdentity(fullPath, repositoryRoot), contentDigest);
    }

    private static string BuildPortableIdentity(string fullPath, string repositoryRoot)
    {
        string canonicalRoot = Path.GetFullPath(repositoryRoot);
        string relative = Path.GetRelativePath(canonicalRoot, fullPath).Replace(Path.DirectorySeparatorChar, '/');
        if (!relative.StartsWith("../", StringComparison.Ordinal) && relative != "..")
        {
            return relative;
        }

        // External reference locations are not portable between hosts. Keep their absolute path
        // out of the entry while still binding reuse to the selected location as well as its bytes.
        string locationHash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(fullPath)));
        return "$external/" + locationHash;
    }
}
