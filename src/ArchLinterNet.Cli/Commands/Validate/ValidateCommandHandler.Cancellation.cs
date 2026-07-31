using System.Text.Json;

namespace ArchLinterNet.Cli.Commands.Validate;

// Split out of ValidateCommandHandler.cs (which grew past the file-size lint threshold) — issue
// #375's cancellation completion-status content, kept alongside the rest of the error-content
// builders it mirrors (WriteExecutionError et al. remain in the main file).
internal sealed partial class ValidateCommandHandler
{
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
