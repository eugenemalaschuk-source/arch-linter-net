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
    private const string Usage = "arch-linter-net history analyze --from <rev> --to <rev> [--repository <path>] [--policy <path>] [--enrich-dotnet] [--format json|markdown] [--report <format>=<destination>] [--release-summary-json] [--timings]";

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

        bool hasJsonSink = sinks.Count == 0
            ? options.Format == "json"
            : sinks.Any(static sink => sink.Format == "json");
        if (options.ReleaseSummaryJson && !hasJsonSink)
        {
            console.Error.WriteLine($"--release-summary-json requires a JSON output. Usage: {Usage}");
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
            timing,
            _cancellationToken);
        HistoryIngestionResult? result = outcome.Result;
        if (result is null)
        {
            console.Error.Write(HistoryDiagnosticJsonWriter.Write(outcome.Diagnostic!));
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }

        if (sinks.Count == 0)
        {
            return ExecuteLegacySingleFormat(options.Format, result, timing, options.ReleaseSummaryJson);
        }

        return ExecuteReportSinks(sinks, result, timing, options.ReleaseSummaryJson);
    }

    private int ExecuteLegacySingleFormat(
        string format,
        HistoryIngestionResult result,
        HistoryIngestionTiming? timing,
        bool releaseSummaryJson)
    {
        _cancellationToken.ThrowIfCancellationRequested();
        if (format == "markdown")
        {
            Stopwatch? markdownClock = timing is null ? null : HistoryIngestionTiming.Start();
            string content = HistoryIngestionTextWriter.Write(result);
            timing?.Record("markdown_render", markdownClock);
            _cancellationToken.ThrowIfCancellationRequested();
            console.Out.Write(content);
            return CliExitCodes.Success;
        }

        bool written = HistoryReportOutputWriter.TryWriteJson(console, () =>
        {
            Stopwatch? jsonClock = timing is null ? null : HistoryIngestionTiming.Start();
            _cancellationToken.ThrowIfCancellationRequested();
            string content = HistoryIngestionJsonWriter.Write(result, releaseSummaryJson);
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
        HistoryIngestionTiming? timing,
        bool releaseSummaryJson)
    {
        string? jsonContent = null;
        string? markdownContent = null;

        if (sinks.Any(sink => sink.Format == "json"))
        {
            Stopwatch? jsonClock = timing is null ? null : HistoryIngestionTiming.Start();
            if (!HistoryReportOutputWriter.TryRenderJson(
                    console,
                    () =>
                    {
                        _cancellationToken.ThrowIfCancellationRequested();
                        return HistoryIngestionJsonWriter.Write(result, releaseSummaryJson);
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
            Stopwatch? markdownClock = timing is null ? null : HistoryIngestionTiming.Start();
            markdownContent = HistoryIngestionTextWriter.Write(result);
            timing?.Record("markdown_render", markdownClock);
        }

        Stopwatch? outputClock = timing is null ? null : HistoryIngestionTiming.Start();
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
        RouteState state = new(sinks);
        HistoryReportRouteResult? stagingFailure = StageFileSinks(state, jsonContent, markdownContent);
        if (stagingFailure is not null)
        {
            return stagingFailure.Value;
        }

        HistoryReportRouteResult? commitFailure = CommitFileSinks(state);
        if (commitFailure is not null)
        {
            return commitFailure.Value;
        }

        return PublishStreamSinks(state, jsonContent, markdownContent);
    }

    private HistoryReportRouteResult? StageFileSinks(RouteState state, string? jsonContent, string? markdownContent)
    {
        foreach (HistoryReportSink sink in state.Sinks.Where(item => item.DestinationType == HistoryReportDestinationType.File))
        {
            if (_cancellationToken.IsCancellationRequested)
            {
                DeletePendingTemps(state.PendingRenames);
                return BuildRouteResult(state, cancelled: true);
            }

            HistoryReportRouteResult? failure = StageFileSink(state, sink, ContentFor(sink, jsonContent, markdownContent));
            if (failure is not null)
            {
                return failure.Value;
            }
        }

        if (state.FailedPaths.Count == 0)
        {
            return null;
        }

        DeletePendingTemps(state.PendingRenames);
        return BuildRouteResult(state);
    }

    private HistoryReportRouteResult? StageFileSink(RouteState state, HistoryReportSink sink, string content)
    {
        string? tempPath = null;
        try
        {
            tempPath = fileSystem.WriteAllTextToTemp(sink.FilePath!, content);
            ValidateWrittenTempFile(tempPath, sink.Format);
            state.PendingRenames.Add((tempPath, sink.FilePath!));
            state.StagedPaths.Add(sink.FilePath!);
            return null;
        }
        catch (OperationCanceledException) when (_cancellationToken.IsCancellationRequested)
        {
            CleanupTempAndPending(tempPath, state.PendingRenames);
            return BuildRouteResult(state, cancelled: true);
        }
        catch (Exception exception)
        {
            if (tempPath is not null)
            {
                DeleteTempFileBestEffort(tempPath);
            }

            state.FailedPaths.Add(sink.FilePath!);
            state.ErrorDetails.Add(exception.Message);
            return null;
        }
    }

    // Commit staged files before publishing streams. A later rename failure can leave an
    // explicitly reported partial file set, but it cannot send stdout/stderr reports first.
    private HistoryReportRouteResult? CommitFileSinks(RouteState state)
    {
        for (int index = 0; index < state.PendingRenames.Count; index++)
        {
            if (_cancellationToken.IsCancellationRequested)
            {
                DeletePendingTemps(state.PendingRenames.Skip(index));
                return BuildRouteResult(state, cancelled: true);
            }

            HistoryReportRouteResult? failure = CommitFileSink(state, index);
            if (failure is not null)
            {
                return failure.Value;
            }
        }

        return null;
    }

    private HistoryReportRouteResult? CommitFileSink(RouteState state, int index)
    {
        (string tempPath, string targetPath) = state.PendingRenames[index];
        try
        {
            fileSystem.RenameTempToTarget(tempPath, targetPath);
            state.CommittedPaths.Add(targetPath);
            return null;
        }
        catch (OperationCanceledException) when (_cancellationToken.IsCancellationRequested)
        {
            DeleteTempFileBestEffort(tempPath);
            DeletePendingTemps(state.PendingRenames.Skip(index + 1));
            return BuildRouteResult(state, cancelled: true);
        }
        catch (Exception exception)
        {
            state.FailedPaths.Add(targetPath);
            state.ErrorDetails.Add(exception.Message);
            DeleteTempFileBestEffort(tempPath);
            DeletePendingTemps(state.PendingRenames.Skip(index + 1));
            return BuildRouteResult(state);
        }
    }

    // A stream cannot be rolled back. Preserve stdout-before-stderr ordering and record each
    // successful delivery so later failures are reported as partial output.
    private HistoryReportRouteResult PublishStreamSinks(RouteState state, string? jsonContent, string? markdownContent)
    {
        foreach (HistoryReportSink sink in OrderedStreamSinks(state.Sinks))
        {
            if (_cancellationToken.IsCancellationRequested)
            {
                return BuildRouteResult(state, cancelled: true);
            }

            HistoryReportRouteResult? failure = PublishStreamSink(
                state, sink, ContentFor(sink, jsonContent, markdownContent));
            if (failure is not null)
            {
                return failure.Value;
            }
        }

        return BuildRouteResult(state);
    }

    private HistoryReportRouteResult? PublishStreamSink(RouteState state, HistoryReportSink sink, string content)
    {
        string destination = StreamPath(sink.DestinationType);
        try
        {
            if (sink.DestinationType == HistoryReportDestinationType.Stdout)
            {
                WriteStdout(sink.Format, content);
            }
            else
            {
                console.Error.Write(content);
            }

            state.DeliveredStreamPaths.Add(destination);
            return null;
        }
        catch (OperationCanceledException) when (_cancellationToken.IsCancellationRequested)
        {
            return BuildRouteResult(state, cancelled: true);
        }
        catch (Exception exception)
        {
            state.FailedPaths.Add(destination);
            state.ErrorDetails.Add(exception.Message);
            return BuildRouteResult(state);
        }
    }

    private void WriteStdout(string format, string content)
    {
        if (format == "json")
        {
            console.WriteCanonicalJson(content);
            return;
        }

        console.Out.Write(content);
    }

    private static string ContentFor(HistoryReportSink sink, string? jsonContent, string? markdownContent)
    {
        return sink.Format == "json" ? jsonContent! : markdownContent!;
    }

    private static IEnumerable<HistoryReportSink> OrderedStreamSinks(IReadOnlyList<HistoryReportSink> sinks)
    {
        return sinks.Where(sink => sink.DestinationType != HistoryReportDestinationType.File)
            .OrderBy(sink => sink.DestinationType == HistoryReportDestinationType.Stdout ? 0 : 1);
    }

    private void CleanupTempAndPending(
        string? tempPath,
        IEnumerable<(string TempPath, string TargetPath)> pendingRenames)
    {
        if (tempPath is not null)
        {
            DeleteTempFileBestEffort(tempPath);
        }

        DeletePendingTemps(pendingRenames);
    }

    private static HistoryReportRouteResult BuildRouteResult(RouteState state, bool cancelled = false)
    {
        if (state.FailedPaths.Count == 0 && !cancelled)
        {
            return new HistoryReportRouteResult(
                HistoryReportRouteStatus.AllSucceeded,
                Array.Empty<string>(),
                state.CommittedPaths,
                state.StagedPaths,
                Array.Empty<string>(),
                Array.Empty<string>(),
                state.DeliveredStreamPaths);
        }

        HashSet<string> completedPaths = state.CommittedPaths
            .Concat(state.DeliveredStreamPaths)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        IReadOnlyList<string> uncommittedPaths = state.Sinks
            .Select(SinkPath)
            .Where(path => !completedPaths.Contains(path))
            .ToArray();
        HistoryReportRouteStatus status = state.CommittedPaths.Count > 0 || state.DeliveredStreamPaths.Count > 0
            ? HistoryReportRouteStatus.PartialOutput
            : HistoryReportRouteStatus.OutputFailed;
        return new HistoryReportRouteResult(
            status,
            state.FailedPaths,
            state.CommittedPaths,
            state.StagedPaths,
            uncommittedPaths,
            state.ErrorDetails,
            state.DeliveredStreamPaths)
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
            publication: new HistoryDiagnosticPublication(
                Status: publicationStatus,
                Cancelled: route.Cancelled,
                FailedDestinations: route.FailedPaths,
                CommittedDestinations: route.CommittedPaths,
                DeliveredDestinations: route.DeliveredStreamPaths,
                UncommittedDestinations: route.UncommittedPaths,
                Details: route.ErrorDetails))));
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

    private sealed class RouteState(IReadOnlyList<HistoryReportSink> sinks)
    {
        public IReadOnlyList<HistoryReportSink> Sinks { get; } = sinks;

        public List<(string TempPath, string TargetPath)> PendingRenames { get; } = new();

        public List<string> FailedPaths { get; } = new();

        public List<string> CommittedPaths { get; } = new();

        public List<string> StagedPaths { get; } = new();

        public List<string> ErrorDetails { get; } = new();

        public List<string> DeliveredStreamPaths { get; } = new();
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
