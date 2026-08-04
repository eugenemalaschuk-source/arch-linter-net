namespace ArchLinterNet.Core.Caching;

// A raw-byte identity captured before analysis consumes an artifact. The physical path remains
// private to the running snapshot; only the portable AnalysisCacheArtifactManifest is persisted.
internal sealed record AnalysisCacheCapturedFileIdentity(string FullPath, string ContentDigest)
{
    public static AnalysisCacheCapturedFileIdentity FromPath(string path, string contentDigest) => new(
        Path.GetFullPath(path),
        contentDigest);
}
