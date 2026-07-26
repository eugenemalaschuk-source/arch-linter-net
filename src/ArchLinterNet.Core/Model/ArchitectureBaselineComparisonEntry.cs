namespace ArchLinterNet.Core.Model;

/// <summary>
/// One baseline entry (or one current violation candidate) as seen by baseline comparison.
/// <paramref name="Issue"/> is informational tracking metadata carried verbatim through update,
/// prune, and migrate; it never participates in identity, matching, or deduplication.
/// <paramref name="CurrentForbiddenReference"/> is set only for entries whose identity matched a
/// live candidate, and records the display text that candidate produces now — that is what lets a
/// write report an entry as <see cref="BaselineEntryLifecycle.Changed"/> rather than
/// <see cref="BaselineEntryLifecycle.Kept"/> when only the display text drifted.
/// </summary>
public sealed record ArchitectureBaselineComparisonEntry(
    string ContractGroup,
    string ContractId,
    string SourceType,
    string ForbiddenReference,
    string? Reason,
    ArchitectureViolationIdentity? Identity = null)
{
    public string? Issue { get; init; }

    public string? CurrentForbiddenReference { get; init; }
}

public sealed record ArchitectureBaselineComparisonResult(
    IReadOnlyList<ArchitectureBaselineComparisonEntry> New,
    IReadOnlyList<ArchitectureBaselineComparisonEntry> Frozen,
    IReadOnlyList<ArchitectureBaselineComparisonEntry> Resolved,
    IReadOnlyList<ArchitectureBaselineComparisonEntry> ConfigurationErrors,
    IReadOnlyList<ArchitectureBaselineComparisonEntry> OutOfScope)
{
    /// <summary>
    /// Baseline entries that correlate to more than one current candidate. Under-specified identity
    /// (a version-1 legacy pair, most often) means one entry would suppress several distinct
    /// violations, so these are never rewritten, removed, or counted as matched — they are reported
    /// for manual review, the same way `baseline migrate` fails closed on them.
    /// </summary>
    public IReadOnlyList<ArchitectureBaselineComparisonEntry> Ambiguous { get; init; } =
        Array.Empty<ArchitectureBaselineComparisonEntry>();
}
