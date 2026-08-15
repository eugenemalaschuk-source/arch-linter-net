using ArchLinterNet.Core.Model;

namespace ArchLinterNet.Core.Reporting.Abstractions;

public partial interface IArchitectureDiagnosticFormatter
{
    /// <summary>
    /// Additive build-state overload. The required preflight argument keeps it unambiguous against
    /// prior overloads and lets existing implementations retain their earlier contracts.
    /// </summary>
    string FormatResultForCiArtifacts( // NOSONAR: each parameter represents a semantically distinct section of the CI artifact payload; grouping would obscure the data contract
        string mode,
        bool passed,
        IReadOnlyCollection<ArchitectureViolation> violations,
        IReadOnlyCollection<string> cycles,
        IReadOnlyCollection<ArchitectureClassificationRoleFact> classificationRoles,
        ArchitectureClassificationPathDeferredNotice? classificationPathDeferred,
        IReadOnlyCollection<BuildStatePreflightDiagnostic> preflightDiagnostics,
        IReadOnlyCollection<ArchitectureViolation>? coverageFindings = null,
        IReadOnlyCollection<ArchitectureUnmatchedIgnoredViolation>? unmatched = null,
        IReadOnlyCollection<PolicyConsistencyDiagnostic>? policyConsistencyFindings = null,
        IReadOnlyCollection<ArchitectureCoverageSummary>? coverageSummaries = null,
        IReadOnlyCollection<ArchitectureClassificationConflict>? classificationConflicts = null,
        IReadOnlyCollection<ArchitectureClassificationMetadataFailure>? classificationMetadataFailures = null)
        => FormatResultForCiArtifacts(
            mode, passed, violations, cycles, classificationRoles, classificationPathDeferred, coverageFindings,
            unmatched, policyConsistencyFindings, coverageSummaries, classificationConflicts, classificationMetadataFailures);

    /// <summary>
    /// Default implementation keeps existing formatter implementations source-compatible; the
    /// concrete formatter overrides it with actual build-state rendering.
    /// </summary>
    string FormatBuildStatePreflightForHumans(IReadOnlyCollection<BuildStatePreflightDiagnostic> diagnostics) => string.Empty;
}
