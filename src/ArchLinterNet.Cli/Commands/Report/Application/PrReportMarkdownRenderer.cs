using System.Text;
using ArchLinterNet.Core.Change;
using ArchLinterNet.Core.Model;

namespace ArchLinterNet.Cli.Commands.Report.Application;

/// <summary>
/// Renders the Core PR-report projection as architecture-only Markdown.
/// This type deliberately has no access to policy, analysis, SARIF, or network services.
/// </summary>
internal static partial class PrReportMarkdownRenderer
{
    public static string Render(ArchitecturePrReportProjection projection, int maxDetails = 20)
    {
        ArgumentNullException.ThrowIfNull(projection);
        if (maxDetails <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxDetails), "The report detail bound must be positive.");
        }

        var builder = new StringBuilder();
        builder.AppendLine("# Architecture PR report");
        builder.AppendLine();
        AppendHeadline(builder, projection);
        builder.AppendLine();
        AppendBlockers(builder, projection, maxDetails);
        builder.AppendLine();
        AppendDebt(builder, projection, maxDetails);
        builder.AppendLine();
        AppendCompleteness(builder, projection, maxDetails);
        builder.AppendLine();
        AppendChanges(builder, projection, maxDetails);
        builder.AppendLine();
        AppendRemediation(builder, projection, maxDetails);
        builder.AppendLine();
        AppendNavigation(builder, projection, maxDetails);
        return builder.ToString().TrimEnd() + Environment.NewLine;
    }

    private static void AppendHeadline(StringBuilder builder, ArchitecturePrReportProjection projection)
    {
        ArchitecturePrReportHeadline headline = projection.Headline;
        builder.AppendLine("## Acceptance");
        builder.AppendLine($"- Architecture acceptance: **{GateToken(headline.Gate)}** (`gate={GateToken(headline.Gate)}`)");
        builder.AppendLine($"- Architecture health: `{HealthToken(headline.Health)}`");
        builder.AppendLine($"- Report availability: `{AvailabilityToken(headline.Availability)}`");

        ArchitecturePolicyInventory? inventory = PrimaryReceipt(projection)?.PolicyInventory;
        builder.AppendLine(inventory is null
            ? "- Effective policy controls: `unavailable`"
            : $"- Effective policy controls: `{inventory.EffectiveRuleCount}` (strict {inventory.Rules.Strict}, audit {inventory.Rules.Audit}, coverage {inventory.Rules.Coverage})");
        builder.AppendLine($"- Control applicability/evaluability: {ApplicabilityHeadline(projection)}");
        builder.AppendLine($"- Configured topology: {TopologyHeadline(projection)}");
        builder.AppendLine($"- Explicit waiver debt: {WaiverHeadline(projection)}");
        builder.AppendLine($"- Existing finding debt: {ExistingDebtHeadline(projection)}");
        builder.AppendLine($"- New architecture debt: {NewDebtHeadline(projection)}");
        builder.AppendLine($"- Policy weakening: {PolicyWeakeningHeadline(projection)}");
        builder.AppendLine($"- Metrics: `{DimensionToken(projection, "metrics")}`");
        builder.AppendLine($"- Required external evidence: {ExternalHeadline(projection)}");
    }

    private static void AppendBlockers(
        StringBuilder builder,
        ArchitecturePrReportProjection projection,
        int maxDetails)
    {
        builder.AppendLine("## Blockers");
        List<string> blockers = new();
        ArchitecturePrReportEvidence? evidence = projection.Evidence;
        if (evidence is not null)
        {
            if (evidence.DebtGate.PolicyWeakening is not null)
            {
                foreach (ArchitecturePrReportPolicyWeakeningFinding finding in evidence.DebtGate.PolicyWeakening.Findings
                    .Where(IsPolicyWeakeningBlocker)
                    .OrderBy(item => item.Identity, StringComparer.Ordinal))
                {
                    blockers.Add($"policy weakening `{Safe(finding.Identity)}`: {Safe(finding.Classification)} {Safe(finding.ControlIdentity)}");
                }
            }

            foreach (ArchitecturePrReportValidationReceipt receipt in evidence.ValidationOutcomes)
            {
                if (receipt.WaiverLifecycle is not null)
                {
                    HashSet<string> blockingStates = receipt.WaiverLifecycle.BlockingStates.ToHashSet(StringComparer.Ordinal);
                    foreach (ArchitectureWaiverLifecycleRecord waiver in receipt.WaiverLifecycle.Records
                        .Where(item => blockingStates.Contains(item.State))
                        .OrderBy(item => item.Id, StringComparer.Ordinal))
                    {
                        blockers.Add($"waiver `{Safe(waiver.Id)}`: lifecycle `{Safe(waiver.State)}` ({Safe(waiver.ContractId ?? waiver.ContractName)})");
                    }
                }
            }

            if (projection.Headline.Gate != ArchitectureHealthGate.Pass)
            {
                foreach (ArchitecturePrReportFinding finding in AllFindings(evidence)
                    .OrderBy(item => item.ContractId ?? item.ContractName, StringComparer.Ordinal)
                    .ThenBy(item => item.CanonicalIdentity, StringComparer.Ordinal))
                {
                    blockers.Add($"finding `{Safe(finding.CanonicalIdentity)}`: {Safe(finding.MessageCode)} ({Safe(finding.ContractId ?? finding.ContractName)})");
                }
            }
        }

        foreach (ArchitectureHealthDimension dimension in projection.Headline.Dimensions
            .Where(item => item.State is ArchitectureHealthDimensionState.Fail or ArchitectureHealthDimensionState.Unassessable)
            .OrderBy(item => item.Name, StringComparer.Ordinal))
        {
            foreach (ArchitectureHealthReason reason in dimension.Reasons)
            {
                blockers.Add($"{Safe(dimension.Name)} `{DimensionToken(dimension.State)}`: {Safe(reason.Code)}{FormatReasonIdentity(reason)}");
            }
        }

        AppendBounded(builder, "Blocking governance and findings", blockers.Count, blockers, maxDetails,
            static item => $"- {item}");
    }

    private static void AppendDebt(
        StringBuilder builder,
        ArchitecturePrReportProjection projection,
        int maxDetails)
    {
        builder.AppendLine("## Non-blocking debt");
        ArchitecturePrReportEvidence? evidence = projection.Evidence;
        if (evidence is null)
        {
            builder.AppendLine("- Debt evidence: `unavailable`");
            return;
        }

        ArchitecturePrReportValidationReceipt? receipt = PrimaryReceipt(projection);
        ArchitecturePolicyInventoryIgnoreDebt? waiverDebt = receipt?.PolicyInventory?.IgnoreDebt;
        builder.AppendLine(waiverDebt is null
            ? "- Explicit waiver debt: `unavailable`"
            : $"- Explicit waiver debt: {waiverDebt.Total} total ({waiverDebt.Active} active; {waiverDebt.Stale} stale; {waiverDebt.Expired} expired; {waiverDebt.MetadataIncomplete} metadata-incomplete; {waiverDebt.Invalid} invalid)");

        IReadOnlyList<ArchitectureWaiverLifecycleRecord> lifecycleRecords = receipt?.WaiverLifecycle?.Records is { Count: > 0 } records
            ? records
            : receipt?.PolicyInventory?.Waivers ?? Array.Empty<ArchitectureWaiverLifecycleRecord>();
        HashSet<string> blockingStates = receipt?.WaiverLifecycle?.BlockingStates.ToHashSet(StringComparer.Ordinal) ?? [];
        List<string> waivers = lifecycleRecords
            .Where(item => !blockingStates.Contains(item.State))
            .OrderBy(item => item.Id, StringComparer.Ordinal)
            .Select(FormatWaiver)
            .ToList() ?? new();
        AppendBounded(builder, "Waiver lifecycle detail", waivers.Count, waivers, maxDetails,
            static item => $"- {item}");

        List<string> baseline = evidence.DebtGate.PersistentDebt.Entries
            .OrderBy(item => item.Identity ?? item.ContractId, StringComparer.Ordinal)
            .ThenBy(item => item.Status, StringComparer.Ordinal)
            .Select(FormatBaseline)
            .ToList();
        AppendBounded(builder, "Existing baseline/finding debt", baseline.Count, baseline, maxDetails,
            static item => $"- {item}");
    }

    private static void AppendCompleteness(
        StringBuilder builder,
        ArchitecturePrReportProjection projection,
        int maxDetails)
    {
        builder.AppendLine("## Completeness and evidence");
        ArchitecturePrReportValidationReceipt? receipt = PrimaryReceipt(projection);
        if (receipt is null)
        {
            builder.AppendLine("- Canonical report evidence: `unavailable`");
            return;
        }

        ArchitecturePrReportApplicability? applicability = receipt.Applicability;
        if (applicability is null)
        {
            builder.AppendLine("- Applicability: `unavailable`");
        }
        else
        {
            ArchitecturePrReportApplicabilitySummary summary = applicability.Summary;
            builder.AppendLine($"- Applicability: `{Safe(applicability.State)}` — {summary.RequiredEvaluable}/{summary.Required} evaluable; {summary.RequiredUnassessable} unassessable.");
            List<string> controls = applicability.Controls
                .OrderBy(item => item.ControlIdentity, StringComparer.Ordinal)
                .Select(FormatApplicabilityControl)
                .ToList();
            AppendBounded(builder, "Applicability controls", controls.Count, controls, maxDetails,
                static item => $"- {item}");
            List<string> reasons = applicability.Reasons
                .OrderBy(item => item.Code, StringComparer.Ordinal)
                .Select(FormatApplicabilityReason)
                .ToList();
            AppendBounded(builder, "Applicability reasons", reasons.Count, reasons, maxDetails,
                static item => $"- {item}");
        }

        List<(ArchitecturePrReportTopology Topology, string Control)> topologies = receipt.Applicability?.Controls
            .Where(item => item.Record?.Topology is not null)
            .Select(item => (item.Record!.Topology!, item.ControlIdentity))
            .OrderBy(item => item.ControlIdentity, StringComparer.Ordinal)
            .ToList() ?? new();
        if (topologies.Count == 0)
        {
            builder.AppendLine($"- Topology evidence: `{DimensionToken(projection, "topology")}`");
        }
        else
        {
            int subjectCount = topologies.Sum(item => item.Topology.Subjects.Count);
            int mapped = topologies.Sum(item => item.Topology.Counts.Mapped);
            int unmapped = topologies.Sum(item => item.Topology.Counts.Unmapped);
            int ambiguous = topologies.Sum(item => item.Topology.Counts.Ambiguous);
            builder.AppendLine($"- Topology evidence: {mapped} mapped, {unmapped} unmapped, {ambiguous} ambiguous ({subjectCount} subjects).");
            List<string> subjects = topologies
                .SelectMany(item => item.Topology.Subjects.Select(subject => FormatTopologySubject(item.Control, subject)))
                .OrderBy(item => item, StringComparer.Ordinal)
                .ToList();
            AppendBounded(builder, "Topology subjects", subjects.Count, subjects, maxDetails,
                static item => $"- {item}");
        }

        ArchitecturePrReportExternalEvidence? external = receipt.ExternalEvidence;
        if (external is null)
        {
            builder.AppendLine($"- External evidence: `{DimensionToken(projection, "external_evidence")}`");
        }
        else
        {
            builder.AppendLine($"- External evidence: `{DimensionToken(projection, "external_evidence")}` — {external.Requirements.Count} requirement(s), {external.Findings.Count} finding(s).");
            List<string> requirements = external.Requirements
                .OrderBy(item => item.Id, StringComparer.Ordinal)
                .Select(FormatExternalRequirement)
                .ToList();
            AppendBounded(builder, "External evidence requirements", requirements.Count, requirements, maxDetails,
                static item => $"- {item}");
            List<string> findings = external.Findings
                .OrderBy(item => item.CanonicalIdentity, StringComparer.Ordinal)
                .Select(FormatFinding)
                .ToList();
            AppendBounded(builder, "External evidence findings", findings.Count, findings, maxDetails,
                static item => $"- {item}");
        }
    }

    private static void AppendChanges(
        StringBuilder builder,
        ArchitecturePrReportProjection projection,
        int maxDetails)
    {
        builder.AppendLine("## Architecture change");
        ArchitectureChangeReport change = projection.Change;
        AppendChangeEntries(builder, "Added", change.Added, maxDetails);
        AppendChangeEntries(builder, "Removed", change.Removed, maxDetails);
        AppendChangeFindings(builder, "New findings", change.NewFindings, maxDetails);
        AppendChangeFindings(builder, "Existing findings", change.ExistingFindings, maxDetails);
        AppendChangeFindings(builder, "Resolved findings", change.ResolvedFindings, maxDetails);
        AppendBounded(builder, "Baseline debt identities", change.BaselineDebt.Count, change.BaselineDebt,
            maxDetails, static item => $"- `{Safe(item)}`");
    }

    private static void AppendRemediation(
        StringBuilder builder,
        ArchitecturePrReportProjection projection,
        int maxDetails)
    {
        builder.AppendLine("## Supplied remediation");
        List<ArchitecturePrReportFinding> findings = projection.Evidence is null
            ? new()
            : AllFindings(projection.Evidence)
                .Where(item => item.Remediation is not null)
                .GroupBy(item => item.CanonicalIdentity, StringComparer.Ordinal)
                .Select(group => group.First())
                .OrderBy(item => item.Remediation!.Category, StringComparer.Ordinal)
                .ThenBy(item => item.CanonicalIdentity, StringComparer.Ordinal)
                .ToList();
        List<string> entries = findings.Select(FormatRemediation).ToList();
        AppendBounded(builder, "Remediation categories", entries.Count, entries, maxDetails,
            static item => $"- {item}");
    }

    private static void AppendNavigation(
        StringBuilder builder,
        ArchitecturePrReportProjection projection,
        int maxDetails)
    {
        builder.AppendLine("## Canonical navigation");
        List<string> references = projection.Navigation
            .OrderBy(item => item.Authority, StringComparer.Ordinal)
            .ThenBy(item => item.Identity, StringComparer.Ordinal)
            .ThenBy(item => item.Path, StringComparer.Ordinal)
            .Select(item => $"`{Safe(item.Authority)}`" +
                (string.IsNullOrWhiteSpace(item.Identity) ? string.Empty : $" `{Safe(item.Identity)}`") +
                (string.IsNullOrWhiteSpace(item.Path) ? string.Empty : $" ({Safe(item.Path)})"))
            .ToList();
        AppendBounded(builder, "References", references.Count, references, maxDetails,
            static item => $"- {item}");
    }

    private static void AppendChangeEntries(
        StringBuilder builder,
        string title,
        IReadOnlyList<ArchitectureChangeEntry> entries,
        int maxDetails) =>
        AppendBounded(builder, $"{title} surfaces", entries.Count,
            entries.OrderBy(item => item.Kind, StringComparer.Ordinal)
                .ThenBy(item => item.Identity, StringComparer.Ordinal)
                .Select(item => $"[{Safe(item.Kind)}] `{Safe(item.Identity)}` — {Safe(item.Display)}")
                .ToList(), maxDetails, static item => $"- {item}");

    private static void AppendChangeFindings(
        StringBuilder builder,
        string title,
        IReadOnlyList<ArchitectureChangeFinding> findings,
        int maxDetails) =>
        AppendBounded(builder, title, findings.Count,
            findings.OrderBy(item => item.Kind, StringComparer.Ordinal)
                .ThenBy(item => item.Identity, StringComparer.Ordinal)
                .Select(item => $"[{Safe(item.Kind)}] `{Safe(item.Identity)}` — {Safe(item.Display)}")
                .ToList(), maxDetails, static item => $"- {item}");

    private static void AppendBounded<T>(
        StringBuilder builder,
        string title,
        int total,
        IReadOnlyList<T> items,
        int maxDetails,
        Func<T, string> format)
    {
        builder.AppendLine($"### {title} ({total})");
        int shown = Math.Min(total, Math.Min(maxDetails, items.Count));
        builder.AppendLine($"Showing {shown} of {total}; omitted {Math.Max(0, total - shown)}.");
        for (int index = 0; index < shown; index++)
        {
            builder.AppendLine(format(items[index]));
        }
    }

}
