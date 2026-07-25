using System.Text;
using System.Text.Json.Nodes;
using ArchLinterNet.Cli.Abstractions;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;
using ArchLinterNet.Core.Validation;

namespace ArchLinterNet.Cli.Commands.Validate;

internal enum ReportRouteStatus
{
    AllSucceeded,
    PartialOutput,
    OutputFailed,
}

internal readonly record struct RouteResult(
    ReportRouteStatus Status,
    IReadOnlyList<string> FailedPaths,
    IReadOnlyList<string> CommittedPaths,
    IReadOnlyList<string> StagedPaths,
    IReadOnlyList<string> UncommittedPaths,
    IReadOnlyList<string> ErrorDetails,
    IReadOnlyList<string> DeliveredStreamPaths);

internal sealed class ReportCoordinator
{
    private const int MaxReportBytes = 100 * 1024 * 1024;
    private const string FormatHuman = "human";
    private const string FormatJson = "json";
    private const string FormatSarif = "sarif";

    private readonly ICliRuntime _runtime;
    private readonly ICliConsole _console;
    private readonly IFileSystem _fileSystem;

    public ReportCoordinator(ICliRuntime runtime, ICliConsole console, IFileSystem fileSystem)
    {
        _runtime = runtime;
        _console = console;
        _fileSystem = fileSystem;
    }

    public RouteResult RouteSingleOutcome(
        string stdoutFormat,
        string mode,
        ValidationOutcome outcome,
        IReadOnlyList<ReportSink> additionalSinks)
    {
        bool isReportMode = additionalSinks.Count > 0;
        return RouteOutcomes(stdoutFormat, new[] { (mode, outcome) }, additionalSinks, isSingleMode: true, isReportMode);
    }

    public RouteResult RouteCombinedOutcomes(
        string stdoutFormat,
        IReadOnlyList<(string Mode, ValidationOutcome Outcome)> outcomesByMode,
        IReadOnlyList<ReportSink> additionalSinks)
    {
        bool isReportMode = additionalSinks.Count > 0;
        return RouteOutcomes(stdoutFormat, outcomesByMode, additionalSinks, isSingleMode: false, isReportMode);
    }

    private RouteResult RouteOutcomes(
        string stdoutFormat,
        IReadOnlyList<(string Mode, ValidationOutcome Outcome)> outcomesByMode,
        IReadOnlyList<ReportSink> additionalSinks,
        bool isSingleMode,
        bool isReportMode)
    {
        // Legacy combined human: write each mode sequentially (pre-#364 behavior)
        bool legacyCombinedHuman = !isReportMode && !isSingleMode && stdoutFormat == FormatHuman;

        string? humanContent = ResolveHumanContent(
            StdoutOrAnySinkNeeds(FormatHuman, stdoutFormat, additionalSinks, isReportMode),
            legacyCombinedHuman, isSingleMode, outcomesByMode);
        string? jsonContent = ResolveStructuredContent(
            StdoutOrAnySinkNeeds(FormatJson, stdoutFormat, additionalSinks, isReportMode),
            isSingleMode, outcomesByMode, FormatSingleJson, FormatCombinedJson);
        string? sarifContent = ResolveStructuredContent(
            StdoutOrAnySinkNeeds(FormatSarif, stdoutFormat, additionalSinks, isReportMode),
            isSingleMode, outcomesByMode, FormatSingleSarif, FormatCombinedSarif);

        if (!isReportMode && !legacyCombinedHuman)
        {
            _console.Out.WriteLine(DispatchFormat(stdoutFormat, humanContent, jsonContent, sarifContent));
        }

        return DistributeToSinks(additionalSinks, BuildContentByFormat(humanContent, jsonContent, sarifContent));
    }

    private string? ResolveHumanContent(
        string? neededHuman,
        bool legacyCombinedHuman,
        bool isSingleMode,
        IReadOnlyList<(string Mode, ValidationOutcome Outcome)> outcomesByMode)
    {
        if (neededHuman is null)
        {
            return null;
        }

        if (!legacyCombinedHuman)
        {
            return isSingleMode
                ? FormatSingleHuman(outcomesByMode[0].Outcome)
                : FormatCombinedHuman(outcomesByMode);
        }

        foreach ((_, ValidationOutcome outcome) in outcomesByMode)
        {
            _console.Out.WriteLine(FormatSingleHuman(outcome));
        }

        return null;
    }

    private static string? ResolveStructuredContent(
        string? needed,
        bool isSingleMode,
        IReadOnlyList<(string Mode, ValidationOutcome Outcome)> outcomesByMode,
        Func<string, ValidationOutcome, string> formatSingle,
        Func<IReadOnlyList<(string Mode, ValidationOutcome Outcome)>, string> formatCombined)
    {
        if (needed is null)
        {
            return null;
        }

        return isSingleMode
            ? formatSingle(outcomesByMode[0].Mode, outcomesByMode[0].Outcome)
            : formatCombined(outcomesByMode);
    }

    private static Dictionary<string, string> BuildContentByFormat(string? humanContent, string? jsonContent, string? sarifContent)
    {
        Dictionary<string, string> contentByFormat = new();
        if (humanContent is not null)
        {
            contentByFormat[FormatHuman] = humanContent;
        }
        if (jsonContent is not null)
        {
            contentByFormat[FormatJson] = jsonContent;
        }
        if (sarifContent is not null)
        {
            contentByFormat[FormatSarif] = sarifContent;
        }
        return contentByFormat;
    }

    // Re-renders the full report for one format from an already-computed outcome — the contract
    // execution and analysis this reads from already happened; this only re-serializes it. Used to
    // embed the complete normalized findings into an output-routing-error document, so a sink
    // failure never reduces what reaches the user to bare pass/fail counts.
    public string RenderReportContent(
        string format, bool isSingleMode, IReadOnlyList<(string Mode, ValidationOutcome Outcome)> outcomesByMode)
    {
        return format switch
        {
            FormatJson => isSingleMode
                ? FormatSingleJson(outcomesByMode[0].Mode, outcomesByMode[0].Outcome)
                : FormatCombinedJson(outcomesByMode),
            FormatSarif => isSingleMode
                ? FormatSingleSarif(outcomesByMode[0].Mode, outcomesByMode[0].Outcome)
                : FormatCombinedSarif(outcomesByMode),
            _ => isSingleMode
                ? FormatSingleHuman(outcomesByMode[0].Outcome)
                : FormatCombinedHuman(outcomesByMode),
        };
    }

    // Routes error content (policy load failures, unexpected runtime errors) to every configured
    // sink whose format matches — file, stdout, and stderr alike. Safe to use for these errors
    // specifically because they occur before any legitimate report content has been produced for
    // this invocation, so there is nothing at the destination for the error content to clobber.
    public RouteResult RouteErrorToAllSinks(
        IReadOnlyList<ReportSink> additionalSinks,
        IReadOnlyDictionary<string, string> contentByFormat)
    {
        return DistributeToSinks(additionalSinks, contentByFormat);
    }

    private RouteResult DistributeToSinks(
        IReadOnlyList<ReportSink> additionalSinks,
        IReadOnlyDictionary<string, string> contentByFormat)
    {
        List<string> failedPaths = new();
        List<string> stagedPaths = new();
        List<string> errorDetails = new();
        List<string> deliveredStreamPaths = new();
        List<(string TempPath, string TargetPath)> pendingRenames = new();

        // Do not emit a normal stream document until every file artifact is committed. Otherwise
        // a later file-stage/commit failure leaves a successful --report ...=stderr stream
        // carrying only a normal report while the process exits 2 with no output_status evidence.
        foreach (ReportSink sink in additionalSinks.Where(sink => sink.DestinationType == ReportDestinationType.File))
        {
            StageFileSink(sink, contentByFormat, failedPaths, stagedPaths, errorDetails, pendingRenames);
        }

        List<string> committedPaths = new();
        if (failedPaths.Count == 0)
        {
            CommitPendingRenames(pendingRenames, committedPaths, failedPaths, errorDetails);
        }

        if (failedPaths.Count == 0)
        {
            // Stderr is last so a failed stdout never leaves a successful stderr report that
            // hides the same invocation's output failure. A failed stderr is retried by the
            // handler with the enriched fallback document.
            foreach (ReportSink sink in additionalSinks
                .Where(sink => sink.DestinationType != ReportDestinationType.File)
                .OrderBy(sink => sink.DestinationType == ReportDestinationType.Stdout ? 0 : 1))
            {
                WriteStreamSink(sink, contentByFormat, failedPaths, errorDetails, deliveredStreamPaths);
            }
        }

        if (failedPaths.Count > 0 && committedPaths.Count == 0)
        {
            DeletePendingTemps(pendingRenames);
        }

        return BuildRouteResult(
            additionalSinks, contentByFormat, failedPaths, committedPaths, stagedPaths, errorDetails, deliveredStreamPaths);
    }

    private void StageFileSink(
        ReportSink sink,
        IReadOnlyDictionary<string, string> contentByFormat,
        List<string> failedPaths,
        List<string> stagedPaths,
        List<string> errorDetails,
        List<(string TempPath, string TargetPath)> pendingRenames)
    {
        if (!contentByFormat.TryGetValue(sink.Format, out string? content))
        {
            return;
        }

        try
        {
            ValidateContentSize(content);
            if (sink.Format is FormatJson or FormatSarif)
            {
                _ = JsonNode.Parse(content);
            }

            string tempPath = _fileSystem.WriteAllTextToTemp(sink.FilePath!, content);
            ValidateWrittenTempFile(tempPath, sink.Format);
            pendingRenames.Add((tempPath, sink.FilePath!));
            stagedPaths.Add(sink.FilePath!);
        }
        catch (Exception ex)
        {
            failedPaths.Add(sink.FilePath!);
            errorDetails.Add(ex.Message);
        }
    }

    private void WriteStreamSink(
        ReportSink sink,
        IReadOnlyDictionary<string, string> contentByFormat,
        List<string> failedPaths,
        List<string> errorDetails,
        List<string> deliveredStreamPaths)
    {
        if (!contentByFormat.TryGetValue(sink.Format, out string? content))
        {
            return;
        }

        try
        {
            WriteToStream(sink.DestinationType, content);
            deliveredStreamPaths.Add(StreamFailureMarker(sink.DestinationType));
        }
        catch (Exception ex)
        {
            failedPaths.Add(StreamFailureMarker(sink.DestinationType));
            errorDetails.Add(ex.Message);
        }
    }

    private void WriteToStream(ReportDestinationType destinationType, string content)
    {
        if (destinationType == ReportDestinationType.Stderr)
        {
            _console.Error.WriteLine(content);
        }
        else
        {
            _console.Out.WriteLine(content);
        }
    }

    private static string StreamFailureMarker(ReportDestinationType destinationType)
    {
        return destinationType == ReportDestinationType.Stderr ? "<stderr>" : "<stdout>";
    }

    // Phase 2 only runs once every staged sink is already known-good (StageSink validated each
    // temp file before adding it here), so a failure at this point is a genuine OS-level rename
    // fault, not a precondition this coordinator could have caught earlier.
    private void CommitPendingRenames(
        List<(string TempPath, string TargetPath)> pendingRenames,
        List<string> committedPaths,
        List<string> failedPaths,
        List<string> errorDetails)
    {
        foreach ((string tempPath, string targetPath) in pendingRenames)
        {
            try
            {
                _fileSystem.RenameTempToTarget(tempPath, targetPath);
                committedPaths.Add(targetPath);
            }
            catch (Exception ex)
            {
                failedPaths.Add(targetPath);
                errorDetails.Add(ex.Message);
                DeleteTempFileBestEffort(tempPath);
            }
        }
    }

    private void DeleteTempFileBestEffort(string tempPath)
    {
        // Cleanup only — a failure here just leaves a stray .tmp file behind, which doesn't
        // change the RouteResult already being returned for this invocation.
        try
        {
            _fileSystem.DeleteFile(tempPath);
        }
        catch
        {
            // Deliberately swallowed — see comment above.
        }
    }

    private void DeletePendingTemps(IEnumerable<(string TempPath, string TargetPath)> pendingRenames)
    {
        foreach ((string tempPath, string _) in pendingRenames)
        {
            DeleteTempFileBestEffort(tempPath);
        }
    }

    private static RouteResult BuildRouteResult(
        IReadOnlyList<ReportSink> additionalSinks,
        IReadOnlyDictionary<string, string> contentByFormat,
        List<string> failedPaths,
        List<string> committedPaths,
        List<string> stagedPaths,
        List<string> errorDetails,
        List<string> deliveredStreamPaths)
    {
        if (failedPaths.Count == 0)
        {
            return new RouteResult(
                ReportRouteStatus.AllSucceeded, Array.Empty<string>(), committedPaths, stagedPaths,
                Array.Empty<string>(), Array.Empty<string>(), deliveredStreamPaths);
        }

        var allFileSinks = additionalSinks
            .Where(s => s.DestinationType == ReportDestinationType.File && contentByFormat.ContainsKey(s.Format))
            .Select(s => s.FilePath!)
            .ToArray();
        var uncommittedPaths = allFileSinks.Except(committedPaths).ToArray();

        ReportRouteStatus status = committedPaths.Count > 0
            ? ReportRouteStatus.PartialOutput
            : ReportRouteStatus.OutputFailed;

        return new RouteResult(
            status, failedPaths, committedPaths, stagedPaths, uncommittedPaths, errorDetails, deliveredStreamPaths);
    }

    // Re-validates the bytes actually landed on disk rather than trusting the in-memory string that
    // was handed to WriteAllTextToTemp — catches truncated/corrupted writes (disk full, concurrent
    // modification) before the content is ever renamed into the target path. A missing temp file is
    // a hard failure (not a silent no-op): letting it through here would mean phase 2 discovers the
    // gap mid-rename, after other sinks in the same batch have already committed, producing partial
    // output instead of the clean all-or-nothing failure this staging phase is supposed to guarantee.
    private void ValidateWrittenTempFile(string tempPath, string format)
    {
        if (!_fileSystem.FileExists(tempPath))
        {
            throw new InvalidOperationException($"Temp report file was not created: {tempPath}");
        }

        try
        {
            string writtenContent = _fileSystem.ReadAllText(tempPath);
            ValidateContentSize(writtenContent);

            if (format is FormatJson or FormatSarif)
            {
                _ = JsonNode.Parse(writtenContent);
            }
        }
        catch (Exception)
        {
            DeleteTempFileBestEffort(tempPath);
            throw;
        }
    }

    private static string? StdoutOrAnySinkNeeds(
        string format,
        string stdoutFormat,
        IReadOnlyList<ReportSink> sinks,
        bool isReportMode)
    {
        if (!isReportMode && stdoutFormat == format)
        {
            return "stdout";
        }

        foreach (ReportSink sink in sinks)
        {
            if (sink.Format == format)
            {
                return "sink";
            }
        }

        return null;
    }

    private static void ValidateContentSize(string content)
    {
        if (Encoding.UTF8.GetByteCount(content) > MaxReportBytes)
        {
            throw new InvalidOperationException(
                $"Report content exceeds maximum size of {MaxReportBytes} bytes.");
        }
    }

    private string FormatSingleHuman(ValidationOutcome outcome)
    {
        var sb = new StringBuilder();
        AppendHumanSection(sb, outcome);
        return sb.ToString().TrimEnd();
    }

    private string FormatCombinedHuman(IReadOnlyList<(string Mode, ValidationOutcome Outcome)> outcomesByMode)
    {
        var sb = new StringBuilder();
        bool first = true;
        foreach ((string mode, ValidationOutcome outcome) in outcomesByMode)
        {
            if (!first)
            {
                sb.AppendLine();
            }
            first = false;
            if (outcomesByMode.Count > 1)
            {
                sb.AppendLine($"=== Mode: {mode} ===");
            }
            AppendHumanSection(sb, outcome);
        }
        return sb.ToString().TrimEnd();
    }

    private void AppendHumanSection(StringBuilder sb, ValidationOutcome outcome)
    {
        string preflight = FormatHumanPreflight(outcome);
        if (!string.IsNullOrEmpty(preflight))
        {
            sb.AppendLine(preflight);
        }

        if (outcome.PreflightBlocked)
        {
            return;
        }

        if (outcome.Passed)
        {
            sb.AppendLine("Architecture validation passed.");
        }
        else
        {
            if (outcome.Violations.Count > 0)
            {
                sb.AppendLine(_runtime.FormatViolationsForHumans(outcome.Violations));
            }

            if (outcome.Cycles.Count > 0)
            {
                sb.AppendLine(_runtime.FormatCyclesForHumans(outcome.Cycles, outcome.CycleFindings));
            }
        }

        AppendSection(sb, outcome.PolicyConsistencyConfig != "off" && outcome.PolicyConsistencyFindings.Count > 0,
            () => _runtime.FormatPolicyConsistencyForHumans(outcome.PolicyConsistencyFindings));
        AppendSection(sb, outcome.UnmatchedIgnoredViolations.Count > 0 && outcome.UnmatchedIgnoredViolationsConfig != "off",
            () => _runtime.FormatUnmatchedForHumans(outcome.UnmatchedIgnoredViolations));
        AppendSection(sb, outcome.CoverageConfig != "off" && outcome.CoverageFindings.Count > 0,
            () => _runtime.FormatCoverageForHumans(outcome.CoverageFindings));
        AppendSection(sb, outcome.CoverageSummaries.Count > 0,
            () => _runtime.FormatCoverageSummaryForHumans(outcome.CoverageSummaries));
        AppendSection(sb, outcome.ClassificationConflicts.Count > 0 || outcome.ClassificationMetadataFailures.Count > 0
                || outcome.ClassificationPathDeferred != null,
            () => _runtime.FormatClassificationFactsForHumans(
                outcome.ClassificationConflicts, outcome.ClassificationMetadataFailures, outcome.ClassificationPathDeferred));
    }

    private string FormatHumanPreflight(ValidationOutcome outcome)
    {
        if (outcome.PreflightDiagnostics.Count == 0)
        {
            return string.Empty;
        }

        string text = _runtime.FormatBuildStatePreflightForHumans(outcome.PreflightDiagnostics);
        return string.IsNullOrEmpty(text) ? string.Empty : $"\n{text}";
    }

    private static void AppendSection(StringBuilder sb, bool shouldWrite, Func<string> contentFactory)
    {
        if (!shouldWrite)
        {
            return;
        }

        string content = contentFactory();
        if (string.IsNullOrEmpty(content))
        {
            return;
        }

        sb.AppendLine();
        sb.AppendLine(content);
    }

    private string FormatSingleJson(string mode, ValidationOutcome outcome)
    {
        return FormatJsonContent(mode, outcome);
    }

    private string FormatCombinedJson(IReadOnlyList<(string Mode, ValidationOutcome Outcome)> outcomesByMode)
    {
        JsonArray results = new();
        foreach ((string mode, ValidationOutcome outcome) in outcomesByMode)
        {
            results.Add(JsonNode.Parse(FormatJsonContent(mode, outcome)));
        }

        return new JsonObject { ["results"] = results }.ToJsonString();
    }

    private string FormatSingleSarif(string mode, ValidationOutcome outcome)
    {
        return FormatSarifContent(mode, outcome);
    }

    private string FormatCombinedSarif(IReadOnlyList<(string Mode, ValidationOutcome Outcome)> outcomesByMode)
    {
        JsonArray runs = new();
        foreach ((string mode, ValidationOutcome outcome) in outcomesByMode)
        {
            JsonNode? document = JsonNode.Parse(FormatSarifContent(mode, outcome));
            foreach (JsonNode? run in document?["runs"]?.AsArray() ?? new JsonArray())
            {
                runs.Add(run?.DeepClone());
            }
        }

        return new JsonObject { ["version"] = "2.1.0", ["runs"] = runs }.ToJsonString();
    }

    private string FormatJsonContent(string mode, ValidationOutcome outcome)
    {
        return _runtime.FormatResultForCiArtifacts(
            mode, outcome.Passed, outcome.Violations, outcome.Cycles, outcome.CycleFindings, outcome.CoverageFindings,
            outcome.UnmatchedIgnoredViolations,
            outcome.PolicyConsistencyConfig == "off" ? Array.Empty<PolicyConsistencyDiagnostic>() : outcome.PolicyConsistencyFindings,
            outcome.CoverageSummaries, outcome.ClassificationConflicts, outcome.ClassificationMetadataFailures,
            outcome.ClassificationRoles, outcome.ClassificationPathDeferred, outcome.PreflightDiagnostics);
    }

    private string FormatSarifContent(string mode, ValidationOutcome outcome)
    {
        return _runtime.FormatResultAsSarif(
            mode, outcome.Violations, outcome.Cycles, outcome.CycleFindings, outcome.PreflightDiagnostics);
    }

    private static string DispatchFormat(
        string format,
        string? humanContent,
        string? jsonContent,
        string? sarifContent)
    {
        return format switch
        {
            FormatHuman => humanContent ?? string.Empty,
            FormatJson => jsonContent ?? string.Empty,
            FormatSarif => sarifContent ?? string.Empty,
            _ => string.Empty,
        };
    }
}
