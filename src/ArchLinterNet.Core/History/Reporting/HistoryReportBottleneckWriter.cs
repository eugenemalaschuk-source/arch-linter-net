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
            HistoryReportProjectionHelpers.WriteTaskPair(writer, pair);
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

}
