using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.PolicyContext;

namespace ArchLinterNet.Core.PolicyWeakening;

// An approval is deliberately narrower than a waiver. It can acknowledge only a complete,
// canonical Added delta from the base snapshot to the captured CLR surface for the same
// public-api contract in the exact base/head contexts.
internal static class ArchitecturePolicyWeakeningPublicApiApprovalEvaluator
{
    internal static void Evaluate(
        ArchitecturePolicyWeakeningRequest request,
        List<ArchitecturePolicyWeakeningFinding> findings,
        ICollection<ArchitectureApprovedPublicApiAddition> approved)
    {
        foreach (ArchitecturePublicApiWeakeningApproval approval in request.PublicApiApprovals ?? [])
        {
            if (!TryValidate(request, approval, out ArchitectureApprovedPublicApiAddition? accepted))
            {
                continue;
            }

            string controlIdentity = "public_api_surface:" + approval.ContractId + ":resolved_snapshot_entries";
            int removed = findings.RemoveAll(finding =>
                finding.Kind == "typed_fact_impact_not_proven"
                && finding.Classification == "impact_not_proven"
                && string.Equals(finding.ControlIdentity, controlIdentity, StringComparison.Ordinal));
            if (removed > 0)
            {
                approved.Add(accepted!);
            }
        }
    }

    private static bool TryValidate(
        ArchitecturePolicyWeakeningRequest request,
        ArchitecturePublicApiWeakeningApproval approval,
        out ArchitectureApprovedPublicApiAddition? accepted)
    {
        accepted = null;
        if (approval is null
            || approval.SchemaVersion != ArchitecturePublicApiWeakeningApproval.CurrentSchemaVersion
            || !string.Equals(approval.Kind, ArchitecturePublicApiWeakeningApproval.ApprovalKind, StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(approval.ContractId)
            || approval.Added is null
            || approval.Added.Count == 0
            || !string.Equals(approval.BaseContextDigest, ArchitecturePolicyWeakeningContextSupport.ComputeContextDigest(request.BaseContext), StringComparison.Ordinal)
            || !string.Equals(approval.CurrentContextDigest, ArchitecturePolicyWeakeningContextSupport.ComputeContextDigest(request.CurrentContext), StringComparison.Ordinal))
        {
            return false;
        }

        ArchitecturePolicyContextContract? baseline = FindPublicApiContract(request.BaseContext, approval.ContractId);
        ArchitecturePolicyContextContract? current = FindPublicApiContract(request.CurrentContext, approval.ContractId);
        if (baseline is null || current is null
            || !string.Equals(baseline.Mode, current.Mode, StringComparison.Ordinal)
            || !TryGetSingleFactValue(baseline, "api_comparison", out string? baseMode)
            || !TryGetSingleFactValue(current, "api_comparison", out string? currentMode)
            || !string.Equals(baseMode, currentMode, StringComparison.Ordinal)
            || baseMode is not ("exact" or "additions_only")
            || !TryGetSnapshotEntries(baseline, out IReadOnlyList<PublicApiSnapshotEntry> baseEntries)
            || !TryGetSnapshotEntries(current, out IReadOnlyList<PublicApiSnapshotEntry> currentEntries)
            || !TryGetLiveEntries(request, approval, out IReadOnlyList<PublicApiSnapshotEntry> liveEntries)
            || !SameEntries(currentEntries, liveEntries))
        {
            return false;
        }

        PublicApiDelta delta = PublicApiSnapshotDiffer.Diff(baseEntries, liveEntries);
        PublicApiSnapshotEntry[] added = delta.Added
            .Select(entry => new PublicApiSnapshotEntry(entry.AssemblyName, entry.Signature))
            .ToArray();
        if (delta.Removed.Count != 0 || delta.Changed.Count != 0 || !SameEntries(added, approval.Added))
        {
            return false;
        }

        accepted = new ArchitectureApprovedPublicApiAddition(
            approval.ContractId,
            approval.BaseContextDigest,
            approval.CurrentContextDigest,
            currentMode!,
            added.OrderBy(entry => entry.AssemblyName, StringComparer.Ordinal)
                .ThenBy(entry => entry.Signature, StringComparer.Ordinal)
                .ToArray());
        return true;
    }

    private static ArchitecturePolicyContextContract? FindPublicApiContract(
        ArchitecturePolicyContextExport context,
        string id) => context.Contracts.SingleOrDefault(contract =>
        contract.Family == "public_api_surface" && contract.Id == id);

    private static bool TryGetSingleFactValue(
        ArchitecturePolicyContextContract contract,
        string name,
        out string? value)
    {
        value = null;
        ArchitecturePolicyContextContractFact[] facts = contract.Facts.Where(fact => fact.Name == name).ToArray();
        if (facts.Length != 1 || facts[0].Values.Count != 1 || facts[0].Items.Count != 0)
        {
            return false;
        }

        value = facts[0].Values[0];
        return !string.IsNullOrWhiteSpace(value);
    }

    private static bool TryGetSnapshotEntries(
        ArchitecturePolicyContextContract contract,
        out IReadOnlyList<PublicApiSnapshotEntry> entries)
    {
        entries = Array.Empty<PublicApiSnapshotEntry>();
        ArchitecturePolicyContextContractFact[] facts = contract.Facts
            .Where(fact => fact.Name == "resolved_snapshot_entries")
            .ToArray();
        if (facts.Length != 1 || facts[0].Values.Count != 0)
        {
            return false;
        }

        List<PublicApiSnapshotEntry> parsed = new(facts[0].Items.Count);
        foreach (ArchitecturePolicyContextContractFact entry in facts[0].Items)
        {
            if (entry.Name != "entry"
                || !TryGetNestedValue(entry, "assembly", out string? assembly)
                || !TryGetNestedValue(entry, "signature", out string? signature))
            {
                return false;
            }

            parsed.Add(new PublicApiSnapshotEntry(assembly!, signature!));
        }

        if (parsed.Distinct().Count() != parsed.Count)
        {
            return false;
        }

        entries = parsed;
        return true;
    }

    private static bool TryGetLiveEntries(
        ArchitecturePolicyWeakeningRequest request,
        ArchitecturePublicApiWeakeningApproval approval,
        out IReadOnlyList<PublicApiSnapshotEntry> entries)
    {
        entries = Array.Empty<PublicApiSnapshotEntry>();
        ArchitecturePublicApiLiveEvidence[] matchingEvidence = request.PublicApiLiveEvidence.Where(item =>
            item.SchemaVersion == ArchitecturePublicApiLiveEvidence.CurrentSchemaVersion
            && string.Equals(item.Kind, ArchitecturePublicApiLiveEvidence.EvidenceKind, StringComparison.Ordinal)
            && string.Equals(item.ContextDigest, approval.CurrentContextDigest, StringComparison.Ordinal)
            && string.Equals(item.ContractId, approval.ContractId, StringComparison.Ordinal)).ToArray();
        if (matchingEvidence.Length != 1
            || matchingEvidence[0].Entries is null || matchingEvidence[0].Entries.Count == 0
            || matchingEvidence[0].Entries.Any(entry => string.IsNullOrWhiteSpace(entry.AssemblyName) || string.IsNullOrWhiteSpace(entry.Signature))
            || matchingEvidence[0].Entries.Distinct().Count() != matchingEvidence[0].Entries.Count)
        {
            return false;
        }

        entries = matchingEvidence[0].Entries;
        return true;
    }

    private static bool TryGetNestedValue(
        ArchitecturePolicyContextContractFact fact,
        string name,
        out string? value)
    {
        value = null;
        ArchitecturePolicyContextContractFact[] nested = fact.Items.Where(item => item.Name == name).ToArray();
        if (nested.Length != 1 || nested[0].Values.Count != 1 || nested[0].Items.Count != 0)
        {
            return false;
        }

        value = nested[0].Values[0];
        return !string.IsNullOrWhiteSpace(value);
    }

    private static bool SameEntries(
        IReadOnlyList<PublicApiSnapshotEntry> expected,
        IReadOnlyList<PublicApiSnapshotEntry> actual) =>
        expected.Count == actual.Count
        && expected.ToHashSet().SetEquals(actual);
}
