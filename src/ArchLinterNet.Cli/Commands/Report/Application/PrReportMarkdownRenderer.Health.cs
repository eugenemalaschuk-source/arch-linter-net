using System.Text;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;

namespace ArchLinterNet.Cli.Commands.Report.Application;

using static PrReportMarkdownFormatter;

internal static class PrReportMarkdownHealth
{
    internal static bool AppendHealthExplanation(
        StringBuilder builder,
        ArchitecturePrReportProjection projection)
    {
        List<HealthExplanationView> explanations = BuildHealthExplanations(projection)
            .OrderBy(item => item.Dimension, StringComparer.Ordinal)
            .ThenBy(item => item.State)
            .ToList();
        if (explanations.Count == 0)
        {
            return false;
        }

        builder.AppendLine("## Health explanation");
        foreach (HealthExplanationView explanation in explanations)
        {
            string classification = explanation.IsBlocking ? "blocking" : "advisory";
            string reasons = explanation.Reasons.Count == 0
                ? "no canonical reason supplied"
                : string.Join(", ", explanation.Reasons
                    .OrderBy(item => item.Code, StringComparer.Ordinal)
                    .ThenBy(item => item.Source, StringComparer.Ordinal)
                    .Select(FormatHealthReason));
            builder.AppendLine(
                $"- `{Inline(explanation.Dimension)}` state=`{DimensionToken(explanation.State)}` " +
                $"classification=`{classification}`: {reasons}");
        }

        return true;
    }

    internal static IReadOnlyList<HealthExplanationView> BuildHealthExplanations(
        ArchitecturePrReportProjection projection)
    {
        // Core is authoritative when it supplies classified explanations. The dimension fallback
        // keeps legacy projections readable and preserves the existing fail/unassessable blocker
        // selection while old artifacts are still in circulation.
        Dictionary<string, List<ArchitecturePrReportDimensionExplanation>> classified = projection.Headline.DimensionExplanations
            .GroupBy(item => item.Dimension, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.Ordinal);
        List<HealthExplanationView> result = new();
        foreach (ArchitectureHealthDimension dimension in projection.Headline.Dimensions
            .Where(item => item.State is not (ArchitectureHealthDimensionState.Pass
                or ArchitectureHealthDimensionState.NotConfigured
                or ArchitectureHealthDimensionState.NotApplicable))
            .OrderBy(item => item.Name, StringComparer.Ordinal))
        {
            if (classified.TryGetValue(dimension.Name, out List<ArchitecturePrReportDimensionExplanation>? values)
                && values.Count > 0)
            {
                result.Add(new HealthExplanationView(
                    dimension.Name,
                    dimension.State,
                    values.Any(item => item.IsBlocking && IsBlockingState(item.State)),
                    values.Select(item => item.Reason).ToArray()));
            }
            else
            {
                result.Add(new HealthExplanationView(
                    dimension.Name,
                    dimension.State,
                    dimension.State is ArchitectureHealthDimensionState.Fail or ArchitectureHealthDimensionState.Unassessable,
                    dimension.Reasons));
            }
        }

        // A classified explanation may carry a canonical dimension that is not present in the
        // legacy headline array. Keep it visible rather than dropping Core-owned evidence.
        foreach ((string dimension, List<ArchitecturePrReportDimensionExplanation> values) in classified
            .Where(item => result.All(existing => !string.Equals(existing.Dimension, item.Key, StringComparison.Ordinal)))
            .OrderBy(item => item.Key, StringComparer.Ordinal))
        {
            ArchitecturePrReportDimensionExplanation first = values[0];
            result.Add(new HealthExplanationView(
                dimension,
                first.State,
                values.Any(item => item.IsBlocking && IsBlockingState(item.State)),
                values.Select(item => item.Reason).ToArray()));
        }

        return result;
    }

    private static string FormatHealthReason(ArchitectureHealthReason reason) =>
        $"`{Inline(reason.Code)}`{FormatReasonIdentity(reason)}" +
        (string.IsNullOrWhiteSpace(reason.Source) ? string.Empty : $" source=`{Inline(reason.Source)}`");

    private static bool IsBlockingState(ArchitectureHealthDimensionState state) =>
        state is ArchitectureHealthDimensionState.Fail or ArchitectureHealthDimensionState.Unassessable;

    internal sealed record HealthExplanationView(
        string Dimension,
        ArchitectureHealthDimensionState State,
        bool IsBlocking,
        IReadOnlyList<ArchitectureHealthReason> Reasons);
}
