using ArchLinterNet.Core.History.Evidence;
using ArchLinterNet.Core.History.Tasks;

namespace ArchLinterNet.Core.History.Reporting;

internal static class HistoryReportCoChangeWriter
{
    public static void Write(CanonicalJsonWriter writer, HistoryIngestionResult result)
    {
        CoChangeGraph graph = result.CoChangeGraph;
        writer.BeginObject("coChangeGraph");
        writer.BeginObject("weights");
        writer.WriteCanonicalDecimal("commit", graph.CommitWeight);
        writer.WriteCanonicalDecimal("task", graph.TaskWeight);
        writer.EndObject();
        writer.WriteOptionalCanonicalDecimal("significanceThreshold", graph.SignificanceThreshold);
        WriteVertices(writer, result, graph);
        WritePairs(writer, graph);
        WriteClusters(writer, graph);
        writer.EndObject();
    }

    private static void WriteVertices(CanonicalJsonWriter writer, HistoryIngestionResult result, CoChangeGraph graph)
    {
        writer.BeginArray("vertices");
        foreach (CoChangeVertex vertex in graph.Vertices)
        {
            writer.BeginObject();
            writer.WriteString("canonicalPath", vertex.CanonicalPath);
            writer.WriteString("category", HistoryReportProjectionHelpers.CategoryText(vertex.Category));
            writer.BeginArray("renameComponentIndexes");
            foreach (RenameComponent component in vertex.RenameComponents)
            {
                writer.WriteNumberElement(HistoryReportProjectionHelpers.IndexOfReference(result.RenameComponents, component));
            }

            writer.EndArray();
            writer.EndObject();
        }

        writer.EndArray();
    }

    private static void WritePairs(CanonicalJsonWriter writer, CoChangeGraph graph)
    {
        writer.BeginArray("pairs");
        foreach (CoChangePair pair in graph.Pairs)
        {
            writer.BeginObject();
            writer.WriteString("firstPath", pair.First.CanonicalPath);
            writer.WriteString("secondPath", pair.Second.CanonicalPath);
            WriteCohort(writer, pair.Cohort);
            writer.WriteNumber("commitCoChange", pair.CommitCoChange);
            writer.WriteNumber("taskCoChange", pair.TaskCoChange);
            writer.WriteBoolean("isBaseEdge", pair.IsBaseEdge);
            writer.WriteOptionalNumber("cohortRank", pair.CohortRank);
            writer.WriteOptionalCanonicalDecimal("commitComponent", pair.CommitComponent);
            writer.WriteOptionalCanonicalDecimal("taskComponent", pair.TaskComponent);
            writer.WriteOptionalCanonicalDecimal("combinedCoChange", pair.CombinedCoChange);
            HistoryReportProjectionHelpers.WriteStringArray(writer, "commitIds", pair.CommitIds);
            writer.BeginArray("taskKeys");
            foreach (TaskKey key in pair.TaskKeys)
            {
                HistoryReportProjectionHelpers.WriteTaskKey(writer, key);
            }

            writer.EndArray();
            writer.EndObject();
        }

        writer.EndArray();
    }

    private static void WriteClusters(CanonicalJsonWriter writer, CoChangeGraph graph)
    {
        writer.BeginArray("clusters");
        foreach (CoChangeCluster cluster in graph.Clusters)
        {
            writer.BeginObject();
            writer.WriteString("id", HistoryReportProjectionHelpers.ClusterId(cluster));
            WriteCohort(writer, cluster.Cohort);
            writer.WriteCanonicalDecimal("maximum", cluster.Maximum);
            writer.WriteCanonicalDecimal("aggregate", cluster.Aggregate);
            writer.BeginArray("members");
            foreach (CoChangeVertex member in cluster.Members)
            {
                writer.WriteStringElement(member.CanonicalPath);
            }

            writer.EndArray();
            writer.BeginArray("edges");
            foreach (CoChangePair edge in cluster.Edges)
            {
                writer.BeginObject();
                writer.WriteString("firstPath", edge.First.CanonicalPath);
                writer.WriteString("secondPath", edge.Second.CanonicalPath);
                writer.EndObject();
            }

            writer.EndArray();
            writer.EndObject();
        }

        writer.EndArray();
    }

    private static void WriteCohort(CanonicalJsonWriter writer, CoChangeCohort cohort)
    {
        writer.BeginObject("cohort");
        writer.WriteString("firstCategory", HistoryReportProjectionHelpers.CategoryText(cohort.First));
        writer.WriteString("secondCategory", HistoryReportProjectionHelpers.CategoryText(cohort.Second));
        writer.EndObject();
    }

}
