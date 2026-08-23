namespace ArchLinterNet.Core.History.Reporting;

// The versioned successful report is a read-only projection of finalized canonical evidence. It
// neither reads Git/policy input nor recalculates a finding, preserving the fail-closed boundary.
internal static class HistoryIngestionJsonWriter
{
    private const string ReportKind = "release-architecture-forensics";
    private const string HistorySemanticsVersion = "v1";

    public static string Write(HistoryIngestionResult result)
    {
        CanonicalJsonWriter writer = new();
        writer.BeginObject();
        writer.WriteNumber("schemaVersion", 1);
        writer.WriteString("kind", ReportKind);
        writer.WriteString("historySemanticsVersion", HistorySemanticsVersion);
        writer.WriteString("toolVersion", ToolVersion());
        HistoryReportAnalysisWriter.Write(writer, result);
        HistoryReportEvidenceWriter.Write(writer, result);
        HistoryReportHotspotWriter.Write(writer, result.HotspotAnalysis);
        HistoryReportCoChangeWriter.Write(writer, result);
        HistoryReportBottleneckWriter.Write(writer, result.BottleneckAnalysis);
        HistoryReportOcpWriter.Write(writer, result.OcpAnalysis);
        HistoryReportEnrichmentWriter.Write(writer, result.Enrichment);
        HistoryReportCandidateWriter.Write(writer, result);
        writer.EndObject();
        return writer.ToCanonicalText() + "\n";
    }

    private static string ToolVersion() => typeof(HistoryIngestionJsonWriter).Assembly.GetName().Version?.ToString(3) ?? "unknown";
}
