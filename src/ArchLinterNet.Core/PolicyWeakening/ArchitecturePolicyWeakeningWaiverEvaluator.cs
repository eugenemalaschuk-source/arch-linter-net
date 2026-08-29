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

        foreach (ArchitecturePolicyContextWaiver waiver in current.Waivers.OrderBy(WaiverKey, _comparer))
        {
            bool existsInBaseline = baseById.TryGetValue(WaiverKey(waiver), out ArchitecturePolicyContextWaiver? previous);
            string control = $"{waiver.ContractFamily}:{waiver.ContractId}:{waiver.WaiverId}";

            if (!existsInBaseline)
            {
                AddFinding(
                    "structured_waiver_added",
                    "semantic",
                    control,
                    Array.Empty<string>(),
                    [string.Join("; ", waiver.TargetFingerprint, waiver.ContractFamily, waiver.ContractId)],
                    null,
                    waiver,
                    severity,
                    findings);
                continue;
            }

            if (!string.Equals(previous!.TargetFingerprint, waiver.TargetFingerprint, StringComparison.Ordinal))
            {
                AddFinding(
                    "structured_waiver_target_changed",
                    "impact_not_proven",
                    control,
                    [previous.TargetFingerprint],
                    [string.Join("; ", waiver.TargetFingerprint, waiver.ContractFamily, waiver.ContractId)],
                    previous,
                    waiver,
                    severity,
                    findings);
            }

            if (HasExtendedExpiry(previous.Expires, waiver.Expires))
            {
                AddFinding(
                    "structured_waiver_expiry_extended",
                    "semantic",
                    control,
                    [previous.Expires!],
                    [waiver.Expires!],
                    previous,
                    waiver,
                    severity,
                    findings);
            }
        }
    }

    private static void AddFinding(
        string kind,
        string classification,
        string control,
        IReadOnlyList<string> baseValues,
        IReadOnlyList<string> currentValues,
        ArchitecturePolicyContextWaiver? previous,
        ArchitecturePolicyContextWaiver waiver,
        string severity,
        ICollection<ArchitecturePolicyWeakeningFinding> findings) => findings.Add(CreateFinding(
        new PolicyWeakeningControlContext(kind, control, classification, severity),
        baseValues,
        currentValues,
        previous?.Provenance,
        waiver.Provenance,
        [waiver.ContractId, waiver.WaiverId, waiver.TargetFingerprint],
        waiver.Reason));

    private static bool HasExtendedExpiry(string? previous, string? current) => DateOnly.TryParseExact(
            previous, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out DateOnly previousExpiry)
        && DateOnly.TryParseExact(
            current, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out DateOnly currentExpiry)
        && currentExpiry > previousExpiry;

    private static string WaiverKey(ArchitecturePolicyContextWaiver waiver) => string.Join(
        "\u001f", waiver.Mode, waiver.ContractFamily, waiver.ContractId, waiver.WaiverId);
}
