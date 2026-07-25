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
    IReadOnlyList<string> ErrorDetails);

internal sealed class ReportCoordinator
{
    private const int MaxReportBytes = 100 * 1024 * 1024;

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
        string? neededHuman = StdoutOrAnySinkNeeds("human", stdoutFormat, additionalSinks, isReportMode);
        string? neededJson = StdoutOrAnySinkNeeds("json", stdoutFormat, additionalSinks, isReportMode);
        string? neededSarif = StdoutOrAnySinkNeeds("sarif", stdoutFormat, additionalSinks, isReportMode);

        string? humanContent = null;
        string? jsonContent = null;
        string? sarifContent = null;

        if (neededHuman is not null)
        {
            humanContent = isSingleMode
                ? FormatSingleHuman(outcomesByMode[0].Mode, outcomesByMode[0].Outcome)
                : FormatCombinedHuman(outcomesByMode);
        }

        if (neededJson is not null)
        {
            jsonContent = isSingleMode
                ? FormatSingleJson(outcomesByMode[0].Mode, outcomesByMode[0].Outcome)
                : FormatCombinedJson(outcomesByMode);
        }

        if (neededSarif is not null)
        {
            sarifContent = isSingleMode
                ? FormatSingleSarif(outcomesByMode[0].Mode, outcomesByMode[0].Outcome)
                : FormatCombinedSarif(outcomesByMode);
        }

        if (!isReportMode)
        {
            _console.Out.WriteLine(DispatchFormat(stdoutFormat, humanContent, jsonContent, sarifContent));
        }

        List<string> failedPaths = new();
        List<string> committedPaths = new();
        List<string> errorDetails = new();
        List<(string TempPath, string TargetPath)> pendingRenames = new();

        foreach (ReportSink sink in additionalSinks)
        {
            if (sink.DestinationType is ReportDestinationType.Stdout or ReportDestinationType.Stderr)
            {
                string content = sink.Format switch
                {
                    "human" => humanContent!,
                    "json" => jsonContent!,
                    "sarif" => sarifContent!,
                    _ => string.Empty,
                };

                if (sink.DestinationType == ReportDestinationType.Stderr)
                {
                    _console.Error.WriteLine(content);
                }
                else
                {
                    _console.Out.WriteLine(content);
                }

                continue;
            }

            string fileContent = sink.Format switch
            {
                "human" => humanContent!,
                "json" => jsonContent!,
                "sarif" => sarifContent!,
                _ => string.Empty,
            };

            try
            {
                ValidateBoundedOutput(sink.Format, fileContent);
                _fileSystem.WriteAllTextToTemp(sink.FilePath!, fileContent);
                string tempPath = _fileSystem.ResolveTempPath(sink.FilePath!);
                pendingRenames.Add((tempPath, sink.FilePath!));
            }
            catch (Exception ex)
            {
                failedPaths.Add(sink.FilePath!);
                errorDetails.Add(ex.Message);
            }
        }

        bool phase1AllSucceeded = failedPaths.Count == 0;

        if (phase1AllSucceeded)
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
                    try { _fileSystem.DeleteFile(tempPath); } catch { }
                }
            }
        }
        else
        {
            foreach ((string tempPath, string _) in pendingRenames)
            {
                try { _fileSystem.DeleteFile(tempPath); } catch { }
            }
        }

        if (failedPaths.Count == 0)
        {
            return new RouteResult(ReportRouteStatus.AllSucceeded, Array.Empty<string>(), committedPaths, Array.Empty<string>());
        }

        ReportRouteStatus status = committedPaths.Count > 0
            ? ReportRouteStatus.PartialOutput
            : ReportRouteStatus.OutputFailed;

        return new RouteResult(status, failedPaths, committedPaths, errorDetails);
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

    private static void ValidateBoundedOutput(string format, string content)
    {
        if (content.Length > MaxReportBytes)
        {
            throw new InvalidOperationException(
                $"Report content exceeds maximum size of {MaxReportBytes} bytes.");
        }

        if (format is "json" or "sarif")
        {
            _ = JsonNode.Parse(content);
        }
    }

    private string FormatSingleHuman(string mode, ValidationOutcome outcome)
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
        return FormatJson(mode, outcome);
    }

    private string FormatCombinedJson(IReadOnlyList<(string Mode, ValidationOutcome Outcome)> outcomesByMode)
    {
        JsonArray results = new();
        foreach ((string mode, ValidationOutcome outcome) in outcomesByMode)
        {
            results.Add(JsonNode.Parse(FormatJson(mode, outcome)));
        }

        return new JsonObject { ["results"] = results }.ToJsonString();
    }

    private string FormatSingleSarif(string mode, ValidationOutcome outcome)
    {
        return FormatSarif(mode, outcome);
    }

    private string FormatCombinedSarif(IReadOnlyList<(string Mode, ValidationOutcome Outcome)> outcomesByMode)
    {
        JsonArray runs = new();
        foreach ((string mode, ValidationOutcome outcome) in outcomesByMode)
        {
            JsonNode? document = JsonNode.Parse(FormatSarif(mode, outcome));
            foreach (JsonNode? run in document?["runs"]?.AsArray() ?? new JsonArray())
            {
                runs.Add(run?.DeepClone());
            }
        }

        return new JsonObject { ["version"] = "2.1.0", ["runs"] = runs }.ToJsonString();
    }

    private string FormatJson(string mode, ValidationOutcome outcome)
    {
        return _runtime.FormatResultForCiArtifacts(
            mode, outcome.Passed, outcome.Violations, outcome.Cycles, outcome.CycleFindings, outcome.CoverageFindings,
            outcome.UnmatchedIgnoredViolations,
            outcome.PolicyConsistencyConfig == "off" ? Array.Empty<PolicyConsistencyDiagnostic>() : outcome.PolicyConsistencyFindings,
            outcome.CoverageSummaries, outcome.ClassificationConflicts, outcome.ClassificationMetadataFailures,
            outcome.ClassificationRoles, outcome.ClassificationPathDeferred, outcome.PreflightDiagnostics);
    }

    private string FormatSarif(string mode, ValidationOutcome outcome)
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
            "human" => humanContent ?? string.Empty,
            "json" => jsonContent ?? string.Empty,
            "sarif" => sarifContent ?? string.Empty,
            _ => string.Empty,
        };
    }
}
