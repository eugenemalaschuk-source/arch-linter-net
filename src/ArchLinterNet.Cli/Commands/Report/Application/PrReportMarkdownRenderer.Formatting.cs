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
        if (applicability is not null)
        {
            return $"`{Inline(applicability.State)}` — {applicability.Summary.RequiredEvaluable}/{applicability.Summary.Required} evaluable";
        }

        string token = DimensionToken(projection, "applicability");
        return token == "not_configured"
            ? "`not_configured` — canonical applicability receipt not configured"
            : $"`{token}`";
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
        return debt is null
            ? "`unavailable`"
            : $"`{debt.Total}` total (`{debt.Active}` active, `{debt.Stale}` stale, `{debt.Expired}` expired, " +
              $"`{debt.MetadataIncomplete}` metadata-incomplete, `{debt.Invalid}` invalid)";
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
        if (!external.HasCompleteTrustReceipts)
        {
            return "`unavailable` — canonical trust receipt missing";
        }

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
        $"`{Inline(Bounded(waiver.Id))}` state=`{Inline(Bounded(waiver.State))}` rule=`{Inline(Bounded(waiver.ContractId ?? waiver.ContractName))}` " +
        $"target=`{Inline(Bounded(waiver.ForbiddenReference))}` reason={Text(Bounded(waiver.Reason))} owner={Text(Bounded(waiver.Owner ?? "-"))} " +
        $"issue={Text(Bounded(waiver.Issue ?? "-"))} expires={Text(waiver.Expires?.ToString("yyyy-MM-dd") ?? "-")}";

    internal static string FormatBaseline(ArchitecturePrReportBaselineEntry entry) =>
        $"`{Inline(Bounded(entry.Identity ?? entry.ContractId))}` status=`{Inline(Bounded(entry.Status))}` {Text(Bounded(entry.ContractGroup))} " +
        $"{Text(Bounded(entry.SourceType))} → {Text(Bounded(entry.ForbiddenReference))}";

    internal static string FormatApplicabilityControl(ArchitecturePrReportApplicabilityControl control) =>
        $"`{Inline(Bounded(control.ControlIdentity))}` state=`{Inline(Bounded(control.State))}` membership=`{Inline(Bounded(control.Membership ?? "-"))}` " +
        (control.IntegrityValid ? "integrity=valid" : "integrity=invalid");

    internal static string FormatApplicabilityReason(ArchitecturePrReportApplicabilityReason reason) =>
        $"`{Inline(Bounded(reason.Code))}`{FormatProvenance(reason.Provenance)}";

    internal static string FormatTopologySubject(string control, ArchitecturePrReportTopologySubject subject) =>
        $"control=`{Inline(Bounded(control))}` `{Inline(Bounded(subject.Identity))}` {Text(Bounded(subject.Project))} / {Text(Bounded(subject.Assembly))} " +
        $"subject={Text(Bounded(subject.Subject))} disposition=`{Inline(Bounded(subject.Disposition))}`";

    internal static string FormatExternalRequirement(ArchitecturePrReportExternalRequirement requirement) =>
        $"`{Inline(Bounded(requirement.Id))}` tool=`{Inline(Bounded(requirement.Tool))}` format=`{Inline(Bounded(requirement.Format))}` " +
        $"required={requirement.Required} run=`{Inline(Bounded(requirement.Run))}`";

    internal static string FormatExternalEvidenceTrustReceipt(ArchitecturePrReportExternalEvidenceTrustReceipt receipt)
    {
        string resultCount = receipt.ResultCount is null ? string.Empty : $" results=`{receipt.ResultCount}`";
        string run = string.IsNullOrWhiteSpace(receipt.RunId) ? string.Empty : $" run=`{Inline(Bounded(receipt.RunId))}`";
        string artifact = string.IsNullOrWhiteSpace(receipt.ArtifactPath) ? string.Empty : $" artifact=`{Inline(Bounded(receipt.ArtifactPath))}`";
        return $"logical_evidence=`{Inline(Bounded(receipt.LogicalId))}` state=`{TrustStateToken(receipt.State)}` " +
            $"trust_status=`{TrustStatusToken(receipt.Status)}` reason=`{Inline(Bounded(receipt.ReasonCode))}`{resultCount}{run}{artifact}" +
            FormatExternalEvidenceContext(receipt.Context);
    }

    internal static string FormatFinding(ArchitecturePrReportFinding finding) =>
        $"`{Inline(Bounded(finding.CanonicalIdentity))}` {Text(Bounded(finding.MessageCode))} ({Text(Bounded(finding.ContractId ?? finding.ContractName))})";

    internal static string FormatRemediation(ArchitecturePrReportFinding finding)
    {
        ArchitecturePrReportRemediation remediation = finding.Remediation!;
        return $"`{Inline(Bounded(finding.CanonicalIdentity))}` category=`{Inline(Bounded(remediation.Category))}` {Text(Bounded(remediation.Summary))}";
    }

    internal static string FormatReasonIdentity(ArchitectureHealthReason reason)
    {
        string identity = reason.EvidenceIdentity ?? reason.ControlIdentity ?? reason.PolicyIdentity ?? string.Empty;
        return string.IsNullOrWhiteSpace(identity) ? string.Empty : $" (`{Inline(Bounded(identity))}`)";
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
            ? $" ({string.Join(", ", values.Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => $"`{Inline(Bounded(value!))}`"))})"
            : string.Empty;
    }

    internal static string Bounded(string value, int maxLength = 256) =>
        value.Length <= maxLength ? value : value[..(maxLength - 3)] + "...";

    internal static string Inline(string value) => PrReportMarkdownEscaping.EscapeInlineCode(value);

    internal static string Text(string value) => PrReportMarkdownEscaping.EscapeMarkdownText(value);

    private static string TrustStateToken(ArchitecturePrReportExternalEvidenceTrustState state) => state switch
    {
        ArchitecturePrReportExternalEvidenceTrustState.Current => "current",
        ArchitecturePrReportExternalEvidenceTrustState.Stale => "stale",
        ArchitecturePrReportExternalEvidenceTrustState.WrongContext => "wrong_context",
        ArchitecturePrReportExternalEvidenceTrustState.Missing => "missing",
        ArchitecturePrReportExternalEvidenceTrustState.Invalid => "invalid",
        ArchitecturePrReportExternalEvidenceTrustState.NotConfigured => "not_configured",
        _ => "invalid",
    };

    private static string TrustStatusToken(SarifEvidenceTrustStatus status) =>
        System.Text.Json.JsonNamingPolicy.SnakeCaseLower.ConvertName(status.ToString());

    private static string FormatExternalEvidenceContext(ArchitecturePrReportExternalEvidenceContext? context)
    {
        if (context is null)
        {
            return string.Empty;
        }

        return $" repository=`{Inline(Bounded(context.Repository ?? "-"))}` revision=`{Inline(Bounded(context.Revision ?? "-"))}` " +
            $"scope=`{Inline(Bounded(context.Scope ?? "-"))}`";
    }
}
