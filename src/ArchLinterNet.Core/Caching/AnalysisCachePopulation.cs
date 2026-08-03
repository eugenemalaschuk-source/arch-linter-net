using ArchLinterNet.Core.BuildState;

namespace ArchLinterNet.Core.Caching;

// One shared population/lookup implementation for CLI and Testing — not two independently
// maintained ones (matching the analysis-profile/v1 pattern: one AnalysisProfileBuilder for both
// hosts). Computes each discovered project's #406 evaluated-build-input manifest and gates
// AnalysisCacheStore.Put on every one of them being VerifiedCacheEligible.
public static class AnalysisCachePopulation
{
    public readonly record struct Outcome(AnalysisCacheRejectReason? RejectReason, int ProjectsEvaluated);

    public static Outcome TryPopulate(
        AnalysisCacheLocation? location,
        AnalysisCacheKey key,
        IReadOnlyList<string> discoveredProjectPaths,
        string repositoryRoot,
        string? configuration,
        string? targetFramework,
        string? platform,
        string? runtimeIdentifier,
        AnalysisCacheFactsV1 facts,
        CancellationToken cancellationToken = default)
    {
        if (location is null)
        {
            return new Outcome(AnalysisCacheRejectReason.Disabled, 0);
        }

        if (discoveredProjectPaths.Count == 0)
        {
            return new Outcome(AnalysisCacheRejectReason.IneligibleBuildInput, 0);
        }

        List<AnalysisCacheProjectManifest> manifests = new(discoveredProjectPaths.Count);
        foreach (string projectPath in discoveredProjectPaths)
        {
            cancellationToken.ThrowIfCancellationRequested();
            EvaluatedBuildInputManifestV1 manifest = EvaluatedBuildInputManifestCollector.Collect(
                projectPath, repositoryRoot, configuration, targetFramework, platform, runtimeIdentifier, cancellationToken);
            manifests.Add(AnalysisCacheProjectManifest.FromManifest(
                ToRepositoryRelative(projectPath, repositoryRoot), manifest));
        }

        AnalysisCacheRejectReason? rejectReason = AnalysisCacheStore.Put(location, key, manifests, facts, cancellationToken);
        return new Outcome(rejectReason, manifests.Count);
    }

    // Re-derives each stored project's manifest and checks it against the given cache entry's
    // key/authorization chain — the read-side counterpart used by `cache inspect`-style
    // consumers and by tests that want to prove Put/TryGet symmetry against a real manifest
    // instead of a hand-built fake.
    public static AnalysisCacheLookupResult TryLookup(
        AnalysisCacheLocation? location,
        AnalysisCacheKey key,
        IReadOnlyList<string> discoveredProjectPaths,
        string repositoryRoot,
        string? configuration,
        string? targetFramework,
        string? platform,
        string? runtimeIdentifier,
        CancellationToken cancellationToken = default)
    {
        if (location is null)
        {
            return AnalysisCacheLookupResult.Miss(AnalysisCacheRejectReason.Disabled);
        }

        List<AnalysisCacheProjectManifest> manifests = new(discoveredProjectPaths.Count);
        foreach (string projectPath in discoveredProjectPaths)
        {
            cancellationToken.ThrowIfCancellationRequested();
            EvaluatedBuildInputManifestV1 manifest = EvaluatedBuildInputManifestCollector.Collect(
                projectPath, repositoryRoot, configuration, targetFramework, platform, runtimeIdentifier, cancellationToken);
            manifests.Add(AnalysisCacheProjectManifest.FromManifest(
                ToRepositoryRelative(projectPath, repositoryRoot), manifest));
        }

        return AnalysisCacheStore.TryGet(location, key, manifests);
    }

    private static string ToRepositoryRelative(string projectPath, string repositoryRoot)
    {
        string full = Path.GetFullPath(projectPath);
        string root = Path.GetFullPath(repositoryRoot);
        return Path.GetRelativePath(root, full).Replace(Path.DirectorySeparatorChar, '/').Replace(Path.AltDirectorySeparatorChar, '/');
    }
}
