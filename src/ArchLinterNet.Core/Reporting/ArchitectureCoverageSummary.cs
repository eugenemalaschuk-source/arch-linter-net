using ArchLinterNet.Core.Model;

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

    // Populated when the exclusion came from a layer's `exclude` entry (as opposed to a coverage
    // contract's own `exclude` list, which has no single YAML element to point at) - carries the
    // exact `layers.<name>.exclude[<index>]` element location, including imported-fragment
    // provenance, the same way ArchitectureDiagnostic.PolicyLocation does for diagnostics. Null
    // for reasons that don't originate from a single located policy element.
    public ArchitecturePolicySourceLocation? PolicyLocation { get; init; }
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
