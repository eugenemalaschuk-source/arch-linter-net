using ArchLinterNet.Core.History.Analysis;

namespace ArchLinterNet.Core.History.Reporting;

// Candidates are a report-only view of positive finalized findings and Gtheta clusters. Keeping
// them separate from the evidence writer prevents their heuristic interpretation from feeding
// back into canonical scores, ranks, or graph construction.
internal static class HistoryReportCandidateWriter
{
    public static void Write(CanonicalJsonWriter writer, HistoryIngestionResult result)
    {
        writer.BeginArray("candidates");
        foreach (HotspotFinding finding in result.HotspotAnalysis.Findings.Where(static item => item.Score > 0m))
        {
            WriteHotspot(writer, finding);
        }

        if (result.CoChangeGraph.SignificanceThreshold is decimal threshold)
        {
            foreach (CoChangeCluster cluster in result.CoChangeGraph.Clusters)
            {
                WriteCluster(writer, cluster, threshold);
            }
        }

        foreach (HistoryBottleneckFinding finding in result.BottleneckAnalysis.Findings.Where(static item => item.Score > 0m))
        {
            WriteBottleneck(writer, finding);
        }

        foreach (HistoryOcpFinding finding in result.OcpAnalysis.Findings.Where(static item => item.Score > 0m))
        {
            WriteOcpPressure(writer, finding);
        }

        writer.EndArray();
    }

    private static void WriteHotspot(CanonicalJsonWriter writer, HotspotFinding finding)
    {
        string sourceId = HistoryIngestionJsonWriter.FindingId("hotspot", finding.Category, finding.CanonicalPath);
        Start(writer, "hotspot", sourceId, [finding.CanonicalPath]);
        writer.BeginObject("qualification");
        writer.WriteCanonicalDecimal("score", finding.Score);
        writer.WriteCanonicalDecimal("minimumScoreExclusive", 0m);
        writer.BeginObject("components");
        writer.WriteCanonicalDecimal("commit", finding.Components.Commit);
        writer.WriteCanonicalDecimal("churn", finding.Components.Churn);
        writer.WriteCanonicalDecimal("task", finding.Components.Task);
        writer.WriteCanonicalDecimal("author", finding.Components.Author);
        writer.WriteCanonicalDecimal("temporal", finding.Components.Temporal);
        writer.EndObject();
        writer.EndObject();
        WriteStrings(writer, "caveats", ["heuristic_investigation", "pathname_reuse_may_conflate_generations", "score_is_category_local"]);
        writer.EndObject();
    }

    private static void WriteCluster(CanonicalJsonWriter writer, CoChangeCluster cluster, decimal threshold)
    {
        string sourceId = HistoryIngestionJsonWriter.ClusterId(cluster);
        string[] members = [.. cluster.Members.Select(static item => item.CanonicalPath)];
        Start(writer, "co_change_cluster", sourceId, members);
        writer.BeginObject("qualification");
        writer.WriteCanonicalDecimal("significanceThreshold", threshold);
        writer.WriteCanonicalDecimal("maximum", cluster.Maximum);
        writer.WriteCanonicalDecimal("aggregate", cluster.Aggregate);
        writer.BeginArray("qualifyingEdges");
        foreach (CoChangePair edge in cluster.Edges)
        {
            writer.BeginObject();
            writer.WriteString("firstPath", edge.First.CanonicalPath);
            writer.WriteString("secondPath", edge.Second.CanonicalPath);
            writer.WriteCanonicalDecimal("combinedCoChange", edge.CombinedCoChange!.Value);
            writer.EndObject();
        }

        writer.EndArray();
        writer.EndObject();
        WriteStrings(writer, "caveats", ["heuristic_investigation", "co_change_is_not_ownership_proof", "threshold_does_not_rescore_files"]);
        writer.EndObject();
    }

    private static void WriteBottleneck(CanonicalJsonWriter writer, HistoryBottleneckFinding finding)
    {
        string sourceId = HistoryIngestionJsonWriter.FindingId("bottleneck", finding.Category, finding.CanonicalPath);
        Start(writer, "bottleneck", sourceId, [finding.CanonicalPath]);
        writer.BeginObject("qualification");
        writer.WriteCanonicalDecimal("score", finding.Score);
        writer.WriteCanonicalDecimal("minimumScoreExclusive", 0m);
        writer.BeginObject("components");
        writer.WriteCanonicalDecimal("independentTask", finding.Components.IndependentTask);
        writer.WriteCanonicalDecimal("author", finding.Components.Author);
        writer.WriteCanonicalDecimal("temporal", finding.Components.Temporal);
        writer.WriteCanonicalDecimal("degree", finding.Components.Degree);
        writer.WriteCanonicalDecimal("centrality", finding.Components.Centrality);
        writer.EndObject();
        writer.EndObject();
        WriteStrings(writer, "caveats", ["heuristic_investigation", "does_not_prove_merge_conflict", "pathname_reuse_may_conflate_generations"]);
        writer.EndObject();
    }

    private static void WriteOcpPressure(CanonicalJsonWriter writer, HistoryOcpFinding finding)
    {
        string sourceId = HistoryIngestionJsonWriter.FindingId("ocp-pressure", finding.Category, finding.CanonicalPath);
        Start(writer, "ocp_pressure", sourceId, [finding.CanonicalPath]);
        writer.BeginObject("qualification");
        writer.WriteCanonicalDecimal("score", finding.Score);
        writer.WriteCanonicalDecimal("minimumScoreExclusive", 0m);
        writer.BeginObject("components");
        writer.WriteCanonicalDecimal("independentTask", finding.Components.IndependentTask);
        writer.WriteCanonicalDecimal("centrality", finding.Components.Centrality);
        writer.WriteCanonicalDecimal("repeatedEdit", finding.Components.RepeatedEdit);
        writer.WriteCanonicalDecimal("roleHint", finding.Components.RoleHint);
        writer.EndObject();
        writer.EndObject();
        WriteStrings(writer, "caveats", ["heuristic_investigation", "does_not_prove_ocp_violation", "pathname_reuse_may_conflate_generations"]);
        writer.EndObject();
    }

    private static void Start(CanonicalJsonWriter writer, string kind, string sourceId, IReadOnlyList<string> paths)
    {
        writer.BeginObject();
        writer.WriteString("id", $"{kind}-investigation:{sourceId}");
        writer.WriteString("kind", kind);
        WriteStrings(writer, "sourceFindingIds", [sourceId]);
        WriteStrings(writer, "affectedPaths", paths);
    }

    private static void WriteStrings(CanonicalJsonWriter writer, string propertyName, IReadOnlyList<string> values)
    {
        writer.BeginArray(propertyName);
        foreach (string value in values)
        {
            writer.WriteStringElement(value);
        }

        writer.EndArray();
    }
}
