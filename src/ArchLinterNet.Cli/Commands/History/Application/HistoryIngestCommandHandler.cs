using System.Diagnostics;
using System.Text.Json.Nodes;
using ArchLinterNet.Cli.Abstractions;
using ArchLinterNet.Core.History;
using ArchLinterNet.Core.History.Reporting;

namespace ArchLinterNet.Cli.Commands.History.Application;

// The command boundary records publication evidence explicitly. Stream writes and independent file
// renames cannot be rolled back as a set, so failures are surfaced as output-failed/partial-output
// diagnostics instead of claiming an all-or-none result that the OS cannot provide.
internal sealed class HistoryIngestCommandHandler(
    ICliConsole console,
    IFileSystem fileSystem,
    CancellationToken cancellationToken = default)
{
    private const string Usage = "arch-linter-net history analyze --from <rev> --to <rev> [--repository <path>] [--policy <path>] [--enrich-dotnet] [--format json|markdown] [--report <format>=<destination>] [--timings]";

    private readonly CancellationToken _cancellationToken = cancellationToken;

    public int Execute(HistoryIngestCommandOptions options)
    {
        HistoryIngestionTiming? timing = options.TimingsEnabled ? new HistoryIngestionTiming() : null;
        try
        {
            return ExecuteCore(options, timing);
        }
        catch (OperationCanceledException) when (_cancellationToken.IsCancellationRequested)
        {
            // History has no successful result envelope to enrich with a cancellation status. Keep
            // the failure on stderr and, importantly, never turn a cancellation into a report.
            console.Error.Write(HistoryDiagnosticJsonWriter.Write(new HistoryDiagnostic(
                HistoryDiagnosticKind.AnalysisCancelled,
                "History analysis was cancelled.")));
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }
        finally
        {
            if (timing is not null)
            {
                console.Error.WriteLine(timing.Format());
            }
        }
    }

    private int ExecuteCore(HistoryIngestCommandOptions options, HistoryIngestionTiming? timing)
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

        if (options.ReportParseError is not null)
        {
            console.Error.WriteLine($"{options.ReportParseError} Usage: {Usage}");
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }

        IReadOnlyList<HistoryReportSink> sinks = options.ReportSinks ?? Array.Empty<HistoryReportSink>();

        // --format only selects the legacy single-destination stdout path; it is ignored once
        // --report sinks take over publication, so an unrelated/default --format value must not
        // block a --report run.
        if (sinks.Count == 0 && options.Format is not ("json" or "markdown"))
        {
            console.Error.WriteLine($"Unsupported --format '{options.Format}'. Usage: {Usage}");
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }

        string? collision = FindReportSinkCollision(sinks, options.PolicyPath);
        if (collision is not null)
        {
            console.Error.WriteLine(collision);
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }

        _cancellationToken.ThrowIfCancellationRequested();
        HistoryIngestionOutcome outcome = HistoryPolicyIngestionService.Ingest(
            new HistoryIngestionRequest(
                options.Repository,
                options.From,
                options.To,
                options.RequestDotNetEnrichment),
            options.PolicyPath,
            _cancellationToken,
            timing);
        if (outcome.Result is not HistoryIngestionResult result)
        {
            console.Error.Write(HistoryDiagnosticJsonWriter.Write(outcome.Diagnostic!));
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }

        if (sinks.Count == 0)
        {
            return ExecuteLegacySingleFormat(options.Format, result, timing);
        }

        return ExecuteReportSinks(sinks, result, timing);
    }

    private int ExecuteLegacySingleFormat(string format, HistoryIngestionResult result, HistoryIngestionTiming? timing)
    {
        _cancellationToken.ThrowIfCancellationRequested();
        if (format == "markdown")
        {
            Stopwatch? markdownClock = timing?.Start();
            string content = HistoryIngestionTextWriter.Write(result);
            timing?.Record("markdown_render", markdownClock);
            _cancellationToken.ThrowIfCancellationRequested();
            console.Out.Write(content);
            return CliExitCodes.Success;
        }

        bool written = HistoryReportOutputWriter.TryWriteJson(console, () =>
        {
            Stopwatch? jsonClock = timing?.Start();
            _cancellationToken.ThrowIfCancellationRequested();
            string content = HistoryIngestionJsonWriter.Write(result);
            timing?.Record("json_render", jsonClock);
            return content;
        });
        _cancellationToken.ThrowIfCancellationRequested();
        return written
            ? CliExitCodes.Success
            : CliExitCodes.InvalidArgumentsOrRuntimeError;
    }

    private int ExecuteReportSinks(
        IReadOnlyList<HistoryReportSink> sinks,
        HistoryIngestionResult result,
        HistoryIngestionTiming? timing)
    {
        string? jsonContent = null;
        string? markdownContent = null;

        if (sinks.Any(sink => sink.Format == "json"))
        {
            Stopwatch? jsonClock = timing?.Start();
            if (!HistoryReportOutputWriter.TryRenderJson(
                    console,
                    () =>
                    {
                        _cancellationToken.ThrowIfCancellationRequested();
                        return HistoryIngestionJsonWriter.Write(result);
                    },
                    out jsonContent))
            {
                timing?.Record("json_render", jsonClock);
                return CliExitCodes.InvalidArgumentsOrRuntimeError;
            }

            timing?.Record("json_render", jsonClock);
        }

        if (sinks.Any(sink => sink.Format == "markdown"))
        {
            Stopwatch? markdownClock = timing?.Start();
            markdownContent = HistoryIngestionTextWriter.Write(result);
            timing?.Record("markdown_render", markdownClock);
        }

        Stopwatch? outputClock = timing?.Start();
        HistoryReportRouteResult route = RouteReportSinks(sinks, jsonContent, markdownContent);
        timing?.Record("output", outputClock);
        if (route.Status != HistoryReportRouteStatus.AllSucceeded || route.Cancelled)
        {
            WriteRouteFailureDiagnostic(route);
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }

        return CliExitCodes.Success;
    }

    private HistoryReportRouteResult RouteReportSinks(
        IReadOnlyList<HistoryReportSink> sinks,
        string? jsonContent,
        string? markdownContent)
    {
        List<(string TempPath, string TargetPath)> pendingRenames = new();
        List<string> failedPaths = new();
        List<string> committedPaths = new();
        List<string> stagedPaths = new();
        List<string> errorDetails = new();
        List<string> deliveredStreamPaths = new();

        foreach (HistoryReportSink sink in sinks.Where(sink => sink.DestinationType == HistoryReportDestinationType.File))
        {
            if (_cancellationToken.IsCancellationRequested)
            {
                DeletePendingTemps(pendingRenames);
                return BuildRouteResult(
                    sinks, failedPaths, committedPaths, stagedPaths, errorDetails, deliveredStreamPaths,
                    cancelled: true);
            }

            string content = sink.Format == "json" ? jsonContent! : markdownContent!;
            string? tempPath = null;
            try
            {
                tempPath = fileSystem.WriteAllTextToTemp(sink.FilePath!, content);
                ValidateWrittenTempFile(tempPath, sink.Format);
                pendingRenames.Add((tempPath, sink.FilePath!));
                stagedPaths.Add(sink.FilePath!);
            }
            catch (OperationCanceledException) when (_cancellationToken.IsCancellationRequested)
            {
                if (tempPath is not null)
                {
                    DeleteTempFileBestEffort(tempPath);
                }

                DeletePendingTemps(pendingRenames);
                return BuildRouteResult(
                    sinks, failedPaths, committedPaths, stagedPaths, errorDetails, deliveredStreamPaths,
                    cancelled: true);
            }
            catch (Exception ex)
            {
                if (tempPath is not null)
                {
                    DeleteTempFileBestEffort(tempPath);
                }

                failedPaths.Add(sink.FilePath!);
                errorDetails.Add(ex.Message);
            }
        }

        if (failedPaths.Count > 0)
        {
            DeletePendingTemps(pendingRenames);
            return BuildRouteResult(
                sinks, failedPaths, committedPaths, stagedPaths, errorDetails, deliveredStreamPaths);
        }

        // Commit staged files before publishing streams. A later rename failure can leave an
        // explicitly reported partial file set, but it cannot send stdout/stderr reports first.
        for (int index = 0; index < pendingRenames.Count; index++)
        {
            if (_cancellationToken.IsCancellationRequested)
            {
                DeletePendingTemps(pendingRenames.Skip(index));
                return BuildRouteResult(
                    sinks, failedPaths, committedPaths, stagedPaths, errorDetails, deliveredStreamPaths,
                    cancelled: true);
            }

            (string tempPath, string targetPath) = pendingRenames[index];
            try
            {
                fileSystem.RenameTempToTarget(tempPath, targetPath);
                committedPaths.Add(targetPath);
            }
            catch (OperationCanceledException) when (_cancellationToken.IsCancellationRequested)
            {
                DeleteTempFileBestEffort(tempPath);
                DeletePendingTemps(pendingRenames.Skip(index + 1));
                return BuildRouteResult(
                    sinks, failedPaths, committedPaths, stagedPaths, errorDetails, deliveredStreamPaths,
                    cancelled: true);
            }
            catch (Exception ex)
            {
                failedPaths.Add(targetPath);
                errorDetails.Add(ex.Message);
                DeleteTempFileBestEffort(tempPath);
                DeletePendingTemps(pendingRenames.Skip(index + 1));
                return BuildRouteResult(
                    sinks, failedPaths, committedPaths, stagedPaths, errorDetails, deliveredStreamPaths);
            }
        }

        // A stream cannot be rolled back. Keep the deterministic stdout-before-stderr ordering,
        // record each successful delivery, and report PartialOutput if a later stream fails.
        foreach (HistoryReportSink sink in sinks
            .Where(sink => sink.DestinationType != HistoryReportDestinationType.File)
            .OrderBy(sink => sink.DestinationType == HistoryReportDestinationType.Stdout ? 0 : 1))
        {
            if (_cancellationToken.IsCancellationRequested)
            {
                return BuildRouteResult(
                    sinks, failedPaths, committedPaths, stagedPaths, errorDetails, deliveredStreamPaths,
                    cancelled: true);
            }

            string content = sink.Format == "json" ? jsonContent! : markdownContent!;
            string destination = StreamPath(sink.DestinationType);
            try
            {
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

                deliveredStreamPaths.Add(destination);
            }
            catch (OperationCanceledException) when (_cancellationToken.IsCancellationRequested)
            {
                return BuildRouteResult(
                    sinks, failedPaths, committedPaths, stagedPaths, errorDetails, deliveredStreamPaths,
                    cancelled: true);
            }
            catch (Exception ex)
            {
                failedPaths.Add(destination);
                errorDetails.Add(ex.Message);
                break;
            }
        }

        if (failedPaths.Count > 0)
        {
            return BuildRouteResult(
                sinks, failedPaths, committedPaths, stagedPaths, errorDetails, deliveredStreamPaths);
        }

        return BuildRouteResult(
            sinks, failedPaths, committedPaths, stagedPaths, errorDetails, deliveredStreamPaths);
    }

    private HistoryReportRouteResult BuildRouteResult(
        IReadOnlyList<HistoryReportSink> sinks,
        IReadOnlyList<string> failedPaths,
        IReadOnlyList<string> committedPaths,
        IReadOnlyList<string> stagedPaths,
        IReadOnlyList<string> errorDetails,
        IReadOnlyList<string> deliveredStreamPaths,
        bool cancelled = false)
    {
        if (failedPaths.Count == 0 && !cancelled)
        {
            return new HistoryReportRouteResult(
                HistoryReportRouteStatus.AllSucceeded,
                Array.Empty<string>(),
                committedPaths,
                stagedPaths,
                Array.Empty<string>(),
                Array.Empty<string>(),
                deliveredStreamPaths);
        }

        HashSet<string> completedPaths = committedPaths
            .Concat(deliveredStreamPaths)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        IReadOnlyList<string> uncommittedPaths = sinks
            .Select(SinkPath)
            .Where(path => !completedPaths.Contains(path))
            .ToArray();
        HistoryReportRouteStatus status = committedPaths.Count > 0 || deliveredStreamPaths.Count > 0
            ? HistoryReportRouteStatus.PartialOutput
            : HistoryReportRouteStatus.OutputFailed;
        return new HistoryReportRouteResult(
            status,
            failedPaths,
            committedPaths,
            stagedPaths,
            uncommittedPaths,
            errorDetails,
            deliveredStreamPaths)
        {
            Cancelled = cancelled,
        };
    }

    private void WriteRouteFailureDiagnostic(HistoryReportRouteResult route)
    {
        string publicationStatus = route.Status == HistoryReportRouteStatus.PartialOutput
            ? "partial-output"
            : "output-failed";
        console.Error.Write(HistoryDiagnosticJsonWriter.Write(new HistoryDiagnostic(
            HistoryDiagnosticKind.ReportPublicationFailed,
            "History report publication did not complete.",
            publicationStatus: publicationStatus,
            publicationCancelled: route.Cancelled,
            failedDestinations: route.FailedPaths,
            committedDestinations: route.CommittedPaths,
            deliveredDestinations: route.DeliveredStreamPaths,
            uncommittedDestinations: route.UncommittedPaths,
            publicationDetails: route.ErrorDetails)));
    }

    private static string SinkPath(HistoryReportSink sink) =>
        sink.DestinationType == HistoryReportDestinationType.File
            ? sink.FilePath!
            : StreamPath(sink.DestinationType);

    private static string StreamPath(HistoryReportDestinationType destinationType) =>
        destinationType == HistoryReportDestinationType.Stdout ? "<stdout>" : "<stderr>";

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

    private void DeletePendingTemps(IEnumerable<(string TempPath, string TargetPath)> pendingRenames)
    {
        foreach ((string tempPath, string _) in pendingRenames)
        {
            DeleteTempFileBestEffort(tempPath);
        }
    }

    private void DeleteTempFileBestEffort(string tempPath)
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

    private string? FindReportSinkCollision(IReadOnlyList<HistoryReportSink> sinks, string? policyPath)
    {
        HistoryReportSink[] fileSinks = sinks
            .Where(sink => sink.DestinationType == HistoryReportDestinationType.File)
            .ToArray();

        for (int firstIndex = 0; firstIndex < fileSinks.Length; firstIndex++)
        {
            for (int secondIndex = firstIndex + 1; secondIndex < fileSinks.Length; secondIndex++)
            {
                HistoryReportSink first = fileSinks[firstIndex];
                HistoryReportSink second = fileSinks[secondIndex];
                if (fileSystem.AreSameExistingFile(first.FilePath!, second.FilePath!))
                {
                    return $"--report destinations '{first.FilePath}' and '{second.FilePath}' refer to the same existing file";
                }
            }
        }

        if (policyPath is null)
        {
            return null;
        }

        foreach (HistoryReportSink sink in fileSinks)
        {
            // AreSameExistingFile matches case-insensitively by path first (so a same-named
            // destination collides on a case-insensitive filesystem even before either file
            // exists) and falls back to real file-identity comparison for hardlinks/symlinks once
            // both paths exist.
            if (fileSystem.AreSameExistingFile(sink.FilePath!, policyPath))
            {
                return $"--report destination '{sink.FilePath}' matches --policy input '{policyPath}'";
            }
        }

        return null;
    }
}
