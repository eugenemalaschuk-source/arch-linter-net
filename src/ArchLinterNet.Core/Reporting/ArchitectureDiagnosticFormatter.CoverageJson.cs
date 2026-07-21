namespace ArchLinterNet.Core.Reporting;

public sealed partial class ArchitectureDiagnosticFormatter
{
    private static Dictionary<string, object?> ToCoverageSummaryJsonObject(ArchitectureCoverageSummary summary)
    {
        return new Dictionary<string, object?>
        {
            ["contract"] = summary.ContractName,
            ["contract_id"] = summary.ContractId,
            ["scope"] = summary.Scope,
            ["counts"] = new Dictionary<string, object?>
            {
                ["covered"] = summary.Counts.Covered,
                ["excluded"] = summary.Counts.Excluded,
                ["uncovered"] = summary.Counts.Uncovered,
                ["stale"] = summary.Counts.Stale,
                ["unknown"] = summary.Counts.Unknown
            },
            ["excluded_items"] = summary.ExcludedItems.OrderBy(item => item.Item, StringComparer.Ordinal).Select(ToExcludedItemJson).ToArray(),
            ["uncovered_items"] = ToEvidenceItemsJson(summary.UncoveredItems),
            ["stale_items"] = ToEvidenceItemsJson(summary.StaleItems),
            ["unknown_items"] = ToEvidenceItemsJson(summary.UnknownItems),
            ["covered_items"] = ToEvidenceItemsJson(summary.CoveredItems)
        };
    }

    private static Dictionary<string, object?>[] ToEvidenceItemsJson(IReadOnlyCollection<ArchitectureCoverageSummaryEvidenceItem> items) =>
        items.OrderBy(item => item.Item, StringComparer.Ordinal).Select(item =>
            new Dictionary<string, object?> { ["item"] = item.Item, ["evidence"] = item.Evidence }).ToArray();

    private static Dictionary<string, object?> ToExcludedItemJson(ArchitectureCoverageSummaryExcludedItem item)
    {
        var result = new Dictionary<string, object?> { ["item"] = item.Item, ["reason"] = item.Reason };
        if (!string.IsNullOrEmpty(item.Evidence)) result["evidence"] = item.Evidence;
        if (item.PolicyLocation is not null) result["policy_location"] = FormatPolicyLocationForJson(item.PolicyLocation);
        return result;
    }
}
