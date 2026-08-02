using System.Text;
using System.Text.Json.Nodes;
using ArchLinterNet.Cli.Abstractions;
using ArchLinterNet.Cli.Infrastructure;
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
    IReadOnlyList<string> DeliveredStreamPaths)
{
    // Additive evidence, not a fourth ReportRouteStatus value: Status still reports how much
    // committed (AllSucceeded/PartialOutput/OutputFailed); Cancelled reports why the run stopped
    // short of that. Files already renamed into place before cancellation was observed stay
    // committed — there is no code path that undoes a completed rename.
    public bool Cancelled { get; init; }

    // Formats whose normal report document finished rendering. This is separate from configured
    // sinks because cancellation can happen before rendering, and one rendered format may feed
    // several destinations.
    public IReadOnlyList<string> RenderedFormats { get; init; } = Array.Empty<string>();
}

internal sealed partial class ReportCoordinator
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
        IReadOnlyList<ReportSink> additionalSinks,
        CancellationToken cancellationToken = default,
        ValidationTiming? timing = null)
    {
        bool isReportMode = additionalSinks.Count > 0;
        return RouteOutcomes(
            stdoutFormat, new[] { (mode, outcome) }, additionalSinks, isSingleMode: true, isReportMode,
            cancellationToken, timing);
    }

    public RouteResult RouteCombinedOutcomes(
        string stdoutFormat,
        IReadOnlyList<(string Mode, ValidationOutcome Outcome)> outcomesByMode,
        IReadOnlyList<ReportSink> additionalSinks,
        CancellationToken cancellationToken = default,
        ValidationTiming? timing = null)
    {
        bool isReportMode = additionalSinks.Count > 0;
        return RouteOutcomes(
            stdoutFormat, outcomesByMode, additionalSinks, isSingleMode: false, isReportMode, cancellationToken, timing);
    }

    private RouteResult RouteOutcomes(
        string stdoutFormat,
        IReadOnlyList<(string Mode, ValidationOutcome Outcome)> outcomesByMode,
        IReadOnlyList<ReportSink> additionalSinks,
        bool isSingleMode,
        bool isReportMode,
        CancellationToken cancellationToken,
        ValidationTiming? timing)
    {
        IReadOnlyList<ReportSink> requiredSinks = isReportMode
            ? additionalSinks
            : new[] { new ReportSink(stdoutFormat, ReportDestinationType.Stdout) };
        SinkDistributionEvidence evidence = SinkDistributionEvidence.Empty();

        // Legacy combined human: write each mode sequentially (pre-#364 behavior). isReportMode is
        // false here by construction (legacyCombinedHuman requires !isReportMode, and isReportMode
        // is additionalSinks.Count > 0), so there are no file/stream sinks to stage or commit —
        // the per-mode stdout writes below are the entire required publication for this call.
        bool legacyCombinedHuman = !isReportMode && !isSingleMode && stdoutFormat == FormatHuman;
        try
        {
            if (legacyCombinedHuman)
            {
                return WriteLegacyCombinedHuman(outcomesByMode, requiredSinks, evidence, cancellationToken, timing);
            }

            // Checked before any rendering or stream write. DistributeToSinks below only guards
            // file-sink staging/commit — it never ran early enough to stop the plain stdout dispatch a
            // few lines down, which also publishes directly to _console.Out. Without this guard, a
            // token already cancelled by the time this method runs would still let a normal document
            // reach stdout before any cancellation evidence is reported.
            if (cancellationToken.IsCancellationRequested)
            {
                return BuildRouteResult(requiredSinks, evidence, cancelled: true);
            }

            string? humanContent = RenderContent(
                StdoutOrAnySinkNeeds(FormatHuman, stdoutFormat, additionalSinks, isReportMode), FormatHuman,
                () => FormatHumanContent(isSingleMode, outcomesByMode, cancellationToken), evidence, timing);
            string? jsonContent = RenderContent(
                StdoutOrAnySinkNeeds(FormatJson, stdoutFormat, additionalSinks, isReportMode), FormatJson,
                () => FormatStructuredContent(
                    isSingleMode, outcomesByMode, FormatSingleJson, FormatCombinedJson, cancellationToken),
                evidence, timing);
            string? sarifContent = RenderContent(
                StdoutOrAnySinkNeeds(FormatSarif, stdoutFormat, additionalSinks, isReportMode), FormatSarif,
                () => FormatStructuredContent(
                    isSingleMode, outcomesByMode, FormatSingleSarif, FormatCombinedSarif, cancellationToken),
                evidence, timing);

            if (!isReportMode)
            {
                // Rendering above is fast, synchronous, in-memory formatting (no I/O) — one check
                // right before the one required write is enough; no per-line check is needed inside
                // formatting itself. There are no --report sinks on this path (isReportMode is false),
                // so once this single write succeeds, publication for this call is complete.
                if (cancellationToken.IsCancellationRequested)
                {
                    return BuildRouteResult(requiredSinks, evidence, cancelled: true);
                }

                using (timing?.Measure("output_stream_write"))
                    _console.Out.WriteLine(DispatchFormat(stdoutFormat, humanContent, jsonContent, sarifContent));
                evidence.DeliveredStreamPaths.Add(StreamFailureMarker(ReportDestinationType.Stdout));
                return BuildRouteResult(requiredSinks, evidence, cancelled: false);
            }

            return DistributeToSinks(
                additionalSinks, BuildContentByFormat(humanContent, jsonContent, sarifContent), cancellationToken, timing, evidence);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return BuildRouteResult(requiredSinks, evidence, cancelled: true);
        }
    }

    // No file/stream sinks are possible on this path (see caller) — cancellation observed between
    // two modes' writes is the only thing to report; a mode already written to stdout is not
    // undone. Once every mode has been written, publication is complete and the result is
    // AllSucceeded regardless of a cancellation signal observed only after this method returns.
    private RouteResult WriteLegacyCombinedHuman(
        IReadOnlyList<(string Mode, ValidationOutcome Outcome)> outcomesByMode,
        IReadOnlyList<ReportSink> requiredSinks,
        SinkDistributionEvidence evidence,
        CancellationToken cancellationToken,
        ValidationTiming? timing)
    {
        foreach ((_, ValidationOutcome outcome) in outcomesByMode)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return BuildRouteResult(requiredSinks, evidence, cancelled: true);
            }

            string content;
            using (timing?.Measure("render_human"))
                content = FormatSingleHuman(outcome, cancellationToken);
            evidence.RecordRenderedFormat(FormatHuman);
            using (timing?.Measure("output_stream_write"))
                _console.Out.WriteLine(content);
            if (!evidence.DeliveredStreamPaths.Contains(StreamFailureMarker(ReportDestinationType.Stdout)))
            {
                evidence.DeliveredStreamPaths.Add(StreamFailureMarker(ReportDestinationType.Stdout));
            }
        }

        return BuildRouteResult(Array.Empty<ReportSink>(), evidence, cancelled: false);
    }

    // Routes error content (policy load failures, unexpected runtime errors) to every configured
    // sink whose format matches — file, stdout, and stderr alike. Safe to use for these errors
    // specifically because they occur before any legitimate report content has been produced for
    // this invocation, so there is nothing at the destination for the error content to clobber.
    public RouteResult RouteErrorToAllSinks(
        IReadOnlyList<ReportSink> additionalSinks,
        IReadOnlyDictionary<string, string> contentByFormat,
        CancellationToken cancellationToken = default)
    {
        return DistributeToSinks(
            additionalSinks, contentByFormat, cancellationToken, timing: null, evidence: SinkDistributionEvidence.Empty());
    }

    private RouteResult DistributeToSinks(
        IReadOnlyList<ReportSink> additionalSinks,
        IReadOnlyDictionary<string, string> contentByFormat,
        CancellationToken cancellationToken,
        ValidationTiming? timing,
        SinkDistributionEvidence evidence)
    {
        List<(string TempPath, string TargetPath)> pendingRenames = new();

        // Checked before each file sink is staged (not once up front) — cancellation observed
        // between two file sinks must stop staging the remaining ones, not just an already-
        // cancelled token checked before the loop started.
        bool cancelledDuringStaging = false;
        if (additionalSinks.Any(sink => sink.DestinationType == ReportDestinationType.File))
        {
            using (timing?.Measure("output_staging"))
                cancelledDuringStaging = StageAllFileSinks(
                    additionalSinks, contentByFormat, evidence, pendingRenames, cancellationToken);
        }

        if (cancelledDuringStaging)
        {
            DeletePendingTemps(pendingRenames);
            return BuildRouteResult(additionalSinks, evidence, cancelled: true);
        }

        // Do not emit a normal stream document until every file artifact passed staging. Otherwise
        // a later file-stage failure leaves a successful --report ...=stderr stream
        // carrying only a normal report while the process exits 2 with no output_status evidence.
        bool cancelledDuringStreamWrite = false;
        if (evidence.FailedPaths.Count == 0
            && additionalSinks.Any(sink => sink.DestinationType != ReportDestinationType.File))
        {
            using (timing?.Measure("output_stream_write"))
                cancelledDuringStreamWrite = WriteStreamSinksInOrder(
                    additionalSinks, contentByFormat, evidence, cancellationToken);
        }

        if (cancelledDuringStreamWrite)
        {
            DeletePendingTemps(pendingRenames);
            return BuildRouteResult(additionalSinks, evidence, cancelled: true);
        }

        bool cancelledMidCommit = false;
        if (evidence.FailedPaths.Count > 0)
        {
            DeletePendingTemps(pendingRenames);
        }
        else if (pendingRenames.Count > 0)
        {
            using (timing?.Measure("output_commit"))
                cancelledMidCommit = CommitPendingRenames(pendingRenames, evidence, cancellationToken);
        }
        // else: nothing staged needs committing — a stream-only run (or a run with no additional
        // sinks at all) has already fully published by this point. Deliberately not re-checking
        // the token here: cancellation observed only after every required destination already
        // received its document must not retroactively reclassify an already-published result.

        return BuildRouteResult(additionalSinks, evidence, cancelled: cancelledMidCommit);
    }

    // Returns true if cancellation stopped staging before every file sink was attempted.
    private bool StageAllFileSinks(
        IReadOnlyList<ReportSink> additionalSinks,
        IReadOnlyDictionary<string, string> contentByFormat,
        SinkDistributionEvidence evidence,
        List<(string TempPath, string TargetPath)> pendingRenames,
        CancellationToken cancellationToken)
    {
        foreach (ReportSink sink in additionalSinks.Where(sink => sink.DestinationType == ReportDestinationType.File))
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return true;
            }

            StageFileSink(sink, contentByFormat, evidence, pendingRenames);
        }

        return cancellationToken.IsCancellationRequested;
    }

    // Stderr is last so a failed stdout never leaves a successful stderr report that hides the
    // same invocation's output failure. A failed stderr is retried by the handler with the
    // enriched fallback document. Returns true if cancellation stopped this loop before every
    // stream sink was attempted.
    private bool WriteStreamSinksInOrder(
        IReadOnlyList<ReportSink> additionalSinks,
        IReadOnlyDictionary<string, string> contentByFormat,
        SinkDistributionEvidence evidence,
        CancellationToken cancellationToken)
    {
        foreach (ReportSink sink in additionalSinks
            .Where(sink => sink.DestinationType != ReportDestinationType.File)
            .OrderBy(sink => sink.DestinationType == ReportDestinationType.Stdout ? 0 : 1))
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return true;
            }

            if (!WriteStreamSink(sink, contentByFormat, evidence))
            {
                break;
            }
        }

        return false;
    }

    private void StageFileSink(
        ReportSink sink,
        IReadOnlyDictionary<string, string> contentByFormat,
        SinkDistributionEvidence evidence,
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
            evidence.StagedPaths.Add(sink.FilePath!);
        }
        catch (Exception ex)
        {
            evidence.FailedPaths.Add(sink.FilePath!);
            evidence.ErrorDetails.Add(ex.Message);
        }
    }

    private bool WriteStreamSink(
        ReportSink sink,
        IReadOnlyDictionary<string, string> contentByFormat,
        SinkDistributionEvidence evidence)
    {
        if (!contentByFormat.TryGetValue(sink.Format, out string? content))
        {
            return true;
        }

        try
        {
            WriteToStream(sink.DestinationType, content);
            evidence.DeliveredStreamPaths.Add(StreamFailureMarker(sink.DestinationType));
            return true;
        }
        catch (Exception ex)
        {
            evidence.FailedPaths.Add(StreamFailureMarker(sink.DestinationType));
            evidence.ErrorDetails.Add(ex.Message);
            return false;
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
    //
    // Returns true if cancellation stopped this loop before every pending rename was processed.
    // Files already renamed before that point stay committed — no rollback; any rename still
    // pending when cancellation was observed has its staged temp file removed instead of renamed.
    private bool CommitPendingRenames(
        List<(string TempPath, string TargetPath)> pendingRenames,
        SinkDistributionEvidence evidence,
        CancellationToken cancellationToken)
    {
        for (int i = 0; i < pendingRenames.Count; i++)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                DeletePendingTemps(pendingRenames.Skip(i));
                return true;
            }

            (string tempPath, string targetPath) = pendingRenames[i];
            try
            {
                _fileSystem.RenameTempToTarget(tempPath, targetPath);
                evidence.CommittedPaths.Add(targetPath);
            }
            catch (Exception ex)
            {
                evidence.FailedPaths.Add(targetPath);
                evidence.ErrorDetails.Add(ex.Message);
                DeleteTempFileBestEffort(tempPath);
            }
        }

        return false;
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

    // Bundles the six accumulator lists DistributeToSinks builds up together (they are always
    // passed as one group to BuildRouteResult) so that method stays under the parameter-count
    // limit rather than taking each list as its own parameter. Mutated in place by StageFileSink/
    // WriteStreamSink/CommitPendingRenames — the record wraps List<T> references, so passing one
    // instance around lets every caller append to the same underlying lists.
    private readonly record struct SinkDistributionEvidence(
        List<string> FailedPaths,
        List<string> CommittedPaths,
        List<string> StagedPaths,
        List<string> ErrorDetails,
        List<string> DeliveredStreamPaths,
        List<string> RenderedFormats)
    {
        public static SinkDistributionEvidence Empty() => new(new(), new(), new(), new(), new(), new());

        public void RecordRenderedFormat(string format)
        {
            if (!RenderedFormats.Contains(format, StringComparer.Ordinal))
            {
                RenderedFormats.Add(format);
            }
        }
    }

    private static RouteResult BuildRouteResult(
        IReadOnlyList<ReportSink> additionalSinks,
        SinkDistributionEvidence evidence,
        bool cancelled = false)
    {
        if (evidence.FailedPaths.Count == 0 && !cancelled)
        {
            return new RouteResult(
                ReportRouteStatus.AllSucceeded, Array.Empty<string>(), evidence.CommittedPaths, evidence.StagedPaths,
                Array.Empty<string>(), Array.Empty<string>(), evidence.DeliveredStreamPaths)
            {
                RenderedFormats = evidence.RenderedFormats.ToArray(),
            };
        }

        // Every configured File sink is required regardless of whether content happened to be
        // rendered for it yet — in particular, a cancellation observed before rendering starts
        // (see RouteOutcomes/StageAllFileSinks) must still list every configured file destination
        // as uncommitted, not silently drop it because contentByFormat was empty at that point.
        HashSet<string> completedPaths = evidence.CommittedPaths
            .Concat(evidence.DeliveredStreamPaths)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var uncommittedPaths = additionalSinks
            .Select(SinkPath)
            .Where(path => !completedPaths.Contains(path))
            .ToArray();

        ReportRouteStatus status = evidence.CommittedPaths.Count > 0 || evidence.DeliveredStreamPaths.Count > 0
            ? ReportRouteStatus.PartialOutput
            : ReportRouteStatus.OutputFailed;

        return new RouteResult(
            status, evidence.FailedPaths, evidence.CommittedPaths, evidence.StagedPaths, uncommittedPaths,
            evidence.ErrorDetails, evidence.DeliveredStreamPaths)
        {
            Cancelled = cancelled,
            RenderedFormats = evidence.RenderedFormats.ToArray(),
        };
    }

    private static string SinkPath(ReportSink sink)
    {
        return sink.DestinationType == ReportDestinationType.File
            ? sink.FilePath!
            : StreamFailureMarker(sink.DestinationType);
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

    // cancellationToken defaults to None (not threaded from RenderReportContent, which
    // deliberately renders unconditionally — see that method's own comment) so the one caller
    // that must always complete a render regardless of the real cancellation state keeps doing
    // so; every other caller passes the live token through from RouteOutcomes.
    private string FormatSingleHuman(ValidationOutcome outcome, CancellationToken cancellationToken = default)
    {
        var sb = new StringBuilder();
        AppendHumanSection(sb, outcome, cancellationToken);
        return StripAnsi(sb.ToString().TrimEnd());
    }

    private string FormatCombinedHuman(
        IReadOnlyList<(string Mode, ValidationOutcome Outcome)> outcomesByMode, CancellationToken cancellationToken = default)
    {
        var sb = new StringBuilder();
        bool first = true;
        foreach ((string mode, ValidationOutcome outcome) in outcomesByMode)
        {
            // Checked per mode — a combined strict+audit render can now be interrupted between
            // modes instead of only before the whole multi-mode document starts.
            cancellationToken.ThrowIfCancellationRequested();

            if (!first)
            {
                sb.AppendLine();
            }
            first = false;
            if (outcomesByMode.Count > 1)
            {
                sb.AppendLine($"=== Mode: {mode} ===");
            }
            AppendHumanSection(sb, outcome, cancellationToken);
        }
        return StripAnsi(sb.ToString().TrimEnd());
    }

    internal static string StripAnsi(string content) => AnsiEscapeSequenceStripper.Strip(content);

    // Checked between each section — a human report bundles up to six independently-sized
    // sections (violations, cycles, policy consistency, unmatched ignores, coverage, coverage
    // summary, classification facts); a token cancelled partway through no longer has to wait
    // for every remaining section to render before it is observed.
    private void AppendHumanSection(StringBuilder sb, ValidationOutcome outcome, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

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
                sb.AppendLine(_runtime.FormatViolationsForHumans(outcome.Violations, cancellationToken));
            }

            cancellationToken.ThrowIfCancellationRequested();

            if (outcome.Cycles.Count > 0)
            {
                sb.AppendLine(_runtime.FormatCyclesForHumans(outcome.Cycles, outcome.CycleFindings));
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
        AppendSection(sb, outcome.PolicyConsistencyConfig != "off" && outcome.PolicyConsistencyFindings.Count > 0,
            () => _runtime.FormatPolicyConsistencyForHumans(outcome.PolicyConsistencyFindings));
        cancellationToken.ThrowIfCancellationRequested();
        AppendSection(sb, outcome.UnmatchedIgnoredViolations.Count > 0 && outcome.UnmatchedIgnoredViolationsConfig != "off",
            () => _runtime.FormatUnmatchedForHumans(outcome.UnmatchedIgnoredViolations));
        cancellationToken.ThrowIfCancellationRequested();
        AppendSection(sb, outcome.CoverageConfig != "off" && outcome.CoverageFindings.Count > 0,
            () => _runtime.FormatCoverageForHumans(outcome.CoverageFindings, cancellationToken));
        cancellationToken.ThrowIfCancellationRequested();
        AppendSection(sb, outcome.CoverageSummaries.Count > 0,
            () => _runtime.FormatCoverageSummaryForHumans(outcome.CoverageSummaries));
        cancellationToken.ThrowIfCancellationRequested();
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

    private string FormatSingleJson(string mode, ValidationOutcome outcome, CancellationToken cancellationToken = default)
    {
        return FormatJsonContent(mode, outcome, cancellationToken);
    }

    private string FormatCombinedJson(
        IReadOnlyList<(string Mode, ValidationOutcome Outcome)> outcomesByMode, CancellationToken cancellationToken = default)
    {
        JsonArray results = new();
        foreach ((string mode, ValidationOutcome outcome) in outcomesByMode)
        {
            // Checked per mode — a combined strict+audit document stops adding further modes'
            // results once cancellation is observed, instead of only checking before the whole
            // multi-mode document starts. FormatJsonContent below additionally checks per finding
            // within a single mode's own violations list.
            cancellationToken.ThrowIfCancellationRequested();
            results.Add(JsonNode.Parse(FormatJsonContent(mode, outcome, cancellationToken)));
        }

        return new JsonObject { ["results"] = results }.ToJsonString();
    }

    private string FormatSingleSarif(string mode, ValidationOutcome outcome, CancellationToken cancellationToken = default)
    {
        return FormatSarifContent(mode, outcome, cancellationToken);
    }

    private string FormatCombinedSarif(
        IReadOnlyList<(string Mode, ValidationOutcome Outcome)> outcomesByMode, CancellationToken cancellationToken = default)
    {
        JsonArray runs = new();
        foreach ((string mode, ValidationOutcome outcome) in outcomesByMode)
        {
            cancellationToken.ThrowIfCancellationRequested();
            JsonNode? document = JsonNode.Parse(FormatSarifContent(mode, outcome, cancellationToken));
            foreach (JsonNode? run in document?["runs"]?.AsArray() ?? new JsonArray())
            {
                runs.Add(run?.DeepClone());
            }
        }

        return new JsonObject { ["version"] = "2.1.0", ["runs"] = runs }.ToJsonString();
    }

    // cancellationToken defaults to None so RenderReportContent (which must always complete a
    // render regardless of the real cancellation state — see its own comment) keeps working
    // unchanged; every other caller passes the live token through, checked per violation inside
    // the widest FormatResultForCiArtifacts overload — the dominant contributor to a large
    // report's size, not just before/after this call.
    private string FormatJsonContent(string mode, ValidationOutcome outcome, CancellationToken cancellationToken = default)
    {
        return _runtime.FormatResultForCiArtifacts(
            mode, outcome.Passed, outcome.Violations, outcome.Cycles, outcome.CycleFindings, outcome.CoverageFindings,
            outcome.UnmatchedIgnoredViolations,
            outcome.PolicyConsistencyConfig == "off" ? Array.Empty<PolicyConsistencyDiagnostic>() : outcome.PolicyConsistencyFindings,
            outcome.CoverageSummaries, outcome.ClassificationConflicts, outcome.ClassificationMetadataFailures,
            outcome.ClassificationRoles, outcome.ClassificationPathDeferred, outcome.PreflightDiagnostics,
            outcome.SourceExpansion, outcome.SubtractiveMatcherParticipation, cancellationToken);
    }

    private string FormatSarifContent(string mode, ValidationOutcome outcome, CancellationToken cancellationToken = default)
    {
        return _runtime.FormatResultAsSarif(
            mode, outcome.Violations, outcome.Cycles, outcome.CycleFindings, outcome.PreflightDiagnostics,
            outcome.CoverageSummaries, outcome.SourceExpansion, outcome.SubtractiveMatcherParticipation, cancellationToken);
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
