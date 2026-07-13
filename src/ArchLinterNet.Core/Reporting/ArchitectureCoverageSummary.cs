namespace ArchLinterNet.Core.Reporting;

public sealed record ArchitectureCoverageSummaryCounts(
    int Covered,
    int Excluded,
    int Uncovered,
    int Stale,
    int Unknown);

public sealed record ArchitectureCoverageSummaryExcludedItem(string Item, string Reason)
{
    public ArchitectureCoverageSummaryExcludedItem(string item, string reason, string evidence)
        : this(item, reason)
    {
        Evidence = evidence;
    }

    public string Evidence { get; init; } = string.Empty;
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
