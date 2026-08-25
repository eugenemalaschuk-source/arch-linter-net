using ArchLinterNet.Core.History.Analysis;
using ArchLinterNet.Core.History.Evidence;
using ArchLinterNet.Core.History.Tasks;

namespace ArchLinterNet.Core.History.Reporting;

internal static class HistoryReportHotspotWriter
{
    public static void Write(CanonicalJsonWriter writer, HistoryHotspotAnalysis analysis)
    {
        writer.BeginArray("hotspotGroups");
        foreach (HotspotCategoryGroup group in analysis.Groups)
        {
            writer.BeginObject();
            writer.WriteString("category", HistoryReportProjectionHelpers.CategoryText(group.Category));
            writer.BeginArray("findings");
            foreach (HotspotFinding finding in group.Findings)
            {
                WriteFinding(writer, finding);
            }

            writer.EndArray();
            writer.EndObject();
        }

        writer.EndArray();
    }

    private static void WriteFinding(CanonicalJsonWriter writer, HotspotFinding finding)
    {
        writer.BeginObject();
        writer.WriteString("id", HistoryReportProjectionHelpers.FindingId("hotspot", finding.Category, finding.CanonicalPath));
        writer.WriteString("canonicalPath", finding.CanonicalPath);
        HistoryReportProjectionHelpers.WriteStringArray(writer, "aliases", finding.Aliases);
        writer.WriteBoolean("pathnameReuseMayConflateGenerations", HotspotRawEvidence.PathnameReuseMayConflateGenerations);
        writer.WriteNumber("commitCount", finding.RawEvidence.CommitCount);
        writer.WriteNumber("churn", finding.RawEvidence.Churn);
        writer.WriteNumber("taskSpread", finding.RawEvidence.TaskSpread);
        writer.WriteNumber("authorSpread", finding.RawEvidence.AuthorSpread);
        writer.WriteIntegerText("temporalSpanSeconds", finding.RawEvidence.TemporalSpanSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture));
        writer.BeginArray("lineCountStatuses");
        foreach (LineCountStatus status in finding.RawEvidence.LineCountStatuses)
        {
            writer.WriteStringElement(HistoryReportProjectionHelpers.LineCountStatusText(status));
        }

        writer.EndArray();
        writer.BeginObject("components");
        writer.WriteCanonicalDecimal("commit", finding.Components.Commit);
        writer.WriteCanonicalDecimal("churn", finding.Components.Churn);
        writer.WriteCanonicalDecimal("task", finding.Components.Task);
        writer.WriteCanonicalDecimal("author", finding.Components.Author);
        writer.WriteCanonicalDecimal("temporal", finding.Components.Temporal);
        writer.EndObject();
        writer.BeginObject("weights");
        writer.WriteCanonicalDecimal("commit", finding.Weights.Commit);
        writer.WriteCanonicalDecimal("churn", finding.Weights.Churn);
        writer.WriteCanonicalDecimal("task", finding.Weights.Task);
        writer.WriteCanonicalDecimal("author", finding.Weights.Author);
        writer.WriteCanonicalDecimal("temporal", finding.Weights.Temporal);
        writer.EndObject();
        writer.WriteCanonicalDecimal("score", finding.Score);
        writer.BeginArray("taskKeys");
        foreach (TaskKey key in finding.RawEvidence.TaskKeys)
        {
            HistoryReportProjectionHelpers.WriteTaskKey(writer, key);
        }

        writer.EndArray();
        writer.BeginArray("taskKeyProvenance");
        foreach (HotspotTaskKeyProvenance item in finding.RawEvidence.TaskKeyProvenance)
        {
            writer.BeginObject();
            writer.WriteString("commitId", item.CommitId);
            writer.WriteString("extractorId", item.Match.ExtractorId);
            HistoryReportProjectionHelpers.WriteTaskKey(writer, "task", item.Match.Key);
            writer.WriteNumber("spanStart", item.Match.SpanStart);
            writer.WriteNumber("spanEnd", item.Match.SpanEnd);
            writer.WriteString("text", item.Match.MatchedText);
            writer.EndObject();
        }

        writer.EndArray();
        HistoryReportProjectionHelpers.WriteStringArray(writer, "canonicalAuthors", finding.RawEvidence.CanonicalAuthors);
        writer.BeginArray("authorProvenance");
        foreach (HotspotAuthorProvenance item in finding.RawEvidence.AuthorProvenance)
        {
            writer.BeginObject();
            writer.WriteString("commitId", item.CommitId);
            writer.WriteString("author", item.CanonicalAuthor);
            writer.EndObject();
        }

        writer.EndArray();
        writer.EndObject();
    }
}
