using ArchLinterNet.Cli.Abstractions;
using ArchLinterNet.Core.History;
using ArchLinterNet.Core.History.Reporting;

namespace ArchLinterNet.Cli.Commands.History.Application;

// The command boundary of the fail-closed rule: a diagnostic goes to the error stream and the output
// stream stays empty, so no partial successful report can ever reach a consumer.
internal sealed class HistoryIngestCommandHandler(ICliConsole console)
{
    private const string Usage = "arch-linter-net history analyze --from <rev> --to <rev> [--repository <path>] [--policy <path>] [--format json|markdown]";

    public int Execute(HistoryIngestCommandOptions options)
    {
        if (options.ShowHelp)
        {
            console.Out.WriteLine(Usage);
            return CliExitCodes.Success;
        }

        if (string.IsNullOrWhiteSpace(options.From) || string.IsNullOrWhiteSpace(options.To))
        {
            console.Error.WriteLine($"Both --from and --to are required. Usage: {Usage}");
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }

        if (options.Format is not ("json" or "markdown"))
        {
            console.Error.WriteLine($"Unsupported --format '{options.Format}'. Usage: {Usage}");
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }

        HistoryIngestionOutcome outcome = HistoryPolicyIngestionService.Default.Ingest(
            new HistoryIngestionRequest(options.Repository, options.From, options.To), options.PolicyPath);
        if (outcome.Result is not HistoryIngestionResult result)
        {
            console.Error.Write(HistoryDiagnosticJsonWriter.Write(outcome.Diagnostic!));
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }

        if (options.Format == "markdown")
        {
            console.Out.Write(HistoryIngestionTextWriter.Write(result));
            return CliExitCodes.Success;
        }

        return HistoryReportOutputWriter.TryWriteJson(console, () => HistoryIngestionJsonWriter.Write(result))
            ? CliExitCodes.Success
            : CliExitCodes.InvalidArgumentsOrRuntimeError;
    }
}
