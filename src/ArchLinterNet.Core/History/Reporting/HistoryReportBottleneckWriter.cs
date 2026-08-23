using ArchLinterNet.Core.History.Analysis;
using ArchLinterNet.Core.History.Evidence;
using ArchLinterNet.Core.History.Tasks;

namespace ArchLinterNet.Core.History.Reporting;

internal static class HistoryReportBottleneckWriter
{
    public static void Write(CanonicalJsonWriter writer, HistoryBottleneckAnalysis analysis)
    {
        writer.BeginArray("bottleneckGroups");
        foreach (HistoryBottleneckCategoryGroup group in analysis.Groups)
        {
            writer.BeginObject();
            writer.WriteString("category", HistoryReportProjectionHelpers.CategoryText(group.Category));
            writer.BeginArray("findings");
            foreach (HistoryBottleneckFinding finding in group.Findings)
            {
                WriteFinding(writer, finding);
            }

            writer.EndArray();
            writer.EndObject();
        }

        writer.EndArray();
    }

    internal static void WriteTaskPair(CanonicalJsonWriter writer, BottleneckTaskPair pair)
    {
        writer.BeginObject();
        HistoryReportProjectionHelpers.WriteTaskKey(writer, "firstTask", pair.First);
        HistoryReportProjectionHelpers.WriteTaskKey(writer, "secondTask", pair.Second);
        WriteInterval(writer, "firstInterval", pair.FirstInterval);
        WriteInterval(writer, "secondInterval", pair.SecondInterval);
        writer.WriteIntegerText("gapSeconds", pair.GapSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture));
        writer.WriteIntegerText("daysBetween", pair.DaysBetween.ToString(System.Globalization.CultureInfo.InvariantCulture));
        writer.WriteCanonicalDecimal("temporalProximity", pair.TemporalProximity);
        HistoryReportProjectionHelpers.WriteStringArray(writer, "firstExclusiveCommitIds", pair.FirstExclusiveCommitIds);
        HistoryReportProjectionHelpers.WriteStringArray(writer, "secondExclusiveCommitIds", pair.SecondExclusiveCommitIds);
        WriteProvenance(writer, "firstProvenance", pair.FirstProvenance);
        WriteProvenance(writer, "secondProvenance", pair.SecondProvenance);
        writer.EndObject();
    }

    private static void WriteFinding(CanonicalJsonWriter writer, HistoryBottleneckFinding finding)
    {
        writer.BeginObject();
        writer.WriteString("id", HistoryReportProjectionHelpers.FindingId("bottleneck", finding.Category, finding.CanonicalPath));
        writer.WriteString("canonicalPath", finding.CanonicalPath);
        HistoryReportProjectionHelpers.WriteStringArray(writer, "aliases", finding.Aliases);
        writer.WriteBoolean("pathnameReuseMayConflateGenerations", finding.RawEvidence.PathnameReuseMayConflateGenerations);
        writer.WriteNumber("independentTaskSpread", finding.RawEvidence.IndependentTaskSpread);
        writer.WriteNumber("distinctAuthorCount", finding.RawEvidence.DistinctAuthorCount);
        writer.WriteCanonicalDecimal("independentTemporalProximity", finding.RawEvidence.IndependentTemporalProximity);
        writer.WriteNumber("distinctNeighborDegree", finding.RawEvidence.DistinctNeighborDegree);
        writer.WriteNumber("incidentCommitDegree", finding.RawEvidence.IncidentCommitDegree);
        writer.WriteNumber("incidentTaskDegree", finding.RawEvidence.IncidentTaskDegree);
        WriteComponents(writer, finding.Components);
        WriteWeights(writer, finding.Weights);
        writer.WriteCanonicalDecimal("score", finding.Score);
        HistoryReportProjectionHelpers.WriteStringArray(writer, "canonicalAuthors", finding.RawEvidence.CanonicalAuthors);
        writer.BeginArray("taskKeys");
        foreach (TaskKey key in finding.RawEvidence.TaskKeys)
        {
            HistoryReportProjectionHelpers.WriteTaskKey(writer, key);
        }

        writer.EndArray();
        writer.BeginArray("independentTaskPairs");
        foreach (BottleneckTaskPair pair in finding.RawEvidence.IndependentTaskPairs)
        {
            WriteTaskPair(writer, pair);
        }

        writer.EndArray();
        writer.EndObject();
    }

    private static void WriteComponents(CanonicalJsonWriter writer, BottleneckComponents components)
    {
        writer.BeginObject("components");
        writer.WriteCanonicalDecimal("independentTask", components.IndependentTask);
        writer.WriteCanonicalDecimal("author", components.Author);
        writer.WriteCanonicalDecimal("temporal", components.Temporal);
        writer.WriteCanonicalDecimal("degree", components.Degree);
        writer.WriteCanonicalDecimal("incidentCommit", components.IncidentCommit);
        writer.WriteCanonicalDecimal("incidentTask", components.IncidentTask);
        writer.WriteCanonicalDecimal("centrality", components.Centrality);
        writer.EndObject();
    }

    private static void WriteWeights(CanonicalJsonWriter writer, BottleneckWeights weights)
    {
        writer.BeginObject("weights");
        writer.WriteCanonicalDecimal("independentTask", weights.IndependentTask);
        writer.WriteCanonicalDecimal("author", weights.Author);
        writer.WriteCanonicalDecimal("temporal", weights.Temporal);
        writer.WriteCanonicalDecimal("degree", weights.Degree);
        writer.WriteCanonicalDecimal("centrality", weights.Centrality);
        writer.EndObject();
    }

    private static void WriteInterval(CanonicalJsonWriter writer, string propertyName, BottleneckTaskInterval interval)
    {
        writer.BeginObject(propertyName);
        writer.WriteIntegerText("startEpochSecond", interval.StartEpochSecond.ToString(System.Globalization.CultureInfo.InvariantCulture));
        writer.WriteIntegerText("endEpochSecond", interval.EndEpochSecond.ToString(System.Globalization.CultureInfo.InvariantCulture));
        writer.EndObject();
    }

    private static void WriteProvenance(CanonicalJsonWriter writer, string propertyName, IReadOnlyList<BottleneckTaskProvenance> provenance)
    {
        writer.BeginArray(propertyName);
        foreach (BottleneckTaskProvenance item in provenance)
        {
            writer.BeginObject();
            writer.WriteString("commitId", item.CommitId);
            writer.WriteString("extractorId", item.Match.ExtractorId);
            writer.WriteNumber("spanStart", item.Match.SpanStart);
            writer.WriteNumber("spanEnd", item.Match.SpanEnd);
            writer.WriteString("text", item.Match.MatchedText);
            writer.EndObject();
        }

        writer.EndArray();
    }
}
