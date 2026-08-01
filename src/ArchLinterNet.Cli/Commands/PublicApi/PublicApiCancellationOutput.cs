using System.Text.Json;
using ArchLinterNet.Cli.Abstractions;

namespace ArchLinterNet.Cli.Commands.PublicApi;

// Issue #375 follow-up: every public-api subcommand handler previously let OperationCanceledException
// fall into its own generic catch-all exception handler, reporting real Ctrl+C/SIGTERM (or a
// caller-supplied cancelled token) as an unexpected "public-api <command> error" —
// indistinguishable from a genuine failure. Shared here so every handler's dedicated
// OperationCanceledException branch reports the same shape, mirroring BaselineCancellationOutput.
internal static class PublicApiCancellationOutput
{
    public static int Write(ICliConsole console, string commandLabel, bool jsonFormat)
    {
        string message = $"public-api {commandLabel} was cancelled.";
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
