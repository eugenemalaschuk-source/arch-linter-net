using ArchLinterNet.Core.Reporting;
using ArchLinterNet.Core.Validation;

namespace ArchLinterNet.Cli.Commands.Validate;

// Rendering helpers are kept separate from report routing so output accounting stays readable.
internal sealed partial class ReportCoordinator
{
    private string FormatHumanContent(
        bool isSingleMode,
        IReadOnlyList<(string Mode, ValidationOutcome Outcome)> outcomesByMode,
        CancellationToken cancellationToken)
    {
        return isSingleMode
            ? FormatSingleHuman(outcomesByMode[0].Outcome, cancellationToken)
            : FormatCombinedHuman(outcomesByMode, cancellationToken);
    }

    private static string? RenderContent(
        string? needed,
        string format,
        Func<string> render,
        SinkDistributionEvidence evidence,
        ValidationTiming? timing)
    {
        if (needed is null)
        {
            return null;
        }

        string content;
        using (timing?.Measure($"render_{format}"))
            content = render();
        evidence.RecordRenderedFormat(format);
        return content;
    }

    private static string FormatStructuredContent(
        bool isSingleMode,
        IReadOnlyList<(string Mode, ValidationOutcome Outcome)> outcomesByMode,
        Func<string, ValidationOutcome, CancellationToken, string> formatSingle,
        Func<IReadOnlyList<(string Mode, ValidationOutcome Outcome)>, CancellationToken, string> formatCombined,
        CancellationToken cancellationToken)
    {
        return isSingleMode
            ? formatSingle(outcomesByMode[0].Mode, outcomesByMode[0].Outcome, cancellationToken)
            : formatCombined(outcomesByMode, cancellationToken);
    }

    private static Dictionary<string, string> BuildContentByFormat(string? humanContent, string? jsonContent, string? sarifContent)
    {
        Dictionary<string, string> contentByFormat = new();
        if (humanContent is not null)
        {
            contentByFormat[FormatHuman] = humanContent;
        }
        if (jsonContent is not null)
        {
            contentByFormat[FormatJson] = jsonContent;
        }
        if (sarifContent is not null)
        {
            contentByFormat[FormatSarif] = sarifContent;
        }
        return contentByFormat;
    }

    // Re-renders a complete document from an already-computed outcome for an output-error
    // envelope; it never repeats validation or contract execution.
    public string RenderReportContent(
        string format, bool isSingleMode, IReadOnlyList<(string Mode, ValidationOutcome Outcome)> outcomesByMode)
    {
        return format switch
        {
            FormatJson => isSingleMode
                ? FormatSingleJson(outcomesByMode[0].Mode, outcomesByMode[0].Outcome)
                : FormatCombinedJson(outcomesByMode),
            FormatSarif => isSingleMode
                ? FormatSingleSarif(outcomesByMode[0].Mode, outcomesByMode[0].Outcome)
                : FormatCombinedSarif(outcomesByMode),
            _ => isSingleMode
                ? FormatSingleHuman(outcomesByMode[0].Outcome)
                : FormatCombinedHuman(outcomesByMode),
        };
    }
}
