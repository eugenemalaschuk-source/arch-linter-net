using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;

namespace ArchLinterNet.Core.Validation;

public sealed record ValidationOutcome(
    bool Passed,
    IReadOnlyCollection<ArchitectureViolation> Violations,
    IReadOnlyCollection<string> Cycles,
    IReadOnlyCollection<ArchitectureViolation> CoverageFindings,
    string CoverageConfig,
    IReadOnlyList<ArchitectureUnmatchedIgnoredViolation> UnmatchedIgnoredViolations,
    string UnmatchedIgnoredViolationsConfig,
    IReadOnlyCollection<PolicyConsistencyDiagnostic> PolicyConsistencyFindings,
    string PolicyConsistencyConfig,
    IReadOnlyCollection<ArchitectureCoverageSummary> CoverageSummaries,
    IReadOnlyCollection<ArchitectureClassificationConflict> ClassificationConflicts,
    IReadOnlyCollection<ArchitectureClassificationMetadataFailure> ClassificationMetadataFailures)
{
    // Declared as an init-only property outside the primary constructor, not as a 13th positional
    // parameter, so existing positional `new ValidationOutcome(...)` call sites and Deconstruct
    // usages compiled against the prior (12-parameter) shape keep working unchanged; callers who
    // want discovered roles opt in via an object initializer.
    public IReadOnlyCollection<ArchitectureClassificationRoleFact> ClassificationRoles { get; init; } =
        Array.Empty<ArchitectureClassificationRoleFact>();

    // Non-null when the loaded policy declared a non-empty classification.path section — see
    // ArchitectureAnalysisSession.CheckClassificationPathDeferred.
    public ArchitectureClassificationPathDeferredNotice? ClassificationPathDeferred { get; init; }

    public IReadOnlyCollection<ArchitectureCycleFinding> CycleFindings { get; init; } =
        Array.Empty<ArchitectureCycleFinding>();
}
