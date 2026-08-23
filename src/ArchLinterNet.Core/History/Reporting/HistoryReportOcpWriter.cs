using ArchLinterNet.Core.History.Analysis;
using ArchLinterNet.Core.History.Evidence;
using ArchLinterNet.Core.History.Tasks;

namespace ArchLinterNet.Core.History.Reporting;

internal static class HistoryReportOcpWriter
{
    public static void Write(CanonicalJsonWriter writer, HistoryOcpAnalysis analysis)
    {
        writer.BeginArray("ocpGroups");
        foreach (HistoryOcpCategoryGroup group in analysis.Groups)
        {
            writer.BeginObject();
            writer.WriteString("category", HistoryReportProjectionHelpers.CategoryText(group.Category));
            writer.BeginArray("findings");
            foreach (HistoryOcpFinding finding in group.Findings)
            {
                WriteFinding(writer, finding);
            }

            writer.EndArray();
            writer.EndObject();
        }

        writer.EndArray();
    }

    private static void WriteFinding(CanonicalJsonWriter writer, HistoryOcpFinding finding)
    {
        writer.BeginObject();
        writer.WriteString("id", HistoryReportProjectionHelpers.FindingId("ocp-pressure", finding.Category, finding.CanonicalPath));
        writer.WriteString("canonicalPath", finding.CanonicalPath);
        HistoryReportProjectionHelpers.WriteStringArray(writer, "aliases", finding.Aliases);
        writer.WriteBoolean("pathnameReuseMayConflateGenerations", finding.RawEvidence.PathnameReuseMayConflateGenerations);
        writer.WriteNumber("independentTaskSpread", finding.RawEvidence.IndependentTaskSpread);
        writer.WriteNumber("incidentCommitDegree", finding.RawEvidence.IncidentCommitDegree);
        writer.WriteNumber("incidentTaskDegree", finding.RawEvidence.IncidentTaskDegree);
        writer.WriteNumber("repeatedEditTotal", finding.RawEvidence.RepeatedEditTotal);
        writer.WriteCanonicalDecimal("roleHint", finding.RawEvidence.RoleHint);
        writer.BeginObject("components");
        writer.WriteCanonicalDecimal("independentTask", finding.Components.IndependentTask);
        writer.WriteCanonicalDecimal("centrality", finding.Components.Centrality);
        writer.WriteCanonicalDecimal("repeatedEdit", finding.Components.RepeatedEdit);
        writer.WriteCanonicalDecimal("roleHint", finding.Components.RoleHint);
        writer.EndObject();
        writer.BeginObject("weights");
        writer.WriteCanonicalDecimal("independentTask", finding.Weights.IndependentTask);
        writer.WriteCanonicalDecimal("centrality", finding.Weights.Centrality);
        writer.WriteCanonicalDecimal("repeatedEdit", finding.Weights.RepeatedEdit);
        writer.WriteCanonicalDecimal("roleHint", finding.Weights.RoleHint);
        writer.EndObject();
        writer.WriteCanonicalDecimal("score", finding.Score);
        writer.BeginArray("taskKeys");
        foreach (TaskKey key in finding.RawEvidence.TaskKeys)
        {
            HistoryReportProjectionHelpers.WriteTaskKey(writer, key);
        }

        writer.EndArray();
        writer.BeginArray("independentTaskPairs");
        foreach (BottleneckTaskPair pair in finding.RawEvidence.IndependentTaskPairs)
        {
            HistoryReportBottleneckWriter.WriteTaskPair(writer, pair);
        }

        writer.EndArray();
        writer.BeginArray("repeatedEdits");
        foreach (OcpTaskRepeatedEdit repeated in finding.RawEvidence.RepeatedEdits)
        {
            writer.BeginObject();
            HistoryReportProjectionHelpers.WriteTaskKey(writer, "task", repeated.TaskKey);
            HistoryReportProjectionHelpers.WriteStringArray(writer, "qualifyingCommitIds", repeated.QualifyingCommitIds);
            writer.WriteNumber("repeatedEditCount", repeated.RepeatedEditCount);
            writer.EndObject();
        }

        writer.EndArray();
        HistoryReportProjectionHelpers.WriteStringArray(writer, "roleTokens", finding.RawEvidence.RoleTokens);
        writer.EndObject();
    }
}
