using ArchLinterNet.Cli.Abstractions;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Validation;

namespace ArchLinterNet.Cli.Commands.Validate.Application;

// Purpose-named rendering facade used by the transport coordinator. The specialized renderers
// compose documents only from supplied outcomes; this type performs no sink operations.
internal sealed class ReportDocumentRenderer
{
    private const string FormatHuman = "human";
    private const string FormatJson = "json";
    private const string FormatSarif = "sarif";

    private readonly HumanReportRenderer _human;
    private readonly StructuredReportRenderer _structured;

    public ReportDocumentRenderer(ICliRuntime runtime)
    {
        _human = new HumanReportRenderer(runtime);
        _structured = new StructuredReportRenderer(runtime);
    }

    internal string RenderHumanContent(
        bool isSingleMode,
        IReadOnlyList<(string Mode, ValidationOutcome Outcome)> outcomesByMode,
        CancellationToken cancellationToken) =>
        _human.Render(isSingleMode, outcomesByMode, cancellationToken);

    internal string RenderStructuredContent(
        string format,
        bool isSingleMode,
        IReadOnlyList<(string Mode, ValidationOutcome Outcome)> outcomesByMode,
        CancellationToken cancellationToken) =>
        _structured.Render(format, isSingleMode, outcomesByMode, cancellationToken);

    // Re-renders a complete document from an already-computed outcome for output-error envelopes;
    // it never repeats validation or contract execution.
    internal string RenderReportContent(
        string format,
        bool isSingleMode,
        IReadOnlyList<(string Mode, ValidationOutcome Outcome)> outcomesByMode)
    {
        return format switch
        {
            FormatJson or FormatSarif => _structured.RenderReportContent(format, isSingleMode, outcomesByMode),
            _ => _human.Render(isSingleMode, outcomesByMode),
        };
    }

    internal static string StripAnsi(string content) => HumanReportRenderer.StripAnsi(content);
}
