namespace ArchLinterNet.Core.History;

// A versioned downstream boundary for optional enrichment. Git-level analysis always completes
// before this projection is considered, so enrichment can never become canonical evidence.
internal enum HistoryEnrichmentStatus
{
    NotRequested,
    NotApplicable,
    Available,
    Unavailable,
}

internal sealed class HistoryEnrichmentProvenance(string kind, string value)
{
    public string Kind { get; } = kind;

    public string Value { get; } = value;
}

internal sealed class HistoryEnrichmentContext(string kind, string value)
{
    public string Kind { get; } = kind;

    public string Value { get; } = value;
}

internal sealed class HistoryEnrichmentProjection(
    HistoryEnrichmentStatus status,
    string? reason = null,
    IReadOnlyList<HistoryEnrichmentProvenance>? provenance = null,
    IReadOnlyList<HistoryEnrichmentContext>? context = null)
{
    public static HistoryEnrichmentProjection NotRequested { get; } = new(HistoryEnrichmentStatus.NotRequested);

    public HistoryEnrichmentStatus Status { get; } = status;

    public string? Reason { get; } = reason;

    public IReadOnlyList<HistoryEnrichmentProvenance> Provenance { get; } = provenance ?? [];

    public IReadOnlyList<HistoryEnrichmentContext> Context { get; } = context ?? [];
}
