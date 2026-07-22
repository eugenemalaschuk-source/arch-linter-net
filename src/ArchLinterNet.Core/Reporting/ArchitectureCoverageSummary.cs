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
    // for reasons that don't originate from a single located policy element. When more than one
    // exclude entry independently matches the same namespace (overlapping patterns, possibly
    // across different layers or different imported fragments), this is the first contributor in
    // deterministic order and RelatedPolicyLocations carries the rest - mirroring
    // ArchitectureDiagnostic.PolicyLocation/RelatedPolicyLocations so no participant is dropped.
    public ArchitecturePolicySourceLocation? PolicyLocation { get; init; }

    public IReadOnlyCollection<ArchitecturePolicySourceLocation> RelatedPolicyLocations { get; init; } =
        Array.Empty<ArchitecturePolicySourceLocation>();
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
