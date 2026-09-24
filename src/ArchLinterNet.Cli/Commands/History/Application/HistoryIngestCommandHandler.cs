using System.Text;
using System.Text.Json.Nodes;
using ArchLinterNet.Cli.Abstractions;
using ArchLinterNet.Core.History;
using ArchLinterNet.Core.History.Reporting;

namespace ArchLinterNet.Cli.Commands.History.Application;

// The command boundary of the fail-closed rule: a diagnostic goes to the error stream and the output
// stream stays empty, so no partial successful report can ever reach a consumer.
internal sealed class HistoryIngestCommandHandler(ICliConsole console, IFileSystem fileSystem)
{
    private const string Usage = "arch-linter-net history analyze --from <rev> --to <rev> [--repository <path>] [--policy <path>] [--enrich-dotnet] [--format json|markdown] [--report <format>=<destination>]";

    private static readonly UTF8Encoding _strictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

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

        if (options.ReportParseError is not null)
        {
            console.Error.WriteLine($"{options.ReportParseError} Usage: {Usage}");
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }

        IReadOnlyList<HistoryReportSink> sinks = options.ReportSinks ?? Array.Empty<HistoryReportSink>();
        string? collision = FindReportSinkCollision(sinks, options.PolicyPath);
        if (collision is not null)
        {
            console.Error.WriteLine(collision);
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }

        HistoryIngestionOutcome outcome = HistoryPolicyIngestionService.Ingest(
            new HistoryIngestionRequest(
                options.Repository,
                options.From,
                options.To,
                options.RequestDotNetEnrichment),
            options.PolicyPath);
        if (outcome.Result is not HistoryIngestionResult result)
        {
            console.Error.Write(HistoryDiagnosticJsonWriter.Write(outcome.Diagnostic!));
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }

        if (sinks.Count == 0)
        {
            return ExecuteLegacySingleFormat(options.Format, result);
        }

        return ExecuteReportSinks(sinks, result);
    }

    private int ExecuteLegacySingleFormat(string format, HistoryIngestionResult result)
    {
        if (format == "markdown")
        {
            console.Out.Write(HistoryIngestionTextWriter.Write(result));
            return CliExitCodes.Success;
        }

        return HistoryReportOutputWriter.TryWriteJson(console, () => HistoryIngestionJsonWriter.Write(result))
            ? CliExitCodes.Success
            : CliExitCodes.InvalidArgumentsOrRuntimeError;
    }

    private int ExecuteReportSinks(IReadOnlyList<HistoryReportSink> sinks, HistoryIngestionResult result)
    {
        string? jsonContent = null;
        string? markdownContent = null;

        if (sinks.Any(sink => sink.Format == "json"))
        {
            jsonContent = HistoryIngestionJsonWriter.Write(result);
            try
            {
                _ = _strictUtf8.GetByteCount(jsonContent);
            }
            catch (EncoderFallbackException)
            {
                console.Error.Write(HistoryDiagnosticJsonWriter.Write(new HistoryDiagnostic(
                    HistoryDiagnosticKind.ReportSerializationInvalid,
                    "The release architecture forensics report contains invalid Unicode scalar content.")));
                return CliExitCodes.InvalidArgumentsOrRuntimeError;
            }
        }

        if (sinks.Any(sink => sink.Format == "markdown"))
        {
            markdownContent = HistoryIngestionTextWriter.Write(result);
        }

        List<(string TempPath, string TargetPath)> pendingRenames = new();
        List<string> failedDestinations = new();
        List<string> errorDetails = new();

        foreach (HistoryReportSink sink in sinks.Where(sink => sink.DestinationType == HistoryReportDestinationType.File))
        {
            string content = sink.Format == "json" ? jsonContent! : markdownContent!;
            try
            {
                string tempPath = fileSystem.WriteAllTextToTemp(sink.FilePath!, content);
                ValidateWrittenTempFile(tempPath, sink.Format);
                pendingRenames.Add((tempPath, sink.FilePath!));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
            {
                failedDestinations.Add(sink.FilePath!);
                errorDetails.Add(ex.Message);
            }
        }

        if (failedDestinations.Count > 0)
        {
            DeletePendingTemps(pendingRenames);
            WriteSinkFailureDiagnostic(failedDestinations, errorDetails);
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }

        foreach (HistoryReportSink sink in sinks
            .Where(sink => sink.DestinationType != HistoryReportDestinationType.File)
            .OrderBy(sink => sink.DestinationType == HistoryReportDestinationType.Stdout ? 0 : 1))
        {
            string content = sink.Format == "json" ? jsonContent! : markdownContent!;
            if (sink.DestinationType == HistoryReportDestinationType.Stdout)
            {
                if (sink.Format == "json")
                {
                    console.WriteCanonicalJson(content);
                }
                else
                {
                    console.Out.Write(content);
                }
            }
            else
            {
                console.Error.Write(content);
            }
        }

        List<string> failedCommits = new();
        List<string> commitErrorDetails = new();
        foreach ((string tempPath, string targetPath) in pendingRenames)
        {
            try
            {
                fileSystem.RenameTempToTarget(tempPath, targetPath);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
            {
                failedCommits.Add(targetPath);
                commitErrorDetails.Add(ex.Message);
            }
        }

        if (failedCommits.Count > 0)
        {
            WriteSinkFailureDiagnostic(failedCommits, commitErrorDetails);
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }

        return CliExitCodes.Success;
    }

    private void WriteSinkFailureDiagnostic(IReadOnlyList<string> failedDestinations, IReadOnlyList<string> errorDetails)
    {
        console.Error.WriteLine(
            $"Could not write the release architecture forensics report to {string.Join(", ", failedDestinations)}: {string.Join("; ", errorDetails)}");
    }

    private void ValidateWrittenTempFile(string tempPath, string format)
    {
        if (!fileSystem.FileExists(tempPath))
        {
            throw new InvalidOperationException($"Temp report file was not created: {tempPath}");
        }

        string writtenContent = fileSystem.ReadAllText(tempPath);
        if (format == "json")
        {
            JsonNode.Parse(writtenContent);
        }
    }

    private void DeletePendingTemps(IReadOnlyList<(string TempPath, string TargetPath)> pendingRenames)
    {
        foreach ((string tempPath, string _) in pendingRenames)
        {
            try
            {
                fileSystem.DeleteFile(tempPath);
            }
            catch
            {
                // Best-effort cleanup only — a stray temp file doesn't change the failure already being reported.
            }
        }
    }

    private static string? FindReportSinkCollision(IReadOnlyList<HistoryReportSink> sinks, string? policyPath)
    {
        if (policyPath is null)
        {
            return null;
        }

        string fullPolicyPath = Path.GetFullPath(policyPath);
        foreach (HistoryReportSink sink in sinks.Where(sink => sink.DestinationType == HistoryReportDestinationType.File))
        {
            if (string.Equals(Path.GetFullPath(sink.FilePath!), fullPolicyPath, FileSystemPathComparison))
            {
                return $"--report destination '{sink.FilePath}' matches --policy input '{policyPath}'";
            }
        }

        return null;
    }

    private static StringComparison FileSystemPathComparison => OperatingSystem.IsWindows()
        ? StringComparison.OrdinalIgnoreCase
        : StringComparison.Ordinal;
}
