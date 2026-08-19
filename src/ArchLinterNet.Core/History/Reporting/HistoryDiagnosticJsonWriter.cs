namespace ArchLinterNet.Core.History.Reporting;

// The fail-closed error surface. It is deliberately a separate document from the ingestion result so
// a diagnostic can never be mistaken for a record inside a successful report.
internal static class HistoryDiagnosticJsonWriter
{
    public static string Write(HistoryDiagnostic diagnostic)
    {
        CanonicalJsonWriter writer = new();
        writer.BeginObject();
        writer.WriteString("kind", diagnostic.KindText);
        writer.WriteString("message", diagnostic.Message);
        writer.WriteString("objectId", diagnostic.ObjectId);
        writer.WriteString("path", diagnostic.Path);
        if (diagnostic.SpanStart is int start && diagnostic.SpanEnd is int end)
        {
            writer.WriteNumber("spanStart", start);
            writer.WriteNumber("spanEnd", end);
        }

        writer.EndObject();
        return writer.ToCanonicalText() + "\n";
    }
}
