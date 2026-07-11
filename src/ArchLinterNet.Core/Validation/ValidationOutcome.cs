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
    IReadOnlyCollection<ArchitectureClassificationMetadataFailure> ClassificationMetadataFailures,
    IReadOnlyCollection<ArchitectureClassificationRoleFact> ClassificationRoles);
