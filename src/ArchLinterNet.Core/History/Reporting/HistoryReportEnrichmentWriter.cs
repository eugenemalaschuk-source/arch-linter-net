using ArchLinterNet.Core.History.Git;

namespace ArchLinterNet.Core.History.Reporting;

// Enrichment is optional report context, so it is rendered in a distinct projection and cannot
// affect any Git-derived evidence, finding, score, rank, or candidate decision.
internal static class HistoryReportEnrichmentWriter
{
    private static readonly IComparer<string> _scalarStringComparer = Comparer<string>.Create(GitPathDecoder.CompareScalarValue);

    public static void Write(CanonicalJsonWriter writer, HistoryEnrichmentProjection enrichment)
    {
        writer.BeginObject("enrichment");
        writer.WriteString("status", StatusText(enrichment.Status));
        if (enrichment.Reason is string reason)
        {
            writer.WriteString("reason", reason);
        }

        WriteItems(writer, "provenance", enrichment.Provenance.Select(static item => (item.Kind, item.Value)));
        WriteItems(writer, "context", enrichment.Context.Select(static item => (item.Kind, item.Value)));
        writer.EndObject();
    }

    private static void WriteItems(CanonicalJsonWriter writer, string propertyName, IEnumerable<(string Kind, string Value)> items)
    {
        writer.BeginArray(propertyName);
        foreach ((string kind, string value) in items.OrderBy(static item => item.Kind, _scalarStringComparer).ThenBy(static item => item.Value, _scalarStringComparer))
        {
            writer.BeginObject();
            writer.WriteString("kind", kind);
            writer.WriteString("value", value);
            writer.EndObject();
        }

        writer.EndArray();
    }

    private static string StatusText(HistoryEnrichmentStatus status) => status switch
    {
        HistoryEnrichmentStatus.NotRequested => "not_requested",
        HistoryEnrichmentStatus.NotApplicable => "not_applicable",
        HistoryEnrichmentStatus.Available => "available",
        HistoryEnrichmentStatus.Unavailable => "unavailable",
        _ => "unavailable",
    };
}
