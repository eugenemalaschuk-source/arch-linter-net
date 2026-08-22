using System.Globalization;
using System.Text;
using ArchLinterNet.Core.History.Analysis;
using ArchLinterNet.Core.History.Configuration;
using ArchLinterNet.Core.History.Evidence;

namespace ArchLinterNet.Core.History.Reporting;

// A deterministic human-readable view of finalized evidence. Markdown is intentionally not an
// input to canonical JSON rendering and therefore cannot influence report artifact identity.
internal static class HistoryIngestionTextWriter
{
    public static string Write(HistoryIngestionResult result)
    {
        StringBuilder text = new();
        Append(text, "# Release Architecture Forensics");
        Append(text, string.Empty);
        Append(text, "## Analysis identity");
        Append(text, $"- Object format: `{result.ObjectFormatName}`");
        Append(text, $"- From: `{result.AuthoredFrom}` → `{result.ResolvedFrom}`");
        Append(text, $"- To: `{result.AuthoredTo}` → `{result.ResolvedTo}`");
        Append(text, $"- Analyzed commits: {result.Commits.Count.ToString(CultureInfo.InvariantCulture)}");
        Append(text, $"- Excluded merge commits: {result.ExcludedMergeCount.ToString(CultureInfo.InvariantCulture)}");
        Append(text, $"- History configuration: {ConfigurationSummary(result)}");
        Append(text, string.Empty);

        AppendHotspots(text, result.HotspotAnalysis);
        AppendClusters(text, result.CoChangeGraph);
        AppendBottlenecks(text, result.BottleneckAnalysis);
        AppendOcpPressure(text, result.OcpAnalysis);
        AppendCandidates(text, result);
        AppendEnrichment(text, result.Enrichment);
        AppendInterpretationLimits(text);
        return text.ToString();
    }

    private static void AppendHotspots(StringBuilder text, HistoryHotspotAnalysis analysis)
    {
        Append(text, "## Hotspots");
        if (analysis.Groups.Count == 0)
        {
            Append(text, "No retained logical files were analyzed.");
        }

        foreach (HotspotCategoryGroup group in analysis.Groups)
        {
            Append(text, $"### {CategoryTitle(group.Category)}");
            foreach (HotspotFinding finding in group.Findings)
            {
                Append(text, $"- `{finding.CanonicalPath}` — score {Decimal(finding.Score)}; commits {finding.RawEvidence.CommitCount.ToString(CultureInfo.InvariantCulture)}; churn {finding.RawEvidence.Churn.ToString(CultureInfo.InvariantCulture)}");
            }
        }

        Append(text, string.Empty);
    }

    private static void AppendClusters(StringBuilder text, CoChangeGraph graph)
    {
        Append(text, "## Co-change clusters");
        if (graph.Clusters.Count == 0)
        {
            Append(text, "No qualifying `Gtheta` clusters.");
        }

        foreach (CoChangeCluster cluster in graph.Clusters)
        {
            string members = string.Join(", ", cluster.Members.Select(static item => $"`{item.CanonicalPath}`"));
            Append(text, $"- {members} — maximum {Decimal(cluster.Maximum)}; aggregate {Decimal(cluster.Aggregate)}");
        }

        Append(text, string.Empty);
    }

    private static void AppendBottlenecks(StringBuilder text, HistoryBottleneckAnalysis analysis)
    {
        Append(text, "## Parallel-development bottlenecks");
        if (analysis.Groups.Count == 0)
        {
            Append(text, "No retained logical files were analyzed.");
        }

        foreach (HistoryBottleneckCategoryGroup group in analysis.Groups)
        {
            Append(text, $"### {CategoryTitle(group.Category)}");
            foreach (HistoryBottleneckFinding finding in group.Findings)
            {
                Append(text, $"- `{finding.CanonicalPath}` — score {Decimal(finding.Score)}; independent tasks {finding.RawEvidence.IndependentTaskSpread.ToString(CultureInfo.InvariantCulture)}; G0 neighbors {finding.RawEvidence.DistinctNeighborDegree.ToString(CultureInfo.InvariantCulture)}");
            }
        }

        Append(text, string.Empty);
    }

    private static void AppendOcpPressure(StringBuilder text, HistoryOcpAnalysis analysis)
    {
        Append(text, "## OCP pressure");
        if (analysis.Groups.Count == 0)
        {
            Append(text, "No retained logical files were analyzed.");
        }

        foreach (HistoryOcpCategoryGroup group in analysis.Groups)
        {
            Append(text, $"### {CategoryTitle(group.Category)}");
            foreach (HistoryOcpFinding finding in group.Findings)
            {
                Append(text, $"- `{finding.CanonicalPath}` — score {Decimal(finding.Score)}; repeated edits {finding.RawEvidence.RepeatedEditTotal.ToString(CultureInfo.InvariantCulture)}; role hint {Decimal(finding.RawEvidence.RoleHint)}");
            }
        }

        Append(text, string.Empty);
    }

    private static void AppendCandidates(StringBuilder text, HistoryIngestionResult result)
    {
        Append(text, "## Refactoring candidates");
        int count = 0;
        foreach (HotspotFinding finding in result.HotspotAnalysis.Findings.Where(static item => item.Score > 0m))
        {
            Append(text, $"- Hotspot investigation: `{finding.CanonicalPath}` (score {Decimal(finding.Score)})");
            count++;
        }

        foreach (CoChangeCluster cluster in result.CoChangeGraph.Clusters)
        {
            Append(text, $"- Co-change cluster investigation: {string.Join(", ", cluster.Members.Select(static item => $"`{item.CanonicalPath}`"))}");
            count++;
        }

        foreach (HistoryBottleneckFinding finding in result.BottleneckAnalysis.Findings.Where(static item => item.Score > 0m))
        {
            Append(text, $"- Bottleneck investigation: `{finding.CanonicalPath}` (score {Decimal(finding.Score)})");
            count++;
        }

        foreach (HistoryOcpFinding finding in result.OcpAnalysis.Findings.Where(static item => item.Score > 0m))
        {
            Append(text, $"- OCP-pressure investigation: `{finding.CanonicalPath}` (score {Decimal(finding.Score)})");
            count++;
        }

        if (count == 0)
        {
            Append(text, "No qualifying candidates.");
        }

        Append(text, string.Empty);
    }

    private static void AppendEnrichment(StringBuilder text, HistoryEnrichmentProjection enrichment)
    {
        Append(text, "## Enrichment");
        Append(text, $"- Status: `{Status(enrichment.Status)}`");
        if (enrichment.Reason is not null)
        {
            Append(text, $"- Reason: {enrichment.Reason}");
        }

        Append(text, string.Empty);
    }

    private static void AppendInterpretationLimits(StringBuilder text)
    {
        Append(text, "## Interpretation limits");
        Append(text, "- Churn is change volume, not complexity.");
        Append(text, "- Co-change is not proof of module ownership.");
        Append(text, "- Accepted exact renames produce zero content churn; ambiguous or lifecycle-broken rename candidates remain ordinary evidence.");
        Append(text, "- Pathname reuse intentionally uses one v1 baseline identity and can conflate unrelated generations.");
        Append(text, "- Binary, NUL, and non-line events contribute zero line churn with an explicit status.");
        Append(text, "- Merge file deltas are excluded and can understate merge-resolution edits.");
        Append(text, "- Exact-blob rename detection misses rename-with-edit.");
        Append(text, "- TaskKey source spellings normalize to canonical namespaced decimal IDs; normalized scores compare only within cohorts.");
        Append(text, "- Role hints and candidates are heuristic investigations that require human review.");
        Append(text, "- Enrichment is optional context, not Git-level correctness authority.");
    }

    private static string ConfigurationSummary(HistoryIngestionResult result)
        => $"{result.Configuration.Extractors.Count.ToString(CultureInfo.InvariantCulture)} extractor(s), {result.Configuration.Ignore.Count.ToString(CultureInfo.InvariantCulture)} ignore pattern(s), threshold {(result.Configuration.Thresholds.CoChangeSignificance is decimal threshold ? Decimal(threshold) : "none")}";

    private static string CategoryTitle(HistoryPathCategory category) => category.ToString().Replace("Ci", "CI", StringComparison.Ordinal);

    private static string Decimal(decimal value) => value.ToString("F9", CultureInfo.InvariantCulture);

    private static string Status(HistoryEnrichmentStatus status) => status switch
    {
        HistoryEnrichmentStatus.NotRequested => "not_requested",
        HistoryEnrichmentStatus.NotApplicable => "not_applicable",
        HistoryEnrichmentStatus.Available => "available",
        HistoryEnrichmentStatus.Unavailable => "unavailable",
        _ => "unavailable",
    };

    private static void Append(StringBuilder text, string line) => text.Append(line).Append('\n');
}
