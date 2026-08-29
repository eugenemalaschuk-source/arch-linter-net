using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;

namespace ArchLinterNet.Core.Caching;

// Replaces the prior AnalysisCacheFactsV1 (a bare `Passed` boolean plus aggregate counts, which
// review finding #1 correctly identified as a final-result summary rather than a reusable fact
// set: "A hit cannot reconstruct canonical findings, identity, ordering, or exit category").
//
// This is the real, per-mode reusable payload: enough of ValidationOutcome's own fields to
// reconstruct byte-identical Violations (including Payload via AnalysisCacheDiagnosticPayloadConverter's
// closed-set discrimination, and baseline Identity/Identities), Cycles, UnmatchedIgnoredViolations,
// PolicyConsistencyFindings, ClassificationConflicts/MetadataFailures, and Passed on a cache hit —
// see AnalysisCacheOutcomeMapper for the ValidationOutcome <-> AnalysisCacheOutcomeV1 mapping used
// by the ArchitectureAnalysisSnapshot short-circuit seam.
//
// Finding #6: a cache hit was not equivalent to the uncached result — this envelope originally
// omitted ValidationOutcome.CycleFindings (distinct from the plain-string Cycles above; exposed by
// the Testing result mapper and used as structured finding evidence), ClassificationRoles,
// ClassificationPathDeferred, CoverageSummaries, and SubtractiveMatcherParticipation. All five are
// now carried below and round-tripped by AnalysisCacheOutcomeMapper.
//
// Still deliberately out of scope for this entry (disclosed, not silently dropped — see design.md
// "Cache boundary decision, v2"): RepositoryRoot/PolicyImportPaths/ResolvedAssemblyPaths/
// DiscoveredProjectPaths/SourceExpansion (inputs re-supplied by the live run context on
// reconstruction — see AnalysisCacheOutcomeMapper.FromCacheOutcome's own parameters, not results)
// and PreflightDiagnostics/PreflightBlocked (population only ever happens after a completed
// non-preflight-blocked run, so there is never a preflight diagnostic to cache in the first place).
public sealed record AnalysisCacheOutcomeV1(
    bool Passed,
    IReadOnlyList<ArchitectureViolation> Violations,
    IReadOnlyList<string> Cycles,
    IReadOnlyList<ArchitectureViolation> CoverageFindings,
    string CoverageConfig,
    IReadOnlyList<ArchitectureUnmatchedIgnoredViolation> UnmatchedIgnoredViolations,
    string UnmatchedIgnoredViolationsConfig,
    IReadOnlyList<PolicyConsistencyDiagnostic> PolicyConsistencyFindings,
    string PolicyConsistencyConfig,
    IReadOnlyList<ArchitectureClassificationConflict> ClassificationConflicts,
    IReadOnlyList<ArchitectureClassificationMetadataFailure> ClassificationMetadataFailures,
    IReadOnlyList<ArchitectureClassificationRoleFact>? ClassificationRoles = null,
    ArchitectureClassificationPathDeferredNotice? ClassificationPathDeferred = null,
    IReadOnlyList<ArchitectureCycleFinding>? CycleFindings = null,
    IReadOnlyList<ArchitectureCoverageSummary>? CoverageSummaries = null,
    IReadOnlyList<ArchitectureSubtractiveMatcherParticipation>? SubtractiveMatcherParticipation = null)
{
    public IReadOnlyList<ArchitectureClassificationRoleFact> ClassificationRoles { get; init; } =
        ClassificationRoles ?? Array.Empty<ArchitectureClassificationRoleFact>();

    public IReadOnlyList<ArchitectureCycleFinding> CycleFindings { get; init; } =
        CycleFindings ?? Array.Empty<ArchitectureCycleFinding>();

    public IReadOnlyList<ArchitectureCoverageSummary> CoverageSummaries { get; init; } =
        CoverageSummaries ?? Array.Empty<ArchitectureCoverageSummary>();

    public IReadOnlyList<ArchitectureSubtractiveMatcherParticipation> SubtractiveMatcherParticipation { get; init; } =
        SubtractiveMatcherParticipation ?? Array.Empty<ArchitectureSubtractiveMatcherParticipation>();

    public IReadOnlyList<ArchitectureWaiverLifecycleRecord> Waivers { get; init; } =
        Array.Empty<ArchitectureWaiverLifecycleRecord>();
}
