using ArchLinterNet.Core.History.Analysis;
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

    public static int IndexOfReference<T>(IReadOnlyList<T> items, T item)
        where T : class
    {
        for (int index = 0; index < items.Count; index++)
        {
            if (ReferenceEquals(items[index], item))
            {
                return index;
            }
        }

        return -1;
    }

    public static void WriteTaskPair(CanonicalJsonWriter writer, BottleneckTaskPair pair)
    {
        writer.BeginObject();
        WriteTaskKey(writer, "firstTask", pair.First);
        WriteTaskKey(writer, "secondTask", pair.Second);
        WriteInterval(writer, "firstInterval", pair.FirstInterval);
        WriteInterval(writer, "secondInterval", pair.SecondInterval);
        writer.WriteIntegerText("gapSeconds", pair.GapSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture));
        writer.WriteIntegerText("daysBetween", pair.DaysBetween.ToString(System.Globalization.CultureInfo.InvariantCulture));
        writer.WriteCanonicalDecimal("temporalProximity", pair.TemporalProximity);
        WriteStringArray(writer, "firstExclusiveCommitIds", pair.FirstExclusiveCommitIds);
        WriteStringArray(writer, "secondExclusiveCommitIds", pair.SecondExclusiveCommitIds);
        WriteProvenance(writer, "firstProvenance", pair.FirstProvenance);
        WriteProvenance(writer, "secondProvenance", pair.SecondProvenance);
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
