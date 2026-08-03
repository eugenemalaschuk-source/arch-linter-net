using ArchLinterNet.Core.Model;

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
// Deliberately out of scope for this entry (disclosed, not silently dropped — see design.md
// "Cache boundary decision, v2"): ArchitectureCoverageSummary (coverage-detail display),
// ArchitectureClassificationRoleFact / ArchitectureClassificationPathDeferredNotice (explain-only
// detail), ArchitectureSourceExpansionInventory and ArchitectureSubtractiveMatcherParticipation
// (source-set/matcher explain evidence), and BuildStatePreflightDiagnostic (preflight only ever ran
// against project/output facts already re-verified fresh by AnalysisCachePopulation's own
// eligibility gate, and a preflight-blocked run is never cache-eligible to begin with). None of
// these determine findings identity, ordering, or exit category — they are supplementary
// explain/report detail. A cache hit reconstructs them as empty/default; a future change can widen
// this envelope (bumping AnalysisCacheEnvelope.FormatVersion) if a caller needs them restored too.
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
    IReadOnlyList<ArchitectureClassificationMetadataFailure> ClassificationMetadataFailures);
