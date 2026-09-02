using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;

namespace ArchLinterNet.Cli.Commands.Report.Application;

internal static class PrReportMarkdownFormatter
{
    internal static ArchitecturePrReportValidationReceipt? PrimaryReceipt(ArchitecturePrReportProjection projection) =>
        projection.Evidence?.ValidationOutcomes
            .SingleOrDefault(item => string.Equals(item.Mode, projection.Change.Mode, StringComparison.Ordinal));

    internal static IEnumerable<ArchitecturePrReportFinding> AllFindings(ArchitecturePrReportEvidence evidence) =>
        evidence.ValidationOutcomes.SelectMany(item => item.Findings)
            .Concat(evidence.ValidationOutcomes.SelectMany(item => item.ExternalEvidence?.Findings ?? Array.Empty<ArchitecturePrReportFinding>()))
            .Concat(evidence.DebtGate.PersistentDebt.ConfigurationViolations)
            .Concat(evidence.DebtGate.Evaluation.PreflightDiagnostics)
            .DistinctBy(item => item.CanonicalIdentity, StringComparer.Ordinal);

    internal static string ApplicabilityHeadline(ArchitecturePrReportProjection projection)
    {
        ArchitecturePrReportApplicability? applicability = PrimaryReceipt(projection)?.Applicability;
        return applicability is null
            ? "`unavailable`"
            : $"`{Inline(applicability.State)}` — {applicability.Summary.RequiredEvaluable}/{applicability.Summary.Required} evaluable";
    }

    internal static string TopologyHeadline(ArchitecturePrReportProjection projection)
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

    internal static string WaiverHeadline(ArchitecturePrReportProjection projection)
    {
        ArchitecturePolicyInventoryIgnoreDebt? debt = PrimaryReceipt(projection)?.PolicyInventory?.IgnoreDebt;
        return debt is null ? "`unavailable`" : $"`{debt.Total}` total (`{debt.Active}` active, `{debt.Stale}` stale, `{debt.Expired}` expired)";
    }

    internal static string ExistingDebtHeadline(ArchitecturePrReportProjection projection)
    {
        int count = projection.Evidence is null ? 0 : ExistingBaselineDebt(projection.Evidence).Count;
        return projection.Evidence is null ? "`unavailable`" : $"`{count}` baseline entries";
    }

    internal static string NewDebtHeadline(ArchitecturePrReportProjection projection)
    {
        if (projection.Evidence is null)
        {
            return "`unavailable`";
        }

        int count = projection.Evidence.DebtGate.PersistentDebt.Entries.Count(item =>
            string.Equals(item.Status, BaselineEntryLifecycleNames.New, StringComparison.Ordinal));
        return $"`{count}` new baseline entries";
    }

    internal static IReadOnlyList<ArchitecturePrReportBaselineEntry> ExistingBaselineDebt(
        ArchitecturePrReportEvidence evidence) =>
        evidence.DebtGate.PersistentDebt.Entries
            .Where(item => string.Equals(item.Status, BaselineEntryLifecycleNames.Matched, StringComparison.Ordinal))
            .ToArray();

    internal static IReadOnlyList<ArchitecturePrReportBaselineEntry> BlockingBaselineLifecycle(
        ArchitecturePrReportEvidence evidence) =>
        evidence.DebtGate.PersistentDebt.Entries
            .Where(item => !string.Equals(item.Status, BaselineEntryLifecycleNames.Matched, StringComparison.Ordinal))
            .ToArray();

    internal static string PolicyWeakeningHeadline(ArchitecturePrReportProjection projection)
    {
        if (projection.Evidence?.DebtGate.PolicyWeakening is null)
        {
            return projection.Evidence is null ? "`unavailable`" : "`not_configured`";
        }

        ArchitecturePrReportPolicyWeakening weakening = projection.Evidence.DebtGate.PolicyWeakening;
        return $"`{weakening.Findings.Count}` finding(s) ({Text(weakening.Severity)})";
    }

    internal static string ExternalHeadline(ArchitecturePrReportProjection projection)
    {
        ArchitecturePrReportValidationReceipt? receipt = PrimaryReceipt(projection);
        if (receipt?.ExternalEvidence is null)
        {
            return $"`{DimensionToken(projection, "external_evidence")}`";
        }

        ArchitecturePrReportExternalEvidence external = receipt.ExternalEvidence;
        return $"`{DimensionToken(projection, "external_evidence")}` — {external.Requirements.Count} requirement(s), {external.Findings.Count} finding(s)";
    }

    internal static string DimensionToken(ArchitecturePrReportProjection projection, string name) =>
        DimensionToken(projection.Headline.Dimensions.FirstOrDefault(item => string.Equals(item.Name, name, StringComparison.Ordinal))?.State);

    internal static string DimensionToken(ArchitectureHealthDimensionState? state) => state switch
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

    internal static string GateToken(ArchitectureHealthGate gate) => gate switch
    {
        ArchitectureHealthGate.Pass => "pass",
        ArchitectureHealthGate.Fail => "fail",
        ArchitectureHealthGate.Unassessable => "unassessable",
        _ => "unavailable",
    };

    internal static string HealthToken(ArchitectureHealthState health) => health switch
    {
        ArchitectureHealthState.Healthy => "healthy",
        ArchitectureHealthState.Debt => "debt",
        ArchitectureHealthState.Degrading => "degrading",
        ArchitectureHealthState.Failing => "failing",
        ArchitectureHealthState.Unassessable => "unassessable",
        _ => "unavailable",
    };

    internal static string AvailabilityToken(ArchitecturePrReportAvailability availability) => availability switch
    {
        ArchitecturePrReportAvailability.Complete => "complete",
        ArchitecturePrReportAvailability.Unavailable => "unavailable",
        ArchitecturePrReportAvailability.Unassessable => "unassessable",
        _ => "unavailable",
    };

    internal static string FormatWaiver(ArchitectureWaiverLifecycleRecord waiver) =>
        $"`{Inline(waiver.Id)}` state=`{Inline(waiver.State)}` rule=`{Inline(waiver.ContractId ?? waiver.ContractName)}` " +
        $"target=`{Inline(waiver.ForbiddenReference)}` reason={Text(waiver.Reason)} owner={Text(waiver.Owner ?? "-")} " +
        $"issue={Text(waiver.Issue ?? "-")} expires={Text(waiver.Expires?.ToString("yyyy-MM-dd") ?? "-")}";

    internal static string FormatBaseline(ArchitecturePrReportBaselineEntry entry) =>
        $"`{Inline(entry.Identity ?? entry.ContractId)}` status=`{Inline(entry.Status)}` {Text(entry.ContractGroup)} " +
        $"{Text(entry.SourceType)} → {Text(entry.ForbiddenReference)}";

    internal static string FormatApplicabilityControl(ArchitecturePrReportApplicabilityControl control) =>
        $"`{Inline(control.ControlIdentity)}` state=`{Inline(control.State)}` membership=`{Inline(control.Membership ?? "-")}` " +
        (control.IntegrityValid ? "integrity=valid" : "integrity=invalid");

    internal static string FormatApplicabilityReason(ArchitecturePrReportApplicabilityReason reason) =>
        $"`{Inline(reason.Code)}`{FormatProvenance(reason.Provenance)}";

    internal static string FormatTopologySubject(string control, ArchitecturePrReportTopologySubject subject) =>
        $"control=`{Inline(control)}` `{Inline(subject.Identity)}` {Text(subject.Project)} / {Text(subject.Assembly)} " +
        $"subject={Text(subject.Subject)} disposition=`{Inline(subject.Disposition)}`";

    internal static string FormatExternalRequirement(ArchitecturePrReportExternalRequirement requirement) =>
        $"`{Inline(requirement.Id)}` tool=`{Inline(requirement.Tool)}` format=`{Inline(requirement.Format)}` " +
        $"required={requirement.Required} run=`{Inline(requirement.Run)}`";

    internal static string FormatFinding(ArchitecturePrReportFinding finding) =>
        $"`{Inline(finding.CanonicalIdentity)}` {Text(finding.MessageCode)} ({Text(finding.ContractId ?? finding.ContractName)})";

    internal static string FormatRemediation(ArchitecturePrReportFinding finding)
    {
        ArchitecturePrReportRemediation remediation = finding.Remediation!;
        return $"`{Inline(finding.CanonicalIdentity)}` category=`{Inline(remediation.Category)}` {Text(remediation.Summary)}";
    }

    internal static string FormatReasonIdentity(ArchitectureHealthReason reason)
    {
        string identity = reason.EvidenceIdentity ?? reason.ControlIdentity ?? reason.PolicyIdentity ?? string.Empty;
        return string.IsNullOrWhiteSpace(identity) ? string.Empty : $" (`{Inline(identity)}`)";
    }

    internal static string FormatProvenance(ArchitecturePrReportProvenanceReference provenance)
    {
        string?[] values =
        [
            provenance.Family,
            provenance.ControlIdentity,
            provenance.PolicyIdentity,
            provenance.EvidenceIdentity,
        ];
        return values.Any(value => !string.IsNullOrWhiteSpace(value))
            ? $" ({string.Join(", ", values.Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => $"`{Inline(value!)}`"))})"
            : string.Empty;
    }

    internal static string Inline(string value) => PrReportMarkdownEscaping.EscapeInlineCode(value);

    internal static string Text(string value) => PrReportMarkdownEscaping.EscapeMarkdownText(value);
}
