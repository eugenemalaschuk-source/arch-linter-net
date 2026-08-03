using System.Runtime.CompilerServices;
using ArchLinterNet.Core.BuildState;
using ArchLinterNet.Core.Validation;

namespace ArchLinterNet.Core.Caching;

// Shared authorization capture for both cache lookup and post-analysis publication. A successful
// lookup and a later write must use the same pre-analysis input snapshot; otherwise a mutation
// between execution and population could publish an old result under a new input digest.
public static class AnalysisCachePopulation
{
    private sealed record CompletedAuthorization(
        PreparedAuthorization Initial,
        PreparedAuthorization? Completion,
        Outcome CompletionRejection);

    private static readonly ConditionalWeakTable<ValidationOutcome, CompletedAuthorization> _authorizations = new();

    internal static Func<string, string, string?, string?, string?, string?, CancellationToken, EvaluatedBuildInputManifestV1>?
        TestManifestCollectorOverride
    { get; set; }

    internal sealed record PreparedAuthorization(
        AnalysisCacheLocation Location,
        AnalysisCacheKey Key,
        IReadOnlyList<string> DiscoveredProjectPaths,
        IReadOnlyList<string> ArtifactPaths,
        string RepositoryRoot,
        string? Configuration,
        string? TargetFramework,
        string? Platform,
        string? RuntimeIdentifier,
        bool HasUnfingerprintedSourceInputs,
        IReadOnlyList<AnalysisCacheProjectManifest> ProjectManifests,
        IReadOnlyList<AnalysisCacheArtifactManifest> ArtifactManifests);

    internal readonly record struct LookupPreparation(
        AnalysisCacheLookupResult Lookup,
        PreparedAuthorization? Authorization,
        int IneligibleUnitCount = 0);

    // PopulationAttempted distinguishes a cache hit/no-cache path (no write attempted) from a
    // rejected publication. Hosts must not turn a hit into a synthetic Write in their profile.
    public readonly record struct Outcome(
        AnalysisCacheRejectReason? RejectReason,
        int ProjectsEvaluated,
        int IneligibleProjectCount,
        long BytesWritten,
        bool PopulationAttempted = true)
    {
        public static Outcome Skipped => new(null, 0, 0, 0, PopulationAttempted: false);
    }

    private static EvaluatedBuildInputManifestV1 CollectManifest(
        string projectPath, string repositoryRoot, string? configuration, string? targetFramework,
        string? platform, string? runtimeIdentifier, CancellationToken cancellationToken)
    {
        return (TestManifestCollectorOverride ?? EvaluatedBuildInputManifestCollector.Collect)(
            projectPath, repositoryRoot, configuration, targetFramework, platform, runtimeIdentifier, cancellationToken);
    }

    // Compatibility entry point used by direct callers and focused store tests. The snapshot path
    // below supplies a prepared authorization so it can prove pre/post-execution equivalence.
    public static Outcome TryPopulate(
        AnalysisCacheLocation? location,
        AnalysisCacheKey key,
        IReadOnlyList<string> discoveredProjectPaths,
        string repositoryRoot,
        string? configuration,
        string? targetFramework,
        string? platform,
        string? runtimeIdentifier,
        AnalysisCacheOutcomeV1 outcome,
        CancellationToken cancellationToken = default)
    {
        PreparedAuthorization? authorization = Prepare(
            location, key, discoveredProjectPaths, Array.Empty<string>(), repositoryRoot, configuration,
            targetFramework, platform, runtimeIdentifier, hasUnfingerprintedSourceInputs: false,
            cancellationToken, out Outcome rejected);
        if (authorization is null)
        {
            return rejected;
        }

        return Put(authorization, outcome, cancellationToken);
    }

    // The cache-hit path calls this from ArchitectureAnalysisSnapshot before contract execution.
    // The returned prepared authorization is retained only on a non-hit and used to prove the
    // input state did not change before a later publication.
    internal static LookupPreparation TryLookupWithAuthorization(
        AnalysisCacheLocation? location,
        AnalysisCacheKey key,
        IReadOnlyList<string> discoveredProjectPaths,
        IReadOnlyList<string> artifactPaths,
        string repositoryRoot,
        string? configuration,
        string? targetFramework,
        string? platform,
        string? runtimeIdentifier,
        bool hasUnfingerprintedSourceInputs,
        CancellationToken cancellationToken = default)
    {
        PreparedAuthorization? authorization = Prepare(
            location, key, discoveredProjectPaths, artifactPaths, repositoryRoot, configuration,
            targetFramework, platform, runtimeIdentifier, hasUnfingerprintedSourceInputs,
            cancellationToken, out Outcome rejected);
        if (authorization is null)
        {
            AnalysisCacheLookupResult lookup = rejected.RejectReason == AnalysisCacheRejectReason.Disabled
                ? AnalysisCacheLookupResult.Miss(AnalysisCacheRejectReason.Disabled)
                : AnalysisCacheLookupResult.Reject(rejected.RejectReason ?? AnalysisCacheRejectReason.Corrupt);
            return new LookupPreparation(lookup, null, rejected.IneligibleProjectCount);
        }

        return new LookupPreparation(
            AnalysisCacheStore.TryGet(
                authorization.Location, authorization.Key, authorization.ProjectManifests, authorization.ArtifactManifests),
            authorization);
    }

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
        return TryLookupWithAuthorization(
            location, key, discoveredProjectPaths, Array.Empty<string>(), repositoryRoot, configuration,
            targetFramework, platform, runtimeIdentifier, hasUnfingerprintedSourceInputs: false, cancellationToken).Lookup;
    }

    // Called by ArchitectureAnalysisSnapshot after a cache miss completed its real evaluation.
    // The initial authorization proves the selected artifacts did not change during execution;
    // completion captures the full path set the isolated scope actually loaded, including lazy
    // probing-path references whose Assembly.Location is empty after LoadFromStream.
    // ConditionalWeakTable keys by object identity and neither changes nor prolongs the public
    // ValidationOutcome record's value/equality contract.
    internal static void AttachAuthorization(
        ValidationOutcome outcome,
        PreparedAuthorization authorization,
        IReadOnlyList<string> completedArtifactPaths)
    {
        ArgumentNullException.ThrowIfNull(outcome);
        ArgumentNullException.ThrowIfNull(authorization);

        PreparedAuthorization? completion = Prepare(
            authorization.Location,
            authorization.Key,
            authorization.DiscoveredProjectPaths,
            completedArtifactPaths,
            authorization.RepositoryRoot,
            authorization.Configuration,
            authorization.TargetFramework,
            authorization.Platform,
            authorization.RuntimeIdentifier,
            authorization.HasUnfingerprintedSourceInputs,
            default,
            out Outcome rejected);

        _authorizations.Remove(outcome);
        _authorizations.Add(outcome, new CompletedAuthorization(authorization, completion, rejected));
    }

    // Called by CLI and Testing hosts after a completed snapshot Evaluate. The opaque prepared
    // authorization is snapshot-owned transient state; consumers cannot manufacture a
    // post-analysis authorization from changed files.
    public static Outcome TryPopulateCompletedOutcome(ValidationOutcome outcome, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(outcome);
        if (outcome.PreflightBlocked)
        {
            return new Outcome(AnalysisCacheRejectReason.IncompleteOriginalRun, 0, 0, 0);
        }

        if (!_authorizations.TryGetValue(outcome, out CompletedAuthorization? captured))
        {
            return Outcome.Skipped;
        }

        if (captured.Completion is null)
        {
            return captured.CompletionRejection;
        }

        PreparedAuthorization? currentInitial = Prepare(
            captured.Initial.Location, captured.Initial.Key, captured.Initial.DiscoveredProjectPaths,
            captured.Initial.ArtifactPaths, captured.Initial.RepositoryRoot, captured.Initial.Configuration,
            captured.Initial.TargetFramework, captured.Initial.Platform, captured.Initial.RuntimeIdentifier,
            captured.Initial.HasUnfingerprintedSourceInputs, cancellationToken, out Outcome rejected);
        if (currentInitial is null)
        {
            return rejected;
        }

        if (!AuthorizationMatches(captured.Initial, currentInitial))
        {
            return new Outcome(AnalysisCacheRejectReason.InputChangedDuringExecution,
                currentInitial.ProjectManifests.Count,
                currentInitial.ProjectManifests.Count(manifest => manifest.Eligibility != CacheEligibility.VerifiedCacheEligible),
                0);
        }

        PreparedAuthorization? currentCompletion = Prepare(
            captured.Completion.Location, captured.Completion.Key, captured.Completion.DiscoveredProjectPaths,
            captured.Completion.ArtifactPaths, captured.Completion.RepositoryRoot, captured.Completion.Configuration,
            captured.Completion.TargetFramework, captured.Completion.Platform, captured.Completion.RuntimeIdentifier,
            captured.Completion.HasUnfingerprintedSourceInputs, cancellationToken, out rejected);
        if (currentCompletion is null)
        {
            return rejected;
        }

        if (!AuthorizationMatches(captured.Completion, currentCompletion))
        {
            return new Outcome(AnalysisCacheRejectReason.InputChangedDuringExecution,
                currentCompletion.ProjectManifests.Count,
                currentCompletion.ProjectManifests.Count(manifest => manifest.Eligibility != CacheEligibility.VerifiedCacheEligible),
                0);
        }

        return Put(captured.Completion, AnalysisCacheOutcomeMapper.ToCacheOutcome(outcome), cancellationToken);
    }

    private static PreparedAuthorization? Prepare(
        AnalysisCacheLocation? location,
        AnalysisCacheKey key,
        IReadOnlyList<string> discoveredProjectPaths,
        IReadOnlyList<string> artifactPaths,
        string repositoryRoot,
        string? configuration,
        string? targetFramework,
        string? platform,
        string? runtimeIdentifier,
        bool hasUnfingerprintedSourceInputs,
        CancellationToken cancellationToken,
        out Outcome rejected)
    {
        if (location is null)
        {
            rejected = new Outcome(AnalysisCacheRejectReason.Disabled, 0, 0, 0, PopulationAttempted: false);
            return null;
        }

        if (hasUnfingerprintedSourceInputs || discoveredProjectPaths.Count == 0)
        {
            rejected = new Outcome(AnalysisCacheRejectReason.IneligibleBuildInput, 0, 0, 0);
            return null;
        }

        List<AnalysisCacheProjectManifest> manifests = new(discoveredProjectPaths.Count);
        try
        {
            foreach (string projectPath in discoveredProjectPaths)
            {
                cancellationToken.ThrowIfCancellationRequested();
                EvaluatedBuildInputManifestV1 manifest = CollectManifest(
                    projectPath, repositoryRoot, configuration, targetFramework, platform, runtimeIdentifier, cancellationToken);
                manifests.Add(AnalysisCacheProjectManifest.FromManifest(
                    ToRepositoryRelative(projectPath, repositoryRoot), manifest));
            }

            List<AnalysisCacheArtifactManifest> artifacts = artifactPaths
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(path => AnalysisCacheArtifactManifest.FromPath(path, repositoryRoot, cancellationToken))
                .OrderBy(manifest => manifest.ArtifactPath, StringComparer.Ordinal)
                .ToList();

            int ineligible = manifests.Count(manifest => manifest.Eligibility != CacheEligibility.VerifiedCacheEligible);
            if (ineligible > 0)
            {
                rejected = new Outcome(AnalysisCacheRejectReason.IneligibleBuildInput, manifests.Count, ineligible, 0);
                return null;
            }

            rejected = default;
            return new PreparedAuthorization(
                location, key, discoveredProjectPaths.ToArray(), artifactPaths.ToArray(), repositoryRoot,
                configuration, targetFramework, platform, runtimeIdentifier, hasUnfingerprintedSourceInputs,
                manifests, artifacts);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            rejected = new Outcome(AnalysisCacheRejectReason.IneligibleBuildInput, manifests.Count, manifests.Count, 0);
            return null;
        }
    }

    private static Outcome Put(PreparedAuthorization authorization, AnalysisCacheOutcomeV1 outcome, CancellationToken cancellationToken)
    {
        AnalysisCacheStore.PutResult putResult = AnalysisCacheStore.Put(
            authorization.Location, authorization.Key, authorization.ProjectManifests,
            authorization.ArtifactManifests, outcome, cancellationToken);
        int ineligible = authorization.ProjectManifests.Count(manifest =>
            manifest.Eligibility != CacheEligibility.VerifiedCacheEligible);
        return new Outcome(putResult.RejectReason, authorization.ProjectManifests.Count, ineligible, putResult.BytesWritten);
    }

    private static bool AuthorizationMatches(PreparedAuthorization captured, PreparedAuthorization current) =>
        captured.ProjectManifests.OrderBy(manifest => manifest.ProjectPath, StringComparer.Ordinal)
            .SequenceEqual(current.ProjectManifests.OrderBy(manifest => manifest.ProjectPath, StringComparer.Ordinal))
        && captured.ArtifactManifests.OrderBy(manifest => manifest.ArtifactPath, StringComparer.Ordinal)
            .SequenceEqual(current.ArtifactManifests.OrderBy(manifest => manifest.ArtifactPath, StringComparer.Ordinal));

    private static string ToRepositoryRelative(string projectPath, string repositoryRoot) =>
        BuildStateCanonicalHasher.ToRepositoryRelativePath(projectPath, repositoryRoot);
}
