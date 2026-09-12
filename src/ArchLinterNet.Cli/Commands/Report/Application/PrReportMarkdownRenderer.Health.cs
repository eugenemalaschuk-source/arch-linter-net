using System.Text;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;

namespace ArchLinterNet.Cli.Commands.Report.Application;

using static PrReportMarkdownFormatter;

internal static class PrReportMarkdownHealth
{
    private const int MaxHealthExplanationBytes = 16 * 1024;
    private const string HealthBudgetMarker = "- Additional health explanations omitted due to publisher byte budget; see immutable bundle.";

    internal static bool AppendHealthExplanation(
        StringBuilder builder,
        ArchitecturePrReportProjection projection,
        int maxDetails)
    {
        List<HealthExplanationView> explanations = BuildHealthExplanations(projection)
            // Keep blocking canonical dimensions ahead of advisory dimensions when the
            // publisher byte budget requires omitting the tail of this section.
            .OrderByDescending(item => item.IsBlocking)
            .ThenBy(item => item.Dimension, StringComparer.Ordinal)
            .ThenBy(item => item.State)
            .ToList();
        if (explanations.Count == 0)
        {
            return false;
        }

        StringBuilder section = new();
        section.AppendLine("## Health explanation");
        int sectionBytes = Encoding.UTF8.GetByteCount(section.ToString());
        int budgetMarkerBytes = Encoding.UTF8.GetByteCount(HealthBudgetMarker + Environment.NewLine);
        List<string> minimumLines = explanations
            .Select(FormatMinimumExplanationLine)
            .ToList();
        int minimumTailBytes = minimumLines.Sum(line => Encoding.UTF8.GetByteCount(line + Environment.NewLine));
        bool omitted = false;
        for (int index = 0; index < explanations.Count; index++)
        {
            HealthExplanationView explanation = explanations[index];
            string classification = explanation.IsBlocking ? "blocking" : "advisory";
            List<string> formattedReasons = explanation.Reasons
                .OrderBy(item => item.Code, StringComparer.Ordinal)
                .ThenBy(item => item.Source, StringComparer.Ordinal)
                .Select(FormatHealthReason)
                .ToList();
            int shown = Math.Min(maxDetails, formattedReasons.Count);
            string line = FormatExplanationLine(explanation, classification, formattedReasons, shown);
            int lineBytes = Encoding.UTF8.GetByteCount(line + Environment.NewLine);
            int minimumLineBytes = Encoding.UTF8.GetByteCount(minimumLines[index] + Environment.NewLine);
            int tailMinimumBytes = minimumTailBytes - minimumLineBytes;
            if (sectionBytes + lineBytes + tailMinimumBytes + budgetMarkerBytes <= MaxHealthExplanationBytes)
            {
                section.AppendLine(line);
                sectionBytes += lineBytes;
            }
            else
            {
                // Every non-healthy dimension retains at least its canonical reason code. Only
                // the optional source/identity detail is dropped when the shared budget is tight.
                section.AppendLine(minimumLines[index]);
                sectionBytes += minimumLineBytes;
                omitted = true;
            }

            minimumTailBytes = tailMinimumBytes;
        }

        if (omitted)
        {
            section.AppendLine(HealthBudgetMarker);
        }

        builder.Append(section);
        return true;
    }

    private static string FormatExplanationLine(
        HealthExplanationView explanation,
        string classification,
        IReadOnlyList<string> formattedReasons,
        int shown)
    {
        string reasons = formattedReasons.Count == 0
            ? "no canonical reason supplied"
            : string.Join(", ", formattedReasons.Take(shown));
        if (formattedReasons.Count > shown)
        {
            reasons += $"; {formattedReasons.Count - shown} more reason(s) omitted (see immutable bundle)";
        }

        return $"- `{Inline(Bounded(explanation.Dimension))}` state=`{DimensionToken(explanation.State)}` " +
            $"classification=`{classification}`: {reasons}";
    }

    private static string FormatMinimumExplanationLine(HealthExplanationView explanation)
    {
        ArchitectureHealthReason? reason = explanation.Reasons
            .OrderBy(item => item.Code, StringComparer.Ordinal)
            .ThenBy(item => item.Source, StringComparer.Ordinal)
            .FirstOrDefault();
        string canonicalCode = reason is null
            ? "no canonical reason supplied"
            : $"`{Inline(Bounded(reason.Code))}`";
        string classification = explanation.IsBlocking ? "blocking" : "advisory";
        return $"- `{Inline(Bounded(explanation.Dimension))}` state=`{DimensionToken(explanation.State)}` " +
            $"classification=`{classification}`: {canonicalCode}";
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
        $"`{Inline(Bounded(reason.Code))}`{FormatReasonIdentityBounded(reason)}" +
        (string.IsNullOrWhiteSpace(reason.Source) ? string.Empty : $" source=`{Inline(Bounded(reason.Source))}`");

    private static string FormatReasonIdentityBounded(ArchitectureHealthReason reason)
    {
        string identity = reason.EvidenceIdentity ?? reason.ControlIdentity ?? reason.PolicyIdentity ?? string.Empty;
        return string.IsNullOrWhiteSpace(identity) ? string.Empty : $" (`{Inline(Bounded(identity))}`)";
    }

    private static bool IsBlockingState(ArchitectureHealthDimensionState state) =>
        state is ArchitectureHealthDimensionState.Fail or ArchitectureHealthDimensionState.Unassessable;

    internal sealed record HealthExplanationView(
        string Dimension,
        ArchitectureHealthDimensionState State,
        bool IsBlocking,
        IReadOnlyList<ArchitectureHealthReason> Reasons);
}
