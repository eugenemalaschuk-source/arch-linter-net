using System.Text.Json;
using ArchLinterNet.Core.Validation;

namespace ArchLinterNet.Cli.Commands.Validate;

// Split out of ValidateCommandHandler.cs (which grew past the file-size lint threshold) — issue
// #375's cancellation completion-status content, kept alongside the rest of the error-content
// builders it mirrors (WriteExecutionError et al. remain in the main file).
internal sealed partial class ValidateCommandHandler
{
    // Cancellation observed during multi-sink staging/commit (RouteResult.Cancelled) — as opposed
    // to an OperationCanceledException thrown from Core before any outcome existed (see
    // WriteCancellation below) — still exits via the existing numeric category 2 but must be
    // reported as "cancelled", not the generic partial-output/output-failed shape WriteOutputError
    // uses, while preserving the same typed committed/uncommitted evidence that shape already
    // carries. allowFileSinks is false for the same reason WriteOutputError uses false: a File
    // sink either just failed (writing here would only fail again) or already committed valid
    // content this must not overwrite.
    private void WriteCancelledRouting(
        ValidateCommandOptions options,
        string format,
        RouteResult result,
        bool isSingleMode,
        IReadOnlyList<(string Mode, ValidationOutcome Outcome)> outcomesByMode)
    {
        const string Message = "Architecture validation was cancelled during report output.";

        Dictionary<string, string> contentByFormat = new();
        foreach (string neededFormat in NeededErrorFormats(options, format))
        {
            string reportContent = _coordinator.RenderReportContent(neededFormat, isSingleMode, outcomesByMode);
            contentByFormat[neededFormat] = neededFormat switch
            {
                FormatJson => ReportErrorContentFormatter.BuildCancelledOutputJsonText(Message, result, reportContent),
                FormatSarif => ReportErrorContentFormatter.BuildCancelledOutputSarifText(Message, result, reportContent),
                _ => ReportErrorContentFormatter.BuildCancelledOutputHumanText(Message, result, reportContent),
            };
        }

        WriteErrorContent(options, format, contentByFormat, allowFileSinks: false, priorOutputResult: result);
    }

    // Cancellation still exits via the existing numeric category 2
    // (CliExitCodes.InvalidArgumentsOrRuntimeError) — see design.md Decision 5 — but carries a
    // distinct "cancelled" status/kind literal in every format, so a caller telling completion
    // statuses apart never confuses cancellation with a generic execution error.
    private void WriteCancellation(ValidateCommandOptions options, string format)
    {
        const string Message = "Architecture validation was cancelled.";

        Dictionary<string, string> contentByFormat = new();
        foreach (string neededFormat in NeededErrorFormats(options, format))
        {
            contentByFormat[neededFormat] = neededFormat switch
            {
                FormatJson => BuildCancellationJsonText(Message),
                FormatSarif => BuildCancellationSarifText(Message),
                _ => Message,
            };
        }

        // A cancelled run never reaches a legitimate report for this invocation (see
        // WriteExecutionError's own rationale for the analogous "before any report exists" case),
        // so routing the cancellation notice to every configured sink, including files, is safe.
        WriteErrorContent(options, format, contentByFormat, allowFileSinks: true);
    }

    private static string BuildCancellationJsonText(string message)
    {
        return JsonSerializer.Serialize(new
        {
            kind = "architecture_cancelled",
            status = "cancelled",
            message,
        });
    }

    private static string BuildCancellationSarifText(string message)
    {
        return JsonSerializer.Serialize(new
        {
            version = "2.1.0",
            runs = new[]
            {
                new
                {
                    tool = new { driver = new { name = "arch-linter-net" } },
                    results = new[]
                    {
                        new
                        {
                            ruleId = "architecture-cancelled",
                            message = new { text = message },
                            locations = Array.Empty<object>(),
                        },
                    },
                },
            },
        });
    }
}
