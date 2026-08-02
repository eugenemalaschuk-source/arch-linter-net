using ArchLinterNet.Core.Profiling;
using ArchLinterNet.Core.Reporting;
using ArchLinterNet.Core.Validation;

namespace ArchLinterNet.Testing;

// Shared Core ValidationOutcome -> Testing ArchitectureValidationResult mapping used by both the
// independent-run path (ArchitectureValidationBuilder.Validate) and the shared-snapshot path
// (ArchitectureValidationSnapshotSession) so both produce identically-shaped results.
internal static class ArchitectureValidationResultMapper
{
    public static ArchitectureValidationResult ToResult(
        ValidationOutcome outcome, ValidationTiming? timing, string mode, AnalysisProfile? profile = null)
    {
        return new ArchitectureValidationResult(new ArchitectureValidationResultParams(
            outcome.Passed,
            outcome.Violations,
            outcome.Cycles,
            outcome.PolicyConsistencyFindings,
            outcome.PolicyConsistencyConfig,
            outcome.CoverageFindings,
            outcome.CoverageConfig,
            outcome.UnmatchedIgnoredViolations,
            outcome.UnmatchedIgnoredViolationsConfig,
            outcome.CoverageSummaries,
            timing)
        {
            CycleFindings = outcome.CycleFindings,
            PreflightDiagnostics = outcome.PreflightDiagnostics,
            PreflightBlocked = outcome.PreflightBlocked,
            Mode = mode,
            SubtractiveMatcherParticipation = outcome.SubtractiveMatcherParticipation,
            Profile = profile,
        });
    }
}
