namespace ArchLinterNet.Core.Reporting;

public sealed record ArchitectureCoverageSummaryCounts(
    int Covered,
    int Excluded,
    int Uncovered,
    int Stale,
    int Unknown);

public sealed record ArchitectureCoverageSummaryExcludedItem
{
    public ArchitectureCoverageSummaryExcludedItem(string item, string reason)
        : this(item, reason, string.Empty)
    {
    }

    public ArchitectureCoverageSummaryExcludedItem(string item, string reason, string evidence)
    {
        Item = item;
        Reason = reason;
        Evidence = evidence;
    }

    public string Item { get; }

    public string Reason { get; }

    public string Evidence { get; }
}

public sealed record ArchitectureCoverageSummaryEvidenceItem(string Item, string Evidence);

public sealed record ArchitectureCoverageSummary(
    string ContractName,
    string? ContractId,
    string Scope,
    ArchitectureCoverageSummaryCounts Counts,
    IReadOnlyCollection<ArchitectureCoverageSummaryExcludedItem> ExcludedItems,
    IReadOnlyCollection<ArchitectureCoverageSummaryEvidenceItem> UncoveredItems,
    IReadOnlyCollection<ArchitectureCoverageSummaryEvidenceItem> StaleItems,
    IReadOnlyCollection<ArchitectureCoverageSummaryEvidenceItem> UnknownItems,
    IReadOnlyCollection<ArchitectureCoverageSummaryEvidenceItem> CoveredItems);
