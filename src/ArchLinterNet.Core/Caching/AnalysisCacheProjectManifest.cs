using ArchLinterNet.Core.BuildState;

namespace ArchLinterNet.Core.Caching;

// One selected project/context's #406 evaluated-build-input-manifest outcome, carried inside a
// cache entry. Reuse authorization requires every entry in a stored set to still be
// VerifiedCacheEligible with a matching Digest when recomputed at lookup time.
public sealed record AnalysisCacheProjectManifest(
    string ProjectPath,
    string ManifestDigest,
    CacheEligibility Eligibility)
{
    public static AnalysisCacheProjectManifest FromManifest(string projectPath, EvaluatedBuildInputManifestV1 manifest) =>
        new(projectPath, manifest.Digest, manifest.Eligibility);
}
