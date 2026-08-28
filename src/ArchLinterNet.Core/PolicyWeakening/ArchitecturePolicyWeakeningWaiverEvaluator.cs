using ArchLinterNet.Core.PolicyContext;
using static ArchLinterNet.Core.PolicyWeakening.ArchitecturePolicyWeakeningComparisonSupport;

namespace ArchLinterNet.Core.PolicyWeakening;

/// <summary>Compares static structured waiver declarations without evaluating live findings.</summary>
internal static class ArchitecturePolicyWeakeningWaiverEvaluator
{
    private static readonly StringComparer _comparer = StringComparer.Ordinal;

    internal static void Evaluate(
        ArchitecturePolicyContextExport baseline,
        ArchitecturePolicyContextExport current,
        string severity,
        ICollection<ArchitecturePolicyWeakeningFinding> findings)
    {
        Dictionary<string, ArchitecturePolicyContextWaiver> baseById = baseline.Waivers
            .ToDictionary(WaiverKey, _comparer);

        foreach (ArchitecturePolicyContextWaiver waiver in current.Waivers
                     .Where(item => !baseById.TryGetValue(WaiverKey(item), out ArchitecturePolicyContextWaiver? previous)
                         || !string.Equals(previous.TargetFingerprint, item.TargetFingerprint, StringComparison.Ordinal))
                     .OrderBy(WaiverKey, _comparer))
        {
            bool changedTarget = baseById.TryGetValue(WaiverKey(waiver), out ArchitecturePolicyContextWaiver? previous);
            string kind = changedTarget ? "structured_waiver_target_changed" : "structured_waiver_added";
            string control = $"{waiver.ContractFamily}:{waiver.ContractId}:{waiver.WaiverId}";
            string details = string.Join("; ", waiver.TargetFingerprint, waiver.ContractFamily, waiver.ContractId);
            string[] baseValues = changedTarget ? [previous!.TargetFingerprint] : Array.Empty<string>();

            findings.Add(CreateFinding(
                new PolicyWeakeningControlContext(
                    kind,
                    control,
                    changedTarget ? "impact_not_proven" : "semantic",
                    severity),
                baseValues,
                [details],
                previous?.Provenance,
                waiver.Provenance,
                [waiver.ContractId, waiver.WaiverId, waiver.TargetFingerprint],
                waiver.Reason));
        }
    }

    private static string WaiverKey(ArchitecturePolicyContextWaiver waiver) => string.Join(
        "\u001f", waiver.Mode, waiver.ContractFamily, waiver.ContractId, waiver.WaiverId);
}
