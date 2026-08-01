using System.Text.Json;
using ArchLinterNet.Core.BuildState;
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
    //
    // cleanupTimeout is non-null only when the cancellation was actually a
    // BuildStateProcessCleanupTimedOutException — a killed child build/restore process that never
    // confirmed exit within its deadline. That evidence (which process, what deadline) must reach
    // the operator; a bare "cancelled" message would silently discard it and look identical to an
    // ordinary, cleanly-terminated cancellation.
    private void WriteCancellation(
        ValidateCommandOptions options, string format, BuildStateProcessCleanupTimedOutException? cleanupTimeout = null)
    {
        string message = cleanupTimeout is null
            ? "Architecture validation was cancelled."
            : "Architecture validation was cancelled. Child build/restore process " +
                $"{cleanupTimeout.ProcessId} did not exit within {cleanupTimeout.TimeoutMs}ms after being killed " +
                "and may still be running.";

        Dictionary<string, string> contentByFormat = new();
        foreach (string neededFormat in NeededErrorFormats(options, format))
        {
            contentByFormat[neededFormat] = neededFormat switch
            {
                FormatJson => BuildCancellationJsonText(message, cleanupTimeout),
                FormatSarif => BuildCancellationSarifText(message),
                _ => message,
            };
        }

        // allowFileSinks: false, unlike WriteExecutionError's analogous "before any report exists
        // this invocation" case: a --report file sink may already hold a legitimate report from an
        // earlier run of this same command, and #375 requires preserving pre-existing report
        // destinations. Writing the cancellation notice there would silently overwrite it. Routing
        // through the safe stream fallback (stderr, or a configured stream sink) instead — the same
        // path WriteOutputError/WriteCancelledRouting already use for a post-outcome failure — never
        // touches a file, so it needs no dependency on the (already-cancelled) handler token either.
        WriteErrorContent(options, format, contentByFormat, allowFileSinks: false);
    }

    private static string BuildCancellationJsonText(string message, BuildStateProcessCleanupTimedOutException? cleanupTimeout)
    {
        return JsonSerializer.Serialize(new
        {
            kind = "architecture_cancelled",
            status = "cancelled",
            message,
            processCleanupTimedOut = cleanupTimeout is not null,
            processId = cleanupTimeout?.ProcessId,
            processCleanupTimeoutMs = cleanupTimeout?.TimeoutMs,
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
