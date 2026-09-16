using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;

namespace ArchLinterNet.Cli.Commands.Report.Application;

using static PrReportMarkdownFormatter;

internal static class PrReportMarkdownBlockers
{
    internal static List<string> Build(ArchitecturePrReportProjection projection)
    {
        List<string> blockers = projection.Evidence is { } evidence
            ? BuildEvidenceBlockers(projection, evidence)
            : [];
        blockers.AddRange(BuildHealthBlockers(projection));
        return blockers;
    }

    private static List<string> BuildEvidenceBlockers(
        ArchitecturePrReportProjection projection,
        ArchitecturePrReportEvidence evidence)
    {
        List<string> blockers = [];
        AddBaselineBlockers(blockers, evidence);
        AddPolicyWeakeningBlockers(blockers, evidence);
        AddWaiverBlockers(blockers, PrimaryReceipt(projection));
        AddStrictFindingBlockers(blockers, PrimaryReceipt(projection));
        return blockers;
    }

    private static void AddBaselineBlockers(
        List<string> blockers,
        ArchitecturePrReportEvidence evidence)
    {
        if (evidence.DebtGate.PersistentDebt.InSync)
        {
            return;
        }

        foreach (ArchitecturePrReportBaselineEntry entry in BlockingBaselineLifecycle(evidence)
            .OrderBy(item => item.Identity ?? item.ContractId, StringComparer.Ordinal)
            .ThenBy(item => item.Status, StringComparer.Ordinal))
        {
            blockers.Add($"baseline lifecycle `{Inline(Bounded(entry.Status))}`: {FormatBaseline(entry)}");
        }
    }

    private static void AddPolicyWeakeningBlockers(
        List<string> blockers,
        ArchitecturePrReportEvidence evidence)
    {
        ArchitecturePrReportPolicyWeakening? weakening = evidence.DebtGate.PolicyWeakening;
        if (weakening is not { HasBlockingFindings: true }
            || !evidence.DebtGate.Succeeded
            || !evidence.DebtGate.Evaluation.Completed
            || evidence.DebtGate.Passed)
        {
            return;
        }

        foreach (ArchitecturePrReportPolicyWeakeningFinding finding in weakening.Findings
            .OrderBy(item => item.Identity, StringComparer.Ordinal))
        {
            blockers.Add($"policy weakening `{Inline(Bounded(finding.Identity))}`: {Text(Bounded(finding.Classification))} {Text(Bounded(finding.ControlIdentity))}");
        }
    }

    private static void AddWaiverBlockers(
        List<string> blockers,
        ArchitecturePrReportValidationReceipt? receipt)
    {
        if (receipt?.WaiverLifecycle is not { } lifecycle)
        {
            return;
        }

        HashSet<string> blockingStates = lifecycle.BlockingStates.ToHashSet(StringComparer.Ordinal);
        foreach (ArchitectureWaiverLifecycleRecord waiver in lifecycle.Records
            .Where(item => blockingStates.Contains(item.State))
            .OrderBy(item => item.Id, StringComparer.Ordinal))
        {
            blockers.Add($"waiver `{Inline(Bounded(waiver.Id))}`: lifecycle `{Inline(Bounded(waiver.State))}` ({Text(Bounded(waiver.ContractId ?? waiver.ContractName))})");
        }
    }

    private static void AddStrictFindingBlockers(
        List<string> blockers,
        ArchitecturePrReportValidationReceipt? receipt)
    {
        if (receipt is null)
        {
            return;
        }

        foreach (ArchitecturePrReportFinding finding in receipt.Findings
            .Where(finding => string.Equals(finding.Mode, "strict", StringComparison.Ordinal)
                && string.Equals(finding.Severity, "error", StringComparison.Ordinal)
                && !string.Equals(finding.Kind, "build_state_preflight", StringComparison.Ordinal))
            .OrderBy(item => item.ContractId ?? item.ContractName, StringComparer.Ordinal)
            .ThenBy(item => item.CanonicalIdentity, StringComparer.Ordinal))
        {
            blockers.Add($"finding `{Inline(Bounded(finding.CanonicalIdentity))}`: {Text(Bounded(finding.MessageCode))} ({Text(Bounded(finding.ContractId ?? finding.ContractName))})");
        }
    }

    private static List<string> BuildHealthBlockers(ArchitecturePrReportProjection projection)
    {
        List<string> blockers = [];
        foreach (PrReportMarkdownHealth.HealthExplanationView explanation in PrReportMarkdownHealth.BuildHealthExplanations(projection)
            .Where(item => item.IsBlocking)
            .OrderBy(item => item.Dimension, StringComparer.Ordinal))
        {
            foreach (ArchitectureHealthReason reason in explanation.Reasons)
            {
                blockers.Add($"{Text(Bounded(explanation.Dimension))} `{DimensionToken(explanation.State)}`: {Text(Bounded(reason.Code))}{FormatReasonIdentity(reason)}");
            }
        }

        return blockers;
    }
}
