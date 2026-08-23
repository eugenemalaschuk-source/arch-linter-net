using ArchLinterNet.Core.History.Canonical;
using ArchLinterNet.Core.History.Configuration;
using ArchLinterNet.Core.History.Evidence;
using ArchLinterNet.Core.History.Tasks;

namespace ArchLinterNet.Core.History.Reporting;

// Canonical value and identity details are shared only by report projections; they never construct
// evidence or influence a score, rank, or candidate decision.
internal static class HistoryReportProjectionHelpers
{
    public static IComparer<string> ScalarStringComparer { get; } = HistoryScalarValueComparer.Instance;

    public static void WriteTaskKey(CanonicalJsonWriter writer, TaskKey key)
    {
        writer.BeginObject();
        writer.WriteString("namespace", key.Namespace);
        writer.WriteIntegerText("id", key.IdText);
        writer.EndObject();
    }

    public static void WriteTaskKey(CanonicalJsonWriter writer, string propertyName, TaskKey key)
    {
        writer.BeginObject(propertyName);
        writer.WriteString("namespace", key.Namespace);
        writer.WriteIntegerText("id", key.IdText);
        writer.EndObject();
    }

    public static void WriteStringArray(CanonicalJsonWriter writer, string propertyName, IReadOnlyList<string> values)
    {
        writer.BeginArray(propertyName);
        foreach (string value in values)
        {
            writer.WriteStringElement(value);
        }

        writer.EndArray();
    }

    public static string CategoryText(HistoryPathCategory category) => category switch
    {
        HistoryPathCategory.Production => "production",
        HistoryPathCategory.Tests => "tests",
        HistoryPathCategory.Docs => "docs",
        HistoryPathCategory.Generated => "generated",
        HistoryPathCategory.BuildCi => "build_ci",
        HistoryPathCategory.SamplesExamples => "samples_examples",
        HistoryPathCategory.Unknown => "unknown",
        _ => "unknown",
    };

    public static string FindingId(string kind, HistoryPathCategory category, string path)
        => $"{kind}:{CategoryText(category)}:{path}";

    public static string ClusterId(CoChangeCluster cluster)
    {
        string members = string.Concat(cluster.Members.Select(static item => $"{item.CanonicalPath.Length}:{item.CanonicalPath}"));
        return $"co-change-cluster:{CategoryText(cluster.Cohort.First)}:{CategoryText(cluster.Cohort.Second)}:{members}";
    }

    public static string LineCountStatusText(LineCountStatus status) => status switch
    {
        LineCountStatus.Text => "text",
        LineCountStatus.ExactRename => "exact_rename",
        LineCountStatus.BinaryOrUnavailable => "binary_or_unavailable",
        _ => "unknown",
    };
}
