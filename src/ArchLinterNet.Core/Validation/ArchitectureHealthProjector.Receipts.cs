using ArchLinterNet.Core.Model;

namespace ArchLinterNet.Core.Validation;

internal static class ArchitectureHealthReceiptProjector
{
    private const string AuditEvidence = "audit_evidence";
    private const string Coverage = "coverage";
    private const string TopologyFamily = "declared_topology";
    private const string MetricBudgetsFamily = "metric_budgets";
    private const string ExternalEvidenceFamily = "external_diagnostics";

    internal static ArchitectureHealthDimension ProjectCoverage(
        IReadOnlyList<ArchitectureHealthValidationOutcome> outcomes)
    {
        ArchitectureHealthValidationOutcome[] configured = outcomes
            .Where(outcome => outcome.Outcome.CoverageSummaries.Count > 0
                || outcome.Outcome.CoverageFindings.Count > 0)
            .ToArray();
        if (configured.Length == 0)
        {
            return Dimension(Coverage, ArchitectureHealthDimensionState.NotConfigured);
        }

        if (configured.Any(outcome => outcome.Outcome.CoverageSummaries.Any(summary =>
            summary.Counts.Stale > 0 || summary.Counts.Unknown > 0)))
        {
            return Dimension(Coverage, ArchitectureHealthDimensionState.Unassessable, "coverage_incomplete");
        }

        ArchitectureHealthValidationOutcome[] blocking = configured
            .Where(outcome => string.Equals(outcome.Outcome.CoverageConfig, "error", StringComparison.Ordinal)
                && outcome.Outcome.CoverageFindings.Count > 0)
            .ToArray();
        if (blocking.Length > 0)
        {
            return Dimension(Coverage, ArchitectureHealthDimensionState.Fail,
                blocking.SelectMany(outcome => outcome.Outcome.CoverageFindings)
                    .Select(CoverageReason));
        }

        ArchitectureHealthValidationOutcome[] reportable = configured
            .Where(outcome => outcome.Outcome.CoverageFindings.Count > 0)
            .ToArray();
        return reportable.Length > 0
            ? Dimension(Coverage, ArchitectureHealthDimensionState.Degrading,
                reportable.SelectMany(outcome => outcome.Outcome.CoverageFindings)
                    .Select(CoverageReason))
            : Dimension(Coverage, ArchitectureHealthDimensionState.Pass);
    }

    internal static ArchitectureHealthDimension ProjectAuditEvidence(
        IReadOnlyList<ArchitectureHealthValidationOutcome> outcomes)
    {
        ArchitectureHealthValidationOutcome[] auditOutcomes = outcomes
            .Where(outcome => string.Equals(outcome.Mode, "audit", StringComparison.Ordinal))
            .ToArray();
        if (auditOutcomes.Length == 0)
        {
            return Dimension(AuditEvidence, ArchitectureHealthDimensionState.NotConfigured);
        }

        ArchitectureHealthReason[] reasons = auditOutcomes
            .SelectMany(outcome => outcome.Outcome.Violations.Select(AuditViolationReason)
                .Concat(outcome.Outcome.Cycles.Select(AuditCycleReason))
                .Concat(outcome.Outcome.ImportedDiagnosticFindings.Select(AuditImportedDiagnosticReason)))
            .ToArray();
        return reasons.Length > 0
            ? Dimension(AuditEvidence, ArchitectureHealthDimensionState.Degrading, reasons)
            : Dimension(AuditEvidence, ArchitectureHealthDimensionState.Pass);
    }

    private static ArchitectureHealthReason CoverageReason(ArchitectureViolation violation) =>
        Reason(
            "coverage_finding",
            Coverage,
            "coverage",
            violation.ContractId ?? violation.ContractName,
            PolicyIdentity(violation.PolicyLocation),
            EvidenceIdentity(violation));

    private static ArchitectureHealthReason AuditViolationReason(ArchitectureViolation violation) =>
        Reason(
            "audit_validation_finding",
            AuditEvidence,
            AuditFamily(violation),
            violation.ContractId ?? violation.ContractName,
            PolicyIdentity(violation.PolicyLocation),
            EvidenceIdentity(violation));

    private static ArchitectureHealthReason AuditCycleReason(string cycle) =>
        Reason("audit_cycle", AuditEvidence, "cycles", evidenceIdentity: cycle);

    private static ArchitectureHealthReason AuditImportedDiagnosticReason(ArchitectureFinding finding)
    {
        string evidenceIdentity = finding.Details is ImportedExternalDiagnostic imported
            ? imported.SelectedCanonicalIdentity
            : finding.CanonicalIdentity;
        return Reason(
            "audit_imported_external_diagnostic",
            AuditEvidence,
            ExternalEvidenceFamily,
            finding.ContractId,
            PolicyIdentity(finding.PolicyLocation),
            evidenceIdentity);
    }

    private static string AuditFamily(ArchitectureViolation violation) => violation.Payload switch
    {
        MetricBudgetPayload => MetricBudgetsFamily,
        _ when string.Equals(violation.ContractId, "declared-topology", StringComparison.Ordinal) => TopologyFamily,
        _ => "validation",
    };

    private static ArchitectureHealthDimension Dimension(
        string name,
        ArchitectureHealthDimensionState state,
        params string[] reasonCodes) =>
        Dimension(name, state, reasonCodes.Select(code => Reason(code, name)));

    private static ArchitectureHealthDimension Dimension(
        string name,
        ArchitectureHealthDimensionState state,
        IEnumerable<ArchitectureHealthReason> reasons) =>
        new(
            name,
            state,
            reasons
                .Where(reason => !string.IsNullOrWhiteSpace(reason.Code))
                .Distinct()
                .ToArray());

    private static ArchitectureHealthReason Reason(
        string code,
        string source,
        string? family = null,
        string? controlIdentity = null,
        string? policyIdentity = null,
        string? evidenceIdentity = null) =>
        new(code, source)
        {
            Family = family,
            ControlIdentity = controlIdentity,
            PolicyIdentity = policyIdentity,
            EvidenceIdentity = evidenceIdentity,
        };

    private static string? PolicyIdentity(ArchitecturePolicySourceLocation? location) => location is null
        ? null
        : $"{location.SourcePath}:{location.YamlPath}";

    private static string EvidenceIdentity(ArchitectureViolation violation) => violation.Identity is not null
        ? ArchitectureViolationIdentityJson.Serialize(violation.Identity)
        : string.Join(
            "|",
            violation.ContractId ?? violation.ContractName,
            violation.SourceType,
            violation.ForbiddenNamespace,
            string.Join(",", violation.ForbiddenReferences.OrderBy(reference => reference, StringComparer.Ordinal)));
}
