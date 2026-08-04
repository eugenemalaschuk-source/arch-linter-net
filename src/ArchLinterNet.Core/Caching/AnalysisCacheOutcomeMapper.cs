using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;
using ArchLinterNet.Core.Validation;

namespace ArchLinterNet.Core.Caching;

// Maps between the live ValidationOutcome the pipeline produces and the persisted
// AnalysisCacheOutcomeV1 shape — the seam ArchitectureAnalysisSnapshot's cache short-circuit uses
// on both sides: ToCacheOutcome after a completed run (population), FromCacheOutcome on a hit
// (reconstruction).
public static class AnalysisCacheOutcomeMapper
{
    public static AnalysisCacheOutcomeV1 ToCacheOutcome(ValidationOutcome outcome)
    {
        ArgumentNullException.ThrowIfNull(outcome);
        return new AnalysisCacheOutcomeV1(
            outcome.Passed,
            outcome.Violations.ToArray(),
            outcome.Cycles.ToArray(),
            outcome.CoverageFindings.ToArray(),
            outcome.CoverageConfig,
            outcome.UnmatchedIgnoredViolations.ToArray(),
            outcome.UnmatchedIgnoredViolationsConfig,
            outcome.PolicyConsistencyFindings.ToArray(),
            outcome.PolicyConsistencyConfig,
            outcome.ClassificationConflicts.ToArray(),
            outcome.ClassificationMetadataFailures.ToArray(),
            outcome.ClassificationRoles.ToArray(),
            outcome.ClassificationPathDeferred,
            outcome.CycleFindings.ToArray(),
            outcome.CoverageSummaries.ToArray(),
            outcome.SubtractiveMatcherParticipation.ToArray());
    }

    // Reconstructs a ValidationOutcome from a cache hit. PolicyImportPaths/ResolvedAssemblyPaths/
    // DiscoveredProjectPaths/RepositoryRoot/SourceExpansion are supplied fresh by the caller (they
    // come from this run's own already-completed policy composition and project discovery — see
    // ArchitectureAnalysisSnapshot — not from the cached entry itself, since they are portable
    // run metadata rather than analysis results). PreflightBlocked is always false for a
    // reconstructed outcome: AnalysisCachePopulation never persists an entry for a run whose
    // discovered projects are not all #406 VerifiedCacheEligible, and a preflight-blocked run's
    // project set cannot itself be verified current.
    public static ValidationOutcome FromCacheOutcome(
        AnalysisCacheOutcomeV1 cached,
        string repositoryRoot,
        IReadOnlyList<string> policyImportPaths,
        IReadOnlyList<string> resolvedAssemblyPaths,
        IReadOnlyList<string> discoveredProjectPaths,
        ArchitectureSourceExpansionInventory sourceExpansion)
    {
        ArgumentNullException.ThrowIfNull(cached);

        return new ValidationOutcome(
            cached.Passed,
            cached.Violations.ToArray(),
            cached.Cycles.ToArray(),
            cached.CoverageFindings.ToArray(),
            cached.CoverageConfig,
            cached.UnmatchedIgnoredViolations.ToArray(),
            cached.UnmatchedIgnoredViolationsConfig,
            cached.PolicyConsistencyFindings.ToArray(),
            cached.PolicyConsistencyConfig,
            cached.CoverageSummaries.ToArray(),
            cached.ClassificationConflicts.ToArray(),
            cached.ClassificationMetadataFailures.ToArray())
        {
            RepositoryRoot = repositoryRoot,
            PolicyImportPaths = policyImportPaths,
            ResolvedAssemblyPaths = resolvedAssemblyPaths,
            DiscoveredProjectPaths = discoveredProjectPaths,
            SourceExpansion = sourceExpansion,
            PreflightBlocked = false,
            ClassificationRoles = cached.ClassificationRoles.ToArray(),
            ClassificationPathDeferred = cached.ClassificationPathDeferred,
            CycleFindings = cached.CycleFindings.ToArray(),
            SubtractiveMatcherParticipation = cached.SubtractiveMatcherParticipation.ToArray(),
        };
    }
}
