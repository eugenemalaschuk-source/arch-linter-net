namespace ArchLinterNet.Core.History.Reporting;

internal static class HistoryReportAnalysisWriter
{
    public static void Write(CanonicalJsonWriter writer, HistoryIngestionResult result)
    {
        writer.BeginObject("analysis");
        writer.WriteString("objectFormat", result.ObjectFormatName);
        writer.BeginObject("range");
        writer.WriteString("authoredFrom", result.AuthoredFrom);
        writer.WriteString("authoredTo", result.AuthoredTo);
        writer.WriteString("resolvedFrom", result.ResolvedFrom);
        writer.WriteString("resolvedTo", result.ResolvedTo);
        writer.EndObject();
        writer.WriteNumber("analyzedCommitCount", result.Commits.Count);
        writer.WriteNumber("excludedMergeCount", result.ExcludedMergeCount);
        WriteConfiguration(writer, result.Configuration);
        writer.EndObject();
    }

    private static void WriteConfiguration(CanonicalJsonWriter writer, Contracts.HistoryAnalysisConfiguration configuration)
    {
        writer.BeginObject("historyAnalysisConfiguration");
        writer.BeginArray("builtInExtractors");
        writer.WriteStringElement("issue");
        writer.EndArray();
        writer.BeginArray("extractors");
        foreach (Contracts.HistoryTaskExtractorConfiguration extractor in configuration.Extractors.OrderBy(static item => item.Id, HistoryReportProjectionHelpers.ScalarStringComparer))
        {
            writer.BeginObject();
            writer.WriteString("id", extractor.Id);
            writer.WriteString("namespace", extractor.Namespace);
            writer.BeginObject("pattern");
            writer.WriteString("prefix", extractor.Pattern.Prefix);
            writer.WriteString("suffix", extractor.Pattern.Suffix);
            writer.EndObject();
            writer.EndObject();
        }

        writer.EndArray();
        writer.BeginObject("paths");
        WriteSortedStrings(writer, "production", configuration.Paths.Production);
        WriteSortedStrings(writer, "tests", configuration.Paths.Tests);
        WriteSortedStrings(writer, "docs", configuration.Paths.Docs);
        WriteSortedStrings(writer, "generated", configuration.Paths.Generated);
        WriteSortedStrings(writer, "buildCi", configuration.Paths.BuildCi);
        WriteSortedStrings(writer, "samplesExamples", configuration.Paths.SamplesExamples);
        writer.EndObject();
        WriteSortedStrings(writer, "ignore", configuration.Ignore);
        writer.BeginObject("weights");
        writer.BeginObject("hotspot");
        writer.WriteCanonicalDecimal("commit", configuration.Weights.Hotspot.Commit);
        writer.WriteCanonicalDecimal("churn", configuration.Weights.Hotspot.Churn);
        writer.WriteCanonicalDecimal("task", configuration.Weights.Hotspot.Task);
        writer.WriteCanonicalDecimal("author", configuration.Weights.Hotspot.Author);
        writer.WriteCanonicalDecimal("temporal", configuration.Weights.Hotspot.Temporal);
        writer.EndObject();
        writer.BeginObject("coChange");
        writer.WriteCanonicalDecimal("commit", configuration.Weights.CoChange.Commit);
        writer.WriteCanonicalDecimal("task", configuration.Weights.CoChange.Task);
        writer.EndObject();
        writer.BeginObject("bottleneck");
        writer.WriteCanonicalDecimal("independentTask", configuration.Weights.Bottleneck.IndependentTask);
        writer.WriteCanonicalDecimal("author", configuration.Weights.Bottleneck.Author);
        writer.WriteCanonicalDecimal("temporal", configuration.Weights.Bottleneck.Temporal);
        writer.WriteCanonicalDecimal("degree", configuration.Weights.Bottleneck.Degree);
        writer.WriteCanonicalDecimal("centrality", configuration.Weights.Bottleneck.Centrality);
        writer.EndObject();
        writer.BeginObject("ocp");
        writer.WriteCanonicalDecimal("independentTask", configuration.Weights.Ocp.IndependentTask);
        writer.WriteCanonicalDecimal("centrality", configuration.Weights.Ocp.Centrality);
        writer.WriteCanonicalDecimal("repeatedEdit", configuration.Weights.Ocp.RepeatedEdit);
        writer.WriteCanonicalDecimal("roleHint", configuration.Weights.Ocp.RoleHint);
        writer.EndObject();
        writer.EndObject();
        writer.BeginObject("thresholds");
        writer.WriteOptionalCanonicalDecimal("coChangeSignificance", configuration.Thresholds.CoChangeSignificance);
        writer.EndObject();
        writer.EndObject();
    }

    private static void WriteSortedStrings(CanonicalJsonWriter writer, string propertyName, IEnumerable<string> values)
    {
        writer.BeginArray(propertyName);
        foreach (string value in values.OrderBy(static item => item, HistoryReportProjectionHelpers.ScalarStringComparer))
        {
            writer.WriteStringElement(value);
        }

        writer.EndArray();
    }
}
