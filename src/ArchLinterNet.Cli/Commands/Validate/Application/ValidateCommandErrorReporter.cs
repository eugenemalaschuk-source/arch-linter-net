using System.Text.Json;
using ArchLinterNet.Cli.Abstractions;
using ArchLinterNet.Cli.Commands;
using ArchLinterNet.Core.BuildState;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;
using ArchLinterNet.Core.Validation;

namespace ArchLinterNet.Cli.Commands.Validate.Application;

// Owns all user-facing error projections and post-outcome routing. It never evaluates an
// outcome; the execution coordinator supplies the already-computed report facts.
internal sealed class ValidateCommandErrorReporter
{
    private readonly ICliConsole _console;
    private readonly ReportCoordinator _coordinator;
    private readonly CancellationToken _cancellationToken;

    public ValidateCommandErrorReporter(
        ICliConsole console, ReportCoordinator coordinator, CancellationToken cancellationToken)
    {
        _console = console;
        _coordinator = coordinator;
        _cancellationToken = cancellationToken;
    }

    internal void WriteOutputError(
        ValidateCommandOptions options,
        string format,
        RouteResult result,
        bool isSingleMode,
        IReadOnlyList<(string Mode, ValidationOutcome Outcome)> outcomesByMode)
    {
        string status = result.Status == ReportRouteStatus.PartialOutput ? "partial-output" : "output-failed";
        string humanMessage = FormatOutputError(status, result);

        Dictionary<string, string> contentByFormat = new();
        foreach (string neededFormat in NeededErrorFormats(options, format))
        {
            string reportContent = _coordinator.RenderReportContent(neededFormat, isSingleMode, outcomesByMode);
            contentByFormat[neededFormat] = neededFormat switch
            {
                ValidateCommandPreflight.FormatJson => ReportErrorContentFormatter.BuildOutputErrorJsonText(status, humanMessage, result, reportContent),
                ValidateCommandPreflight.FormatSarif => ReportErrorContentFormatter.BuildOutputErrorSarifText(status, humanMessage, result, reportContent),
                _ => ReportErrorContentFormatter.BuildOutputErrorHumanText(humanMessage, result, reportContent),
            };
        }

        WriteErrorContent(options, format, contentByFormat, allowFileSinks: false, priorOutputResult: result);
    }

    internal void WriteCancelledRouting(
        ValidateCommandOptions options,
        string format,
        RouteResult result,
        bool isSingleMode,
        IReadOnlyList<(string Mode, ValidationOutcome Outcome)> outcomesByMode)
    {
        const string Message = "Architecture validation was cancelled during report output.";
        Dictionary<string, string> contentByFormat = new();
        foreach (string neededFormat in NeededErrorFormats(options, format))
        {
            string reportContent = _coordinator.RenderReportContent(neededFormat, isSingleMode, outcomesByMode);
            contentByFormat[neededFormat] = neededFormat switch
            {
                ValidateCommandPreflight.FormatJson => ReportErrorContentFormatter.BuildCancelledOutputJsonText(Message, result, reportContent),
                ValidateCommandPreflight.FormatSarif => ReportErrorContentFormatter.BuildCancelledOutputSarifText(Message, result, reportContent),
                _ => ReportErrorContentFormatter.BuildCancelledOutputHumanText(Message, result, reportContent),
            };
        }

        WriteErrorContent(options, format, contentByFormat, allowFileSinks: false, priorOutputResult: result);
    }

    internal void WriteCancellation(
        ValidateCommandOptions options,
        string format,
        BuildStateProcessCleanupTimedOutException? cleanupTimeout = null)
    {
        string message = cleanupTimeout is null
            ? "Architecture validation was cancelled."
            : "Architecture validation was cancelled. Child build/restore process " +
                $"{cleanupTimeout.ProcessId} did not exit within {cleanupTimeout.TimeoutMs}ms after being killed " +
                "and may still be running.";

        Dictionary<string, string> contentByFormat = new();
        foreach (string neededFormat in NeededErrorFormats(options, format))
        {
            contentByFormat[neededFormat] = neededFormat switch
            {
                ValidateCommandPreflight.FormatJson => BuildCancellationJsonText(message, cleanupTimeout),
                ValidateCommandPreflight.FormatSarif => BuildCancellationSarifText(message),
                _ => message,
            };
        }

        // Cancellation reporting must not use a file sink or the cancelled operation token.
        WriteErrorContent(options, format, contentByFormat, allowFileSinks: false);
    }

    internal void WriteTimingReport(ValidationTiming timing)
    {
        timing.WriteReport(_console.Error);
    }

    internal void WritePolicyDiagnostic(
        ValidateCommandOptions options,
        string format,
        Exception exception,
        ArchitecturePolicyDiagnostic diagnostic)
    {
        string? category = (exception as ArchitecturePolicyLoadException)?.Category;
        string message = exception.Message;
        string? fileCollision = FindPolicyDiagnosticFileCollision(options, diagnostic);
        if (fileCollision is not null)
        {
            message = $"{message} ({fileCollision})";
        }

        Dictionary<string, string> contentByFormat = new();
        foreach (string neededFormat in NeededErrorFormats(options, format))
        {
            contentByFormat[neededFormat] = neededFormat switch
            {
                ValidateCommandPreflight.FormatJson => PolicyDiagnosticOutputWriter.BuildJsonText(message, diagnostic, category),
                ValidateCommandPreflight.FormatSarif => BuildPolicyDiagnosticSarifText(message, diagnostic, category),
                _ => PolicyDiagnosticOutputWriter.BuildHumanText("Architecture validation error", message, diagnostic),
            };
        }

        WriteErrorContent(options, format, contentByFormat, allowFileSinks: fileCollision is null);
    }

    internal void WriteExecutionError(ValidateCommandOptions options, string format, Exception exception)
    {
        string message = exception.Message;
        string? fileCollision = exception is ArchitectureAnalysisEvaluationException evaluationException
            ? ValidateCommandPreflight.FindImportFileCollision(options, evaluationException.PolicyImportPaths)
                ?? ValidateCommandPreflight.FindReceiptFileCollision(options, evaluationException.ResolvedAssemblyPaths)
                ?? ValidateCommandPreflight.FindDiscoveredProjectFileCollision(options, evaluationException.DiscoveredProjectPaths)
            : null;
        if (fileCollision is not null)
        {
            message = $"{message} ({fileCollision})";
        }

        Dictionary<string, string> contentByFormat = new();
        foreach (string neededFormat in NeededErrorFormats(options, format))
        {
            contentByFormat[neededFormat] = neededFormat switch
            {
                ValidateCommandPreflight.FormatJson => BuildExecutionErrorJsonText(message),
                ValidateCommandPreflight.FormatSarif => BuildExecutionErrorSarifText(message),
                _ => $"Architecture validation error: {message}",
            };
        }

        WriteErrorContent(options, format, contentByFormat, allowFileSinks: fileCollision is null);
    }

    private void WriteErrorContent(
        ValidateCommandOptions options,
        string errorFormat,
        Dictionary<string, string> contentByFormat,
        bool allowFileSinks,
        RouteResult? priorOutputResult = null)
    {
        if (options.AdditionalSinks.Count == 0)
        {
            string content = contentByFormat[errorFormat];
            if (errorFormat == ValidateCommandPreflight.FormatHuman)
            {
                _console.Error.WriteLine(content);
            }
            else
            {
                _console.Out.WriteLine(content);
            }

            return;
        }

        if (allowFileSinks)
        {
            RouteResult errorRouteResult = _coordinator.RouteErrorToAllSinks(
                options.AdditionalSinks, contentByFormat, _cancellationToken);
            if (errorRouteResult.Status != ReportRouteStatus.AllSucceeded
                && CanUseStderrFallback(errorRouteResult))
            {
                string fallbackFormat = ResolveStderrFallbackFormat(options, errorFormat, contentByFormat);
                WriteErrorRoutingFailureFallback(fallbackFormat, contentByFormat[fallbackFormat], errorRouteResult);
            }

            return;
        }

        if (priorOutputResult is null || CanUseStderrFallback(priorOutputResult.Value))
        {
            string fallbackFormat = ResolveStderrFallbackFormat(options, errorFormat, contentByFormat);
            TryWriteToStderr(contentByFormat[fallbackFormat]);
        }
    }

    private void WriteErrorRoutingFailureFallback(string format, string originalContent, RouteResult routeResult)
    {
        string status = routeResult.Status == ReportRouteStatus.PartialOutput ? "partial-output" : "output-failed";
        string message = FormatOutputError(status, routeResult);
        string fallbackContent = format switch
        {
            ValidateCommandPreflight.FormatJson => ReportErrorContentFormatter.BuildErrorRoutingFailureJsonText(status, originalContent, routeResult),
            ValidateCommandPreflight.FormatSarif => ReportErrorContentFormatter.BuildOutputErrorSarifText(status, message, routeResult, originalContent),
            _ => ReportErrorContentFormatter.BuildOutputErrorHumanText(message, routeResult, originalContent),
        };

        TryWriteToStderr(fallbackContent);
    }

    private void TryWriteToStderr(string content)
    {
        try
        {
            _console.Error.WriteLine(content);
        }
        catch (Exception)
        {
            // A closed stderr cannot be repaired by another write; preserve the validation exit code.
        }
    }

    private static IEnumerable<string> NeededErrorFormats(ValidateCommandOptions options, string errorFormat)
    {
        if (options.AdditionalSinks.Count == 0)
        {
            return new[] { errorFormat };
        }

        return options.AdditionalSinks.Select(sink => sink.Format).Distinct();
    }

    private static string ResolveStderrFallbackFormat(
        ValidateCommandOptions options,
        string defaultFormat,
        Dictionary<string, string> contentByFormat)
    {
        return options.AdditionalSinks
            .FirstOrDefault(sink => sink.DestinationType == ReportDestinationType.Stderr
                && contentByFormat.ContainsKey(sink.Format))
            ?.Format
            ?? defaultFormat;
    }

    private static bool CanUseStderrFallback(RouteResult result)
    {
        return !result.DeliveredStreamPaths.Contains("<stderr>", StringComparer.Ordinal);
    }

    private static string FormatOutputError(string status, RouteResult result)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append($"Report output failed ({status})");
        if (result.FailedPaths.Count > 0)
        {
            sb.Append($": failed={string.Join(", ", result.FailedPaths)}");
        }
        if (result.CommittedPaths.Count > 0)
        {
            sb.Append($", committed={string.Join(", ", result.CommittedPaths)}");
        }
        return sb.ToString();
    }

    private static string BuildExecutionErrorJsonText(string message)
    {
        return JsonSerializer.Serialize(new { kind = "architecture_execution_error", message });
    }

    private static string BuildExecutionErrorSarifText(string message)
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
                            ruleId = "architecture-execution",
                            message = new { text = message },
                            locations = Array.Empty<object>(),
                        },
                    },
                },
            },
        });
    }

    private static string BuildCancellationJsonText(
        string message, BuildStateProcessCleanupTimedOutException? cleanupTimeout)
    {
        return JsonSerializer.Serialize(new
        {
            kind = "architecture_cancelled",
            status = "cancelled",
            message,
            processCleanupTimedOut = cleanupTimeout is not null,
            processId = cleanupTimeout?.ProcessId,
            processCleanupTimeoutMs = cleanupTimeout?.TimeoutMs,
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

    private static string? FindPolicyDiagnosticFileCollision(
        ValidateCommandOptions options, ArchitecturePolicyDiagnostic diagnostic)
    {
        HashSet<string> involvedPaths = new(StringComparer.OrdinalIgnoreCase);
        if (diagnostic.Location is not null)
        {
            involvedPaths.Add(Path.GetFullPath(diagnostic.Location.SourcePath));
        }

        foreach (ArchitecturePolicySourceLocation related in diagnostic.RelatedLocations)
        {
            involvedPaths.Add(Path.GetFullPath(related.SourcePath));
        }

        foreach (string importPath in diagnostic.ImportChain)
        {
            involvedPaths.Add(Path.GetFullPath(importPath));
        }

        foreach (ReportSink sink in options.AdditionalSinks)
        {
            if (sink.DestinationType != ReportDestinationType.File || sink.FilePath is null)
            {
                continue;
            }

            if (involvedPaths.Contains(Path.GetFullPath(sink.FilePath)))
            {
                return $"--report destination '{sink.FilePath}' matches a policy file involved in this import failure";
            }
        }

        return null;
    }

    private static string BuildPolicyDiagnosticSarifText(
        string message, ArchitecturePolicyDiagnostic diagnostic, string? category)
    {
        object[] relatedLocations = ArchitectureSarifFormatter.FormatPolicyLocationsForSarif(
            diagnostic.Location, diagnostic.RelatedLocations);

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
                            ruleId = "architecture-policy",
                            message = new { text = message },
                            properties = new { error_category = category, import_chain = diagnostic.ImportChain },
                            locations = diagnostic.Location is null ? Array.Empty<object>() : new object[]
                            {
                                new
                                {
                                    physicalLocation = new
                                    {
                                        artifactLocation = new { uri = diagnostic.Location.SourcePath },
                                        region = new { startLine = diagnostic.Location.Line, startColumn = diagnostic.Location.Column },
                                    },
                                },
                            },
                            relatedLocations,
                        },
                    },
                },
            },
        });
    }
}
