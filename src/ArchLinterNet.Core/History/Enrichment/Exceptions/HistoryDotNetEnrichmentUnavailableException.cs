namespace ArchLinterNet.Core.History.Enrichment;

// NOSONAR: internal control-flow signal never crosses the Core assembly boundary.
internal sealed class HistoryDotNetEnrichmentUnavailableException(string reason) : Exception(reason)
{
    public string Reason { get; } = reason;
}
