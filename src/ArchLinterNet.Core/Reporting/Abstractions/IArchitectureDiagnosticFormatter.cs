using ArchLinterNet.Core.Model;

namespace ArchLinterNet.Core.Reporting.Abstractions;

public partial interface IArchitectureDiagnosticFormatter
{
    string FormatViolationsForHumans(IReadOnlyCollection<ArchitectureViolation> violations);

    /// <summary>
    /// Additive overload that preserves compatibility for existing formatter implementations;
    /// the default implementation observes cancellation before delegating to the original member.
    /// </summary>
    string FormatViolationsForHumans(IReadOnlyCollection<ArchitectureViolation> violations, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return FormatViolationsForHumans(violations);
    }

    string FormatCyclesForHumans(IReadOnlyCollection<string> cycles);

    string FormatUnmatchedForHumans(IReadOnlyCollection<ArchitectureUnmatchedIgnoredViolation> unmatched);

    string FormatPolicyConsistencyForHumans(IReadOnlyCollection<PolicyConsistencyDiagnostic> findings);

    string FormatCoverageForHumans(IReadOnlyCollection<ArchitectureViolation> findings);

    /// <summary>
    /// Cancellation-aware additive overload. Existing formatter implementations retain the
    /// original member while the concrete formatter can observe cancellation per finding.
    /// </summary>
    string FormatCoverageForHumans(IReadOnlyCollection<ArchitectureViolation> findings, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return FormatCoverageForHumans(findings);
    }

    string FormatCoverageSummaryForHumans(IReadOnlyCollection<ArchitectureCoverageSummary> summaries);

    string FormatClassificationFactsForHumans(
        IReadOnlyCollection<ArchitectureClassificationConflict> conflicts,
        IReadOnlyCollection<ArchitectureClassificationMetadataFailure> metadataFailures);

    /// <summary>
    /// Additive overload which keeps the legacy two-parameter member unambiguous and preserves
    /// compatibility for formatter implementations that predate path-deferred diagnostics.
    /// </summary>
    string FormatClassificationFactsForHumans(
        IReadOnlyCollection<ArchitectureClassificationConflict> conflicts,
        IReadOnlyCollection<ArchitectureClassificationMetadataFailure> metadataFailures,
        ArchitectureClassificationPathDeferredNotice? classificationPathDeferred)
        => FormatClassificationFactsForHumans(conflicts, metadataFailures);

    /// <summary>
    /// Additive overload carrying classification roles while preserving the original CI payload
    /// member for existing implementations.
    /// </summary>
    string FormatResultForCiArtifacts( // NOSONAR: each parameter represents a semantically distinct section of the CI artifact payload; grouping would obscure the data contract
        string mode,
        bool passed,
        IReadOnlyCollection<ArchitectureViolation> violations,
        IReadOnlyCollection<string> cycles,
        IReadOnlyCollection<ArchitectureViolation>? coverageFindings = null,
        IReadOnlyCollection<ArchitectureUnmatchedIgnoredViolation>? unmatched = null,
        IReadOnlyCollection<PolicyConsistencyDiagnostic>? policyConsistencyFindings = null,
        IReadOnlyCollection<ArchitectureCoverageSummary>? coverageSummaries = null,
        IReadOnlyCollection<ArchitectureClassificationConflict>? classificationConflicts = null,
        IReadOnlyCollection<ArchitectureClassificationMetadataFailure>? classificationMetadataFailures = null);

    /// <summary>
    /// Additive overload carrying the optional path-deferred notice without changing previous
    /// formatter contracts.
    /// </summary>
    string FormatResultForCiArtifacts( // NOSONAR: each parameter represents a semantically distinct section of the CI artifact payload; grouping would obscure the data contract
        string mode,
        bool passed,
        IReadOnlyCollection<ArchitectureViolation> violations,
        IReadOnlyCollection<string> cycles,
        IReadOnlyCollection<ArchitectureClassificationRoleFact> classificationRoles,
        IReadOnlyCollection<ArchitectureViolation>? coverageFindings = null,
        IReadOnlyCollection<ArchitectureUnmatchedIgnoredViolation>? unmatched = null,
        IReadOnlyCollection<PolicyConsistencyDiagnostic>? policyConsistencyFindings = null,
        IReadOnlyCollection<ArchitectureCoverageSummary>? coverageSummaries = null,
        IReadOnlyCollection<ArchitectureClassificationConflict>? classificationConflicts = null,
        IReadOnlyCollection<ArchitectureClassificationMetadataFailure>? classificationMetadataFailures = null)
        => FormatResultForCiArtifacts(
            mode, passed, violations, cycles, coverageFindings, unmatched,
            policyConsistencyFindings, coverageSummaries, classificationConflicts, classificationMetadataFailures);

    string FormatResultForCiArtifacts( // NOSONAR: each parameter represents a semantically distinct section of the CI artifact payload; grouping would obscure the data contract
        string mode,
        bool passed,
        IReadOnlyCollection<ArchitectureViolation> violations,
        IReadOnlyCollection<string> cycles,
        IReadOnlyCollection<ArchitectureClassificationRoleFact> classificationRoles,
        ArchitectureClassificationPathDeferredNotice? classificationPathDeferred,
        IReadOnlyCollection<ArchitectureViolation>? coverageFindings = null,
        IReadOnlyCollection<ArchitectureUnmatchedIgnoredViolation>? unmatched = null,
        IReadOnlyCollection<PolicyConsistencyDiagnostic>? policyConsistencyFindings = null,
        IReadOnlyCollection<ArchitectureCoverageSummary>? coverageSummaries = null,
        IReadOnlyCollection<ArchitectureClassificationConflict>? classificationConflicts = null,
        IReadOnlyCollection<ArchitectureClassificationMetadataFailure>? classificationMetadataFailures = null)
        => FormatResultForCiArtifacts(
            mode, passed, violations, cycles, classificationRoles, coverageFindings, unmatched,
            policyConsistencyFindings, coverageSummaries, classificationConflicts, classificationMetadataFailures);

    string FormatViolationsForCiArtifacts(
        string contractName,
        string? contractId,
        IReadOnlyCollection<ArchitectureViolation> violations);

    /// <summary>
    /// Compatibility overload for implementations that predate cancellation-aware CI rendering.
    /// </summary>
    string FormatViolationsForCiArtifacts(
        string contractName,
        string? contractId,
        IReadOnlyCollection<ArchitectureViolation> violations,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return FormatViolationsForCiArtifacts(contractName, contractId, violations);
    }

    string FormatCyclesForCiArtifacts(
        string contractName,
        string? contractId,
        IReadOnlyCollection<string> cycles);
}
