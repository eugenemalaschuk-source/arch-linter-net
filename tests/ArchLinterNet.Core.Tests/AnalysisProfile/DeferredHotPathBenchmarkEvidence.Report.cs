using System.Globalization;
using System.Text;

namespace ArchLinterNet.Core.Tests;

internal static class DeferredHotPathBenchmarkMarkdown
{
    public static string Render(DeferredHotPathEvidenceDocument document)
    {
        var builder = new StringBuilder()
            .AppendLine("# Deferred hot-path measurement evidence (#655)")
            .AppendLine()
            .AppendLine("## Decision")
            .AppendLine()
            .AppendLine("This is an evidence-first measurement. No selector, graph, cache, prepared-analysis, or public-API optimization is implemented by this issue.")
            .AppendLine()
            .AppendLine($"The checked-in machine-readable artifact is deferred-hot-path-analysis-results.json. It retains the raw analysis-profile/v1 payload for every measured CLI run. The source identity is {document.SourceIdentity}; values are observations of {document.Runtime} on {document.OperatingSystem} ({document.Architecture}), configuration {document.Configuration}.")
            .AppendLine()
            .AppendLine("## Methodology")
            .AppendLine()
            .AppendLine("- Reuse the #502 large-solution-benchmark/v1 synthetic workload generator and materializer.")
            .AppendLine("- Vary independent dimensions at small, medium, and large sizes where the hypothesis is measurable.")
            .AppendLine("- Use deterministic workload counters before interpreting wall-clock phase values.")
            .AppendLine("- Preserve canonical-result SHA-256 identity and completion/exit status for each profile.")
            .AppendLine("- Use staged assemblies to keep fixture compilation outside analyzer preparation; use real MSBuild only for the cache-eligibility lane.")
            .AppendLine("- The explicit harness is DeferredHotPathBenchmarkHarness; it is excluded from normal test and acceptance gates.")
            .AppendLine()
            .AppendLine("## Environment")
            .AppendLine()
            .AppendLine($"- Runtime: {document.Runtime}")
            .AppendLine($"- Operating system: {document.OperatingSystem}")
            .AppendLine($"- Architecture: {document.Architecture}")
            .AppendLine($"- Configuration: {document.Configuration}")
            .AppendLine($"- Tool boundary: {document.ToolIdentity}")
            .AppendLine()
            .AppendLine("## Outcomes");
        builder.AppendLine()
            .AppendLine("| Finding | Outcome | Scale variable | Measurements | Routing |")
            .AppendLine("|---|---|---|---:|---|");
        foreach (DeferredHotPathFindingEvidence finding in document.Findings)
        {
            builder.Append("| ")
                .Append(finding.Id)
                .Append(" | **")
                .Append(finding.Outcome)
                .Append("** | ")
                .Append(finding.ScaleVariable)
                .Append(" | ")
                .Append(finding.Measurements.Count.ToString(CultureInfo.InvariantCulture))
                .Append(" | ")
                .Append(finding.Routing)
                .AppendLine(" |");
        }

        builder.AppendLine()
            .AppendLine("### 1. Type/layer membership amplification")
            .AppendLine()
            .AppendLine(Describe(document.Findings[0]))
            .AppendLine()
            .AppendLine("### 2. Repeated selector/classification work")
            .AppendLine()
            .AppendLine(Describe(document.Findings[1]))
            .AppendLine()
            .AppendLine("### 3. Graph/reachability/witness work")
            .AppendLine()
            .AppendLine(Describe(document.Findings[2]))
            .AppendLine()
            .AppendLine("### 4. Cross-process preparation/fact repetition")
            .AppendLine()
            .AppendLine(Describe(document.Findings[3]))
            .AppendLine()
            .AppendLine("### 5. Exact-request cache eligibility")
            .AppendLine()
            .AppendLine(Describe(document.Findings[4]))
            .AppendLine()
            .AppendLine("### 6. Public-API cross-process reuse")
            .AppendLine()
            .AppendLine(Describe(document.Findings[5]))
            .AppendLine()
            .AppendLine("## Measurement rows")
            .AppendLine()
            .AppendLine("| Finding | Workload | Size | Work | Observed counter | Dominant phase | Canonical result |")
            .AppendLine("|---|---|---|---:|---|---|---|");
        foreach (DeferredHotPathFindingEvidence finding in document.Findings)
        {
            foreach (DeferredHotPathMeasurement measurement in finding.Measurements)
            {
                builder.Append("| ")
                    .Append(finding.Id)
                    .Append(" | ")
                    .Append(measurement.WorkloadId)
                    .Append(" | ")
                    .Append(measurement.Size)
                    .Append(" | ")
                    .Append(measurement.DeterministicWork.ToString(CultureInfo.InvariantCulture))
                    .Append(" | ")
                    .Append(measurement.ObservedCounter)
                    .Append('=')
                    .Append(measurement.ObservedCounterValue?.ToString(CultureInfo.InvariantCulture) ?? "unavailable")
                    .Append(" | ")
                    .Append(measurement.DominantPhase ?? "unavailable")
                    .Append(" ")
                    .Append(measurement.DominantPhaseMilliseconds?.ToString("F1", CultureInfo.InvariantCulture) ?? "n/a")
                    .Append(" ms | ")
                    .Append(measurement.CanonicalResultSha256)
                    .AppendLine(" |");
            }
        }

        return builder
            .AppendLine()
            .AppendLine("## Routing and non-goals")
            .AppendLine()
            .AppendLine("- Cross-process preparation evidence belongs to #492/#493.")
            .AppendLine("- Real-MSBuild cache eligibility belongs to #675.")
            .AppendLine("- Public-API prepared reuse belongs to #498 and remains conditional on the #493 materiality gate.")
            .AppendLine("- No private adopter identity, repository URL, namespace, proprietary topology, or raw private CI log is committed.")
            .AppendLine("- No universal timing SLA is claimed.")
            .AppendLine()
            .AppendLine("OpenSpec: not applicable. This change adds an explicitly invoked internal measurement harness and evidence only; it changes no product behavior, public API, policy semantics, cache trust boundary, or documented user guarantee.")
            .ToString();
    }

    private static string Describe(DeferredHotPathFindingEvidence finding) =>
        $"{finding.Hypothesis} **Outcome {finding.Outcome}.** Current model: {finding.CurrentWorkModel} {finding.ObservedGrowth} {finding.Interpretation} Routing: {finding.Routing}";
}
