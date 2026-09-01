using ArchLinterNet.Core.Model;

namespace ArchLinterNet.Cli.Commands.Report.Application;

internal static partial class PrReportMarkdownRenderer
{
    private static ArchitecturePrReportValidationReceipt? PrimaryReceipt(ArchitecturePrReportProjection projection) =>
        projection.Evidence?.ValidationOutcomes
            .OrderByDescending(item => string.Equals(item.Mode, "strict", StringComparison.Ordinal))
            .ThenBy(item => item.Mode, StringComparer.Ordinal)
            .FirstOrDefault();

    private static IEnumerable<ArchitecturePrReportFinding> AllFindings(ArchitecturePrReportEvidence evidence) =>
        evidence.ValidationOutcomes.SelectMany(item => item.Findings)
            .Concat(evidence.ValidationOutcomes.SelectMany(item => item.ExternalEvidence?.Findings ?? Array.Empty<ArchitecturePrReportFinding>()))
            .Concat(evidence.DebtGate.PersistentDebt.ConfigurationViolations)
            .Concat(evidence.DebtGate.Evaluation.PreflightDiagnostics)
            .DistinctBy(item => item.CanonicalIdentity, StringComparer.Ordinal);

    private static string ApplicabilityHeadline(ArchitecturePrReportProjection projection)
    {
        ArchitecturePrReportApplicability? applicability = PrimaryReceipt(projection)?.Applicability;
        return applicability is null
            ? "`unavailable`"
            : $"`{Safe(applicability.State)}` — {applicability.Summary.RequiredEvaluable}/{applicability.Summary.Required} evaluable";
    }

    private static string TopologyHeadline(ArchitecturePrReportProjection projection)
    {
        ArchitecturePrReportApplicability? applicability = PrimaryReceipt(projection)?.Applicability;
        ArchitecturePrReportTopology[] topologies = applicability?.Controls
            .Where(item => item.Record?.Topology is not null)
            .Select(item => item.Record!.Topology!)
            .ToArray() ?? Array.Empty<ArchitecturePrReportTopology>();
        if (topologies.Length == 0)
        {
            return $"`{DimensionToken(projection, "topology")}`";
        }

        return $"`{DimensionToken(projection, "topology")}` — {topologies.Sum(item => item.Counts.Mapped)} mapped, " +
            $"{topologies.Sum(item => item.Counts.Unmapped)} unmapped, {topologies.Sum(item => item.Counts.Ambiguous)} ambiguous";
    }

    private static string WaiverHeadline(ArchitecturePrReportProjection projection)
    {
        ArchitecturePolicyInventoryIgnoreDebt? debt = PrimaryReceipt(projection)?.PolicyInventory?.IgnoreDebt;
        return debt is null ? "`unavailable`" : $"`{debt.Total}` total (`{debt.Active}` active, `{debt.Stale}` stale, `{debt.Expired}` expired)";
    }

    private static string ExistingDebtHeadline(ArchitecturePrReportProjection projection)
    {
        int count = projection.Evidence?.DebtGate.PersistentDebt.Entries.Count ?? 0;
        return projection.Evidence is null ? "`unavailable`" : $"`{count}` baseline entries";
    }

    private static string NewDebtHeadline(ArchitecturePrReportProjection projection)
    {
        if (projection.Evidence is null)
        {
            return "`unavailable`";
        }

        int count = projection.Evidence.DebtGate.PersistentDebt.Entries.Count(item =>
            string.Equals(item.Status, "new", StringComparison.Ordinal)
            || string.Equals(item.Disposition, "new", StringComparison.Ordinal));
        return $"`{count}` new baseline entries";
    }

    private static string PolicyWeakeningHeadline(ArchitecturePrReportProjection projection)
    {
        if (projection.Evidence?.DebtGate.PolicyWeakening is null)
        {
            return projection.Evidence is null ? "`unavailable`" : "`not_configured`";
        }

        ArchitecturePrReportPolicyWeakening weakening = projection.Evidence.DebtGate.PolicyWeakening;
        return $"`{weakening.Findings.Count}` finding(s) ({Safe(weakening.Severity)})";
    }

    private static string ExternalHeadline(ArchitecturePrReportProjection projection)
    {
        ArchitecturePrReportValidationReceipt? receipt = PrimaryReceipt(projection);
        if (receipt?.ExternalEvidence is null)
        {
            return $"`{DimensionToken(projection, "external_evidence")}`";
        }

        ArchitecturePrReportExternalEvidence external = receipt.ExternalEvidence;
        return $"`{DimensionToken(projection, "external_evidence")}` — {external.Requirements.Count} requirement(s), {external.Findings.Count} finding(s)";
    }

    private static string DimensionToken(ArchitecturePrReportProjection projection, string name) =>
        DimensionToken(projection.Headline.Dimensions.FirstOrDefault(item => string.Equals(item.Name, name, StringComparison.Ordinal))?.State);

    private static string DimensionToken(ArchitectureHealthDimensionState? state) => state switch
    {
        ArchitectureHealthDimensionState.Pass => "pass",
        ArchitectureHealthDimensionState.Fail => "fail",
        ArchitectureHealthDimensionState.Debt => "debt",
        ArchitectureHealthDimensionState.Degrading => "degrading",
        ArchitectureHealthDimensionState.Unassessable => "unassessable",
        ArchitectureHealthDimensionState.NotConfigured => "not_configured",
        ArchitectureHealthDimensionState.NotApplicable => "not_applicable",
        _ => "unavailable",
    };

    private static string GateToken(ArchitectureHealthGate gate) => gate switch
    {
        ArchitectureHealthGate.Pass => "pass",
        ArchitectureHealthGate.Fail => "fail",
        ArchitectureHealthGate.Unassessable => "unassessable",
        _ => "unavailable",
    };

    private static string HealthToken(ArchitectureHealthState health) => health switch
    {
        ArchitectureHealthState.Healthy => "healthy",
        ArchitectureHealthState.Debt => "debt",
        ArchitectureHealthState.Degrading => "degrading",
        ArchitectureHealthState.Failing => "failing",
        ArchitectureHealthState.Unassessable => "unassessable",
        _ => "unavailable",
    };

    private static string AvailabilityToken(ArchitecturePrReportAvailability availability) => availability switch
    {
        ArchitecturePrReportAvailability.Complete => "complete",
        ArchitecturePrReportAvailability.Unavailable => "unavailable",
        ArchitecturePrReportAvailability.Unassessable => "unassessable",
        _ => "unavailable",
    };

    private static string FormatWaiver(ArchitectureWaiverLifecycleRecord waiver) =>
        $"`{Safe(waiver.Id)}` state=`{Safe(waiver.State)}` rule=`{Safe(waiver.ContractId ?? waiver.ContractName)}` " +
        $"target=`{Safe(waiver.ForbiddenReference)}` reason={Safe(waiver.Reason)} owner={Safe(waiver.Owner ?? "-")} " +
        $"issue={Safe(waiver.Issue ?? "-")} expires={Safe(waiver.Expires?.ToString("yyyy-MM-dd") ?? "-")}";

    private static string FormatBaseline(ArchitecturePrReportBaselineEntry entry) =>
        $"`{Safe(entry.Identity ?? entry.ContractId)}` status=`{Safe(entry.Status)}` {Safe(entry.ContractGroup)} " +
        $"{Safe(entry.SourceType)} → {Safe(entry.ForbiddenReference)}";

    private static string FormatApplicabilityControl(ArchitecturePrReportApplicabilityControl control) =>
        $"`{Safe(control.ControlIdentity)}` state=`{Safe(control.State)}` membership=`{Safe(control.Membership ?? "-")}` " +
        (control.IntegrityValid ? "integrity=valid" : "integrity=invalid");

    private static string FormatApplicabilityReason(ArchitecturePrReportApplicabilityReason reason) =>
        $"`{Safe(reason.Code)}`{FormatProvenance(reason.Provenance)}";

    private static string FormatTopologySubject(string control, ArchitecturePrReportTopologySubject subject) =>
        $"control=`{Safe(control)}` `{Safe(subject.Identity)}` {Safe(subject.Project)} / {Safe(subject.Assembly)} " +
        $"subject={Safe(subject.Subject)} disposition=`{Safe(subject.Disposition)}`";

    private static string FormatExternalRequirement(ArchitecturePrReportExternalRequirement requirement) =>
        $"`{Safe(requirement.Id)}` tool=`{Safe(requirement.Tool)}` format=`{Safe(requirement.Format)}` " +
        $"required={requirement.Required} run=`{Safe(requirement.Run)}`";

    private static string FormatFinding(ArchitecturePrReportFinding finding) =>
        $"`{Safe(finding.CanonicalIdentity)}` {Safe(finding.MessageCode)} ({Safe(finding.ContractId ?? finding.ContractName)})";

    private static string FormatRemediation(ArchitecturePrReportFinding finding)
    {
        ArchitecturePrReportRemediation remediation = finding.Remediation!;
        return $"`{Safe(finding.CanonicalIdentity)}` category=`{Safe(remediation.Category)}` {Safe(remediation.Summary)}";
    }

    private static string FormatReasonIdentity(ArchitectureHealthReason reason)
    {
        string identity = reason.EvidenceIdentity ?? reason.ControlIdentity ?? reason.PolicyIdentity ?? string.Empty;
        return string.IsNullOrWhiteSpace(identity) ? string.Empty : $" (`{Safe(identity)}`)";
    }

    private static string FormatProvenance(ArchitecturePrReportProvenanceReference provenance)
    {
        string?[] values =
        [
            provenance.Family,
            provenance.ControlIdentity,
            provenance.PolicyIdentity,
            provenance.EvidenceIdentity,
        ];
        return values.Any(value => !string.IsNullOrWhiteSpace(value))
            ? $" ({string.Join(", ", values.Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => $"`{Safe(value!)}`"))})"
            : string.Empty;
    }

    private static bool IsPolicyWeakeningBlocker(ArchitecturePrReportPolicyWeakeningFinding finding) =>
        string.Equals(finding.Classification, "new", StringComparison.OrdinalIgnoreCase)
        || string.Equals(finding.Classification, "broadened", StringComparison.OrdinalIgnoreCase)
        || string.Equals(finding.Kind, "new_waiver", StringComparison.OrdinalIgnoreCase)
        || string.Equals(finding.Kind, "broadened_waiver", StringComparison.OrdinalIgnoreCase);

    private static string Safe(string value) =>
        value.Replace("\r", " ", StringComparison.Ordinal).Replace("\n", " ", StringComparison.Ordinal).Trim();
}
