using System.Text.Json;
using ArchLinterNet.Cli.Abstractions;

namespace ArchLinterNet.Cli.Commands.Baseline.Application;

// Issue #375 follow-up: every baseline subcommand handler previously let OperationCanceledException
// fall into its own generic catch-all exception handler, reporting real Ctrl+C/SIGTERM (or a
// caller-supplied cancelled token) as an unexpected "<command> error" — indistinguishable from a
// genuine failure, and giving a machine caller (--format json) no typed way to tell the two apart.
// Shared here so every handler's dedicated OperationCanceledException branch reports the same shape.
internal static class BaselineCancellationOutput
{
    public static int Write(ICliConsole console, string commandLabel, bool jsonFormat)
    {
        string message = $"Baseline {commandLabel} was cancelled.";
        if (jsonFormat)
        {
            console.Out.WriteLine(JsonSerializer.Serialize(new { status = "cancelled", message }));
        }
        else
        {
            console.Error.WriteLine(message);
        }

        return CliExitCodes.InvalidArgumentsOrRuntimeError;
    }
}
