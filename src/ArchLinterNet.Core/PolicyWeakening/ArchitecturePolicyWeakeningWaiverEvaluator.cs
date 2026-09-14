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
                    new PolicyWeakeningControlContext("structured_waiver_added", control, "semantic", severity),
                    Array.Empty<string>(),
                    [string.Join("; ", waiver.TargetFingerprint, waiver.ContractFamily, waiver.ContractId)],
                    null,
                    waiver,
                    findings);
                continue;
            }

            if (!string.Equals(previous!.TargetFingerprint, waiver.TargetFingerprint, StringComparison.Ordinal))
            {
                AddFinding(
                    new PolicyWeakeningControlContext("structured_waiver_target_changed", control, "impact_not_proven", severity),
                    [previous.TargetFingerprint],
                    [string.Join("; ", waiver.TargetFingerprint, waiver.ContractFamily, waiver.ContractId)],
                    previous,
                    waiver,
                    findings);
            }

            if (HasExtendedExpiry(previous.Expires, waiver.Expires))
            {
                AddFinding(
                    new PolicyWeakeningControlContext("structured_waiver_expiry_extended", control, "semantic", severity),
                    [previous.Expires!],
                    [waiver.Expires!],
                    previous,
                    waiver,
                    findings);
            }
        }
    }

    private static void AddFinding(
        PolicyWeakeningControlContext controlContext,
        IReadOnlyList<string> baseValues,
        IReadOnlyList<string> currentValues,
        ArchitecturePolicyContextWaiver? previous,
        ArchitecturePolicyContextWaiver waiver,
        ICollection<ArchitecturePolicyWeakeningFinding> findings) => findings.Add(CreateFinding(
        controlContext,
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
