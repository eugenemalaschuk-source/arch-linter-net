using System.Text.Json;
using ArchLinterNet.Cli.Abstractions;
using ArchLinterNet.Cli.Commands;
using ArchLinterNet.Core.BuildState;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;
using ArchLinterNet.Core.Validation;

namespace ArchLinterNet.Cli.Commands.Validate;

internal sealed partial class ValidateCommandHandler
{
    private const string FormatHuman = "human";
    private const string FormatJson = "json";
    private const string FormatSarif = "sarif";

    private readonly ICliRuntime _runtime;
    private readonly ICliConsole _console;
    private readonly IFileSystem _fileSystem;
    private readonly ReportCoordinator _coordinator;
    private readonly CancellationToken _cancellationToken;

    public ValidateCommandHandler(
        ICliRuntime runtime, ICliConsole console, IFileSystem fileSystem, CancellationToken cancellationToken = default)
    {
        _runtime = runtime;
        _console = console;
        _fileSystem = fileSystem;
        _coordinator = new ReportCoordinator(runtime, console, fileSystem);
        _cancellationToken = cancellationToken;
    }
    public int Execute(ValidateCommandOptions options)
    {
        int? immediateResult = TryWriteImmediateResponse(options);
        if (immediateResult is not null)
        {
            return immediateResult.Value;
        }

        string errorFormat = ResolveEffectiveFormat(options);
        ValidationProfileExecutionState profileState = new();

        try
        {
            return ExecuteValidation(options, errorFormat, profileState);
        }
        // Must precede the general OperationCanceledException catch below (it is a subtype) — a
        // killed build/restore process that never confirmed exit carries evidence (which process,
        // what deadline) that a generic "cancelled" message would silently discard.
        catch (BuildStateProcessCleanupTimedOutException ex)
        {
            WriteCancelledProfile(options, profileState);
            WriteCancellation(options, errorFormat, ex);
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }
        catch (OperationCanceledException)
        {
            WriteCancelledProfile(options, profileState);
            WriteCancellation(options, errorFormat);
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }
        catch (Exception ex) when (TryGetPolicyDiagnostic(ex, out ArchitecturePolicyDiagnostic? diagnostic))
        {
            WritePolicyDiagnostic(options, errorFormat, ex, diagnostic!);
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }
        catch (Exception ex)
        {
            WriteExecutionError(options, errorFormat, ex);
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }
    }

    private static string ResolveEffectiveFormat(ValidateCommandOptions options)
    {
        if (options.IsFormatExplicit || options.AdditionalSinks.Count == 0)
        {
            return options.Format;
        }

        ReportSink? stdoutSink = options.AdditionalSinks
            .FirstOrDefault(sink => sink.DestinationType == ReportDestinationType.Stdout);
        if (stdoutSink is not null)
        {
            return stdoutSink.Format;
        }

        ReportSink? structuredSink = options.AdditionalSinks
            .FirstOrDefault(sink => sink.Format is FormatJson or FormatSarif);
        return structuredSink?.Format ?? FormatHuman;
    }

    // Post-outcome failure: some sinks may have already committed a legitimate report. Only
    // stdout/stderr sinks are eligible destinations for this diagnostic — a File sink either just
    // failed (writing here would only fail again) or already committed valid report content that
    // this must not overwrite. See WriteErrorContent's IsStreamOccupied fallback policy.
    //
    // Embeds the full, already-computed report (via ReportCoordinator.RenderReportContent) rather
    // than a bare pass/fail summary, so an output-routing failure never reduces what reaches the
    // user to less than the complete normalized findings.
    private void WriteOutputError(
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
                FormatJson => ReportErrorContentFormatter.BuildOutputErrorJsonText(status, humanMessage, result, reportContent),
                FormatSarif => ReportErrorContentFormatter.BuildOutputErrorSarifText(status, humanMessage, result, reportContent),
                _ => ReportErrorContentFormatter.BuildOutputErrorHumanText(humanMessage, result, reportContent),
            };
        }

        WriteErrorContent(options, format, contentByFormat, allowFileSinks: false, priorOutputResult: result);
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

    // Catches every error that isn't a structured ArchitecturePolicyDiagnostic — including an
    // expression evaluation failure thrown deep inside contract checking (e.g.
    // ArchitectureExpressionFactService.Evaluate for a `when` predicate), which happens well after
    // policy load succeeds and so is never wrapped with policy-location provenance. Without this,
    // a --format json/sarif run would receive an unstructured stderr line instead of the format it
    // asked for; this emits the same "unexpected error" shape on stdout that format expects, with no
    // location (none is available at this point) rather than silently degrading to plain text.
    // Occurs before any legitimate report content exists for this invocation, so — unlike
    // WriteOutputError — routing into a file sink is safe in general. The exception may still
    // name files this invocation actually consumed (see the ArchitectureAnalysisEvaluationException
    // check below), which needs the same protection a policy-load failure's own diagnostic gets.
    private void WriteExecutionError(ValidateCommandOptions options, string format, Exception exception)
    {
        string message = exception.Message;

        // A generic execution failure (contract execution, expression evaluation) can happen after
        // policy and assembly loading already succeeded. ArchitectureAnalysisEvaluationException
        // carries whatever provenance was already known at that point — protect those files from
        // being overwritten by this error document the same way a policy-load failure's own
        // diagnostic protects its inputs (see FindPolicyDiagnosticFileCollision).
        string? fileCollision = exception is ArchitectureAnalysisEvaluationException evaluationException
            ? FindImportFileCollision(options, evaluationException.PolicyImportPaths)
                ?? FindReceiptFileCollision(options, evaluationException.ResolvedAssemblyPaths)
                ?? FindDiscoveredProjectFileCollision(options, evaluationException.DiscoveredProjectPaths)
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
                FormatJson => BuildExecutionErrorJsonText(message),
                FormatSarif => BuildExecutionErrorSarifText(message),
                _ => $"Architecture validation error: {message}",
            };
        }

        WriteErrorContent(options, format, contentByFormat, allowFileSinks: fileCollision is null);
    }

    private static string BuildExecutionErrorJsonText(string message)
    {
        return JsonSerializer.Serialize(new
        {
            kind = "architecture_execution_error",
            message,
        });
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

    // Formats needed to satisfy every configured destination: in report mode, one per distinct
    // sink format (a sink may target a file, stdout, or stderr); in legacy mode, just errorFormat.
    private static IEnumerable<string> NeededErrorFormats(ValidateCommandOptions options, string errorFormat)
    {
        if (options.AdditionalSinks.Count == 0)
        {
            return new[] { errorFormat };
        }

        return options.AdditionalSinks.Select(sink => sink.Format).Distinct();
    }

    // Legacy (no --report): identical to pre-#364 behavior — json/sarif to stdout, human to
    // stderr. Report mode: route through the sinks (files allowed only for pre-outcome errors,
    // per allowFileSinks), falling back to stderr when no sink can safely carry the diagnostic.
    private void WriteErrorContent(
        ValidateCommandOptions options,
        string errorFormat,
        IReadOnlyDictionary<string, string> contentByFormat,
        bool allowFileSinks,
        RouteResult? priorOutputResult = null)
    {
        if (options.AdditionalSinks.Count == 0)
        {
            string content = contentByFormat[errorFormat];
            if (errorFormat == FormatHuman)
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

        // Post-outcome stream sinks already carry the complete report. Do not add a second
        // document to a stream that successfully received it. A configured stderr sink that
        // itself failed is different: no document reached that channel, so it remains a valid
        // fallback target for the routing diagnostic.
        if (priorOutputResult is null)
        {
            // A collision prevented error routing before any configured stream was written, so
            // stderr is idle even when a --report file sink was requested.
            string fallbackFormat = ResolveStderrFallbackFormat(options, errorFormat, contentByFormat);
            TryWriteToStderr(contentByFormat[fallbackFormat]);
        }
        else if (CanUseStderrFallback(priorOutputResult.Value))
        {
            string fallbackFormat = ResolveStderrFallbackFormat(options, errorFormat, contentByFormat);
            TryWriteToStderr(contentByFormat[fallbackFormat]);
        }
    }

    private static string ResolveStderrFallbackFormat(
        ValidateCommandOptions options,
        string defaultFormat,
        IReadOnlyDictionary<string, string> contentByFormat)
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

    // A failed file error sink must not turn a machine-readable stderr fallback into two
    // concatenated documents. Reformat its already-built error document as one JSON/SARIF/human
    // document that also carries the routing failure evidence.
    private void WriteErrorRoutingFailureFallback(string format, string originalContent, RouteResult routeResult)
    {
        string status = routeResult.Status == ReportRouteStatus.PartialOutput ? "partial-output" : "output-failed";
        string message = FormatOutputError(status, routeResult);
        string fallbackContent = format switch
        {
            FormatJson => ReportErrorContentFormatter.BuildErrorRoutingFailureJsonText(status, originalContent, routeResult),
            FormatSarif => ReportErrorContentFormatter.BuildOutputErrorSarifText(status, message, routeResult, originalContent),
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
            // A closed stderr cannot be repaired by another write. The coordinator already
            // recorded the original stream failure; avoid masking the validation exit code.
        }
    }

    private int? TryWriteImmediateResponse(ValidateCommandOptions options)
    {
        if (options.ShowHelp)
        {
            _console.Out.WriteLine(ValidateCommandDefinition.HelpText);
            return CliExitCodes.Success;
        }

        if (options.ShowVersion)
        {
            _console.Out.WriteLine($"arch-linter-net {_runtime.Version}");
            return CliExitCodes.Success;
        }

        if (!TryParseModes(options.Mode, out _, out string? modeError))
        {
            _console.Error.WriteLine(modeError);
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }

        if (options.Format is not (FormatHuman or FormatJson or FormatSarif))
        {
            _console.Error.WriteLine($"Invalid format: {options.Format}. Use 'human', 'json', or 'sarif'.");
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }

        if (options.ReportParseError is not null)
        {
            _console.Error.WriteLine(options.ReportParseError);
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }

        if (options.IsFormatExplicit && options.AdditionalSinks.Count > 0)
        {
            _console.Error.WriteLine(
                "--format/--json cannot be combined with --report. " +
                "Use --report <format>=stdout to route output to stdout.");
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }

        string? reportCollision = FindReportFileCollision(options);
        if (reportCollision is not null)
        {
            _console.Error.WriteLine(reportCollision);
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }

        string? profileCollision = FindProfileFileCollision(options);
        if (profileCollision is not null)
        {
            _console.Error.WriteLine(profileCollision);
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }

        if (!PreValidateReportDestinations(options))
        {
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }

        return null;
    }

    // "strict,audit" (or any comma-separated combination) is parsed here, ahead of dispatch, so a
    // single requested mode keeps its exact original single-call path below and only a genuinely
    // combined request pays for building an ArchitectureAnalysisSnapshot.
    private static string? FindReportFileCollision(ValidateCommandOptions options)
    {
        HashSet<string> inputFiles = new(StringComparer.OrdinalIgnoreCase)
        {
            Path.GetFullPath(options.PolicyPath),
        };

        if (options.BaselinePath is not null)
        {
            inputFiles.Add(Path.GetFullPath(options.BaselinePath));
        }

        foreach (ReportSink sink in options.AdditionalSinks)
        {
            if (sink.DestinationType != ReportDestinationType.File || sink.FilePath is null)
            {
                continue;
            }

            if (inputFiles.Contains(Path.GetFullPath(sink.FilePath)))
            {
                return $"--report destination '{sink.FilePath}' matches an input file";
            }
        }

        return null;
    }

    // --profile writes directly, so it needs the same input protection as --report plus an
    // explicit cross-output collision check. Without this, a profile can overwrite the policy
    // that was just read or replace a report that this invocation has already committed.
    private static string? FindProfileFileCollision(ValidateCommandOptions options)
    {
        if (!TryGetProfileFilePath(options, out string? profilePath))
        {
            return null;
        }

        HashSet<string> inputFiles = new(StringComparer.OrdinalIgnoreCase)
        {
            Path.GetFullPath(options.PolicyPath),
        };
        if (options.BaselinePath is not null)
        {
            inputFiles.Add(Path.GetFullPath(options.BaselinePath));
        }

        if (inputFiles.Contains(profilePath!))
        {
            return $"--profile destination '{options.ProfileDestination}' matches an input file";
        }

        foreach (ReportSink sink in options.AdditionalSinks)
        {
            if (sink.DestinationType == ReportDestinationType.File
                && sink.FilePath is not null
                && string.Equals(profilePath, Path.GetFullPath(sink.FilePath), StringComparison.OrdinalIgnoreCase))
            {
                return $"--profile destination '{options.ProfileDestination}' matches --report destination '{sink.FilePath}'";
            }
        }

        return null;
    }

    private static string? FindProfileFileCollision(
        ValidateCommandOptions options, IEnumerable<string> inputPaths, string inputDescription)
    {
        if (!TryGetProfileFilePath(options, out string? profilePath))
        {
            return null;
        }

        string? matchedPath = inputPaths.FirstOrDefault(inputPath =>
            string.Equals(profilePath, Path.GetFullPath(inputPath), StringComparison.OrdinalIgnoreCase));
        return matchedPath is null
            ? null
            : $"--profile destination '{options.ProfileDestination}' matches {inputDescription} '{matchedPath}'";
    }

    private static bool TryGetProfileFilePath(ValidateCommandOptions options, out string? profilePath)
    {
        if (options.ProfileDestination is null
            || options.ProfileDestination is ProfileDestinationStdout or ProfileDestinationStderr)
        {
            profilePath = null;
            return false;
        }

        profilePath = Path.GetFullPath(options.ProfileDestination);
        return true;
    }

    private static string? FindImportFileCollision(ValidateCommandOptions options, IReadOnlyList<string> policyImportPaths)
    {
        foreach (ReportSink sink in options.AdditionalSinks)
        {
            if (sink.DestinationType != ReportDestinationType.File || sink.FilePath is null)
            {
                continue;
            }

            string sinkFullPath = Path.GetFullPath(sink.FilePath);
            string? matchedImportPath = policyImportPaths
                .FirstOrDefault(importPath => string.Equals(sinkFullPath, importPath, StringComparison.OrdinalIgnoreCase));
            if (matchedImportPath is not null)
            {
                return $"--report destination '{sink.FilePath}' matches imported policy file '{matchedImportPath}'";
            }
        }

        return null;
    }

    // Guards the exact set of build receipts and assemblies this run actually loaded, not every
    // path that happens to end in the receipt suffix — a --report destination that merely *looks*
    // like a receipt path (but was never read as one) is a legitimate user choice, and one that IS
    // a real input must be protected regardless of what its name looks like. Sourced from
    // outcome.ResolvedAssemblyPaths rather than PreflightDiagnostics: preflight is skipped entirely
    // when project discovery finds no projects (see
    // ArchitectureValidationApplicationService.RunBuildStatePreflight), but analysis.target_assemblies
    // configured directly can still resolve assemblies in that case — ResolvedAssemblyPaths is the
    // complete inventory regardless of whether a project-based preflight diagnostic exists for it.
    private static string? FindReceiptFileCollision(
        ValidateCommandOptions options,
        IReadOnlyList<string> resolvedAssemblyPaths)
    {
        HashSet<string> loadedPaths = new(StringComparer.OrdinalIgnoreCase);
        foreach (string assemblyPath in resolvedAssemblyPaths)
        {
            loadedPaths.Add(Path.GetFullPath(assemblyPath));
            loadedPaths.Add(Path.GetFullPath(BuildReceiptStore.ReceiptPathFor(assemblyPath)));
        }

        foreach (ReportSink sink in options.AdditionalSinks)
        {
            if (sink.DestinationType != ReportDestinationType.File || sink.FilePath is null)
            {
                continue;
            }

            if (loadedPaths.Contains(Path.GetFullPath(sink.FilePath)))
            {
                return $"--report destination '{sink.FilePath}' matches a build artifact or receipt loaded during this run";
            }
        }

        return null;
    }

    private static string? FindDiscoveredProjectFileCollision(
        ValidateCommandOptions options,
        IReadOnlyList<string> discoveredProjectPaths)
    {
        HashSet<string> loadedProjectPaths = new(
            discoveredProjectPaths.Select(Path.GetFullPath), StringComparer.OrdinalIgnoreCase);

        foreach (ReportSink sink in options.AdditionalSinks)
        {
            if (sink.DestinationType == ReportDestinationType.File
                && sink.FilePath is not null
                && loadedProjectPaths.Contains(Path.GetFullPath(sink.FilePath)))
            {
                return $"--report destination '{sink.FilePath}' matches a project file loaded during this run";
            }
        }

        return null;
    }

    private bool PreValidateReportDestinations(ValidateCommandOptions options)
    {
        foreach (ReportSink sink in options.AdditionalSinks)
        {
            if (sink.DestinationType != ReportDestinationType.File || sink.FilePath is null)
            {
                continue;
            }

            if (!_fileSystem.CanWriteToDirectory(sink.FilePath))
            {
                _console.Error.WriteLine($"Cannot write report to '{sink.FilePath}': destination is not writable");
                return false;
            }
        }

        return true;
    }

    private static bool TryGetPolicyDiagnostic(Exception exception, out ArchitecturePolicyDiagnostic? diagnostic)
    {
        diagnostic = exception switch
        {
            ArchitecturePolicyLoadException loadException => loadException.Diagnostic,
            ArchitecturePolicyValidationException validationException => validationException.Diagnostic,
            _ => null,
        };
        return diagnostic is not null;
    }

    // Occurs before any legitimate report content exists for this invocation (policy load never
    // reached outcome production), so — unlike WriteOutputError — it is safe to route into every
    // configured sink, including files.
    private void WritePolicyDiagnostic(
        ValidateCommandOptions options, string format, Exception exception, ArchitecturePolicyDiagnostic diagnostic)
    {
        string? category = (exception as ArchitecturePolicyLoadException)?.Category;
        string message = exception.Message;

        // A policy-import failure names the very file(s) it failed to load in its own
        // diagnostic — a --report file sink pointed at one of those paths would otherwise get
        // this error document written over the fragment that just failed to import. Routing to
        // file sinks is safe for pre-outcome errors in general (nothing legitimate has been
        // produced yet for this invocation to clobber), but not when the destination IS one of
        // the inputs that just failed.
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
                FormatJson => PolicyDiagnosticOutputWriter.BuildJsonText(message, diagnostic, category),
                FormatSarif => BuildPolicyDiagnosticSarifText(message, diagnostic, category),
                _ => PolicyDiagnosticOutputWriter.BuildHumanText("Architecture validation error", message, diagnostic),
            };
        }

        WriteErrorContent(options, format, contentByFormat, allowFileSinks: fileCollision is null);
    }

    private static string? FindPolicyDiagnosticFileCollision(ValidateCommandOptions options, ArchitecturePolicyDiagnostic diagnostic)
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
            diagnostic.Location,
            diagnostic.RelatedLocations);

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
