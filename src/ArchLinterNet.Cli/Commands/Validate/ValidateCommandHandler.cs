using System.Text.Json;
using ArchLinterNet.Cli.Abstractions;
using ArchLinterNet.Cli.Commands;
using ArchLinterNet.Core.BuildState;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;
using ArchLinterNet.Core.Validation;

namespace ArchLinterNet.Cli.Commands.Validate;

internal sealed class ValidateCommandHandler
{
    private const string FormatHuman = "human";
    private const string FormatJson = "json";
    private const string FormatSarif = "sarif";

    private readonly ICliRuntime _runtime;
    private readonly ICliConsole _console;
    private readonly IFileSystem _fileSystem;
    private readonly ReportCoordinator _coordinator;

    public ValidateCommandHandler(ICliRuntime runtime, ICliConsole console, IFileSystem fileSystem)
    {
        _runtime = runtime;
        _console = console;
        _fileSystem = fileSystem;
        _coordinator = new ReportCoordinator(runtime, console, fileSystem);
    }
    public int Execute(ValidateCommandOptions options)
    {
        int? immediateResult = TryWriteImmediateResponse(options);
        if (immediateResult is not null)
        {
            return immediateResult.Value;
        }

        string errorFormat = ResolveEffectiveFormat(options);

        try
        {
            return ExecuteValidation(options, errorFormat);
        }
        catch (Exception ex) when (TryGetPolicyDiagnostic(ex, out ArchitecturePolicyDiagnostic? diagnostic))
        {
            WritePolicyDiagnostic(options, errorFormat, ex, diagnostic!);
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }
        catch (Exception ex)
        {
            WriteExecutionError(options, errorFormat, ex.Message);
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
    // this must not overwrite. See ReportCoordinator.TryRouteErrorToStreamSinks.
    //
    // outcomesByMode carries the underlying validation result (pass/fail, violation/cycle counts)
    // so it still reaches the user even when every sink that would have carried the full report
    // failed — an output-routing failure must not silently swallow the validation outcome itself.
    private void WriteOutputError(
        ValidateCommandOptions options,
        string format,
        RouteResult result,
        IReadOnlyList<(string Mode, ValidationOutcome Outcome)> outcomesByMode)
    {
        string status = result.Status == ReportRouteStatus.PartialOutput ? "partial-output" : "output-failed";
        string humanMessage = FormatOutputError(status, result);

        Dictionary<string, string> contentByFormat = new();
        foreach (string neededFormat in NeededErrorFormats(options, format))
        {
            contentByFormat[neededFormat] = neededFormat switch
            {
                FormatJson => BuildOutputErrorJsonText(status, humanMessage, result, outcomesByMode),
                FormatSarif => BuildOutputErrorSarifText(status, humanMessage, result, outcomesByMode),
                _ => BuildOutputErrorHumanText(humanMessage, result, outcomesByMode),
            };
        }

        WriteErrorContent(options, format, contentByFormat, allowFileSinks: false);
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

    private static string BuildOutputErrorJsonText(
        string status, string message, RouteResult result,
        IReadOnlyList<(string Mode, ValidationOutcome Outcome)> outcomesByMode)
    {
        return JsonSerializer.Serialize(new
        {
            kind = "architecture_execution_error",
            output_status = status,
            message,
            failed_paths = result.FailedPaths,
            committed_paths = result.CommittedPaths,
            uncommitted_paths = result.UncommittedPaths,
            errors = result.ErrorDetails,
            validation_results = BuildValidationOutcomeSummaries(outcomesByMode),
        });
    }

    private static string BuildOutputErrorSarifText(
        string status, string message, RouteResult result,
        IReadOnlyList<(string Mode, ValidationOutcome Outcome)> outcomesByMode)
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
                            ruleId = "architecture-output",
                            message = new { text = message },
                            properties = new
                            {
                                output_status = status,
                                failed_paths = result.FailedPaths,
                                committed_paths = result.CommittedPaths,
                                uncommitted_paths = result.UncommittedPaths,
                                errors = result.ErrorDetails,
                                validation_results = BuildValidationOutcomeSummaries(outcomesByMode),
                            },
                            locations = Array.Empty<object>(),
                        },
                    },
                },
            },
        });
    }

    private static string BuildOutputErrorHumanText(
        string message, RouteResult result, IReadOnlyList<(string Mode, ValidationOutcome Outcome)> outcomesByMode)
    {
        var sb = new System.Text.StringBuilder(message);
        sb.Append($"\n  validation result: {BuildValidationOutcomeSummaryText(outcomesByMode)}");
        if (result.UncommittedPaths.Count > 0)
        {
            sb.Append($"\n  uncommitted: {string.Join(", ", result.UncommittedPaths)}");
        }
        foreach (string detail in result.ErrorDetails)
        {
            sb.Append($"\n  {detail}");
        }
        return sb.ToString();
    }

    private static object[] BuildValidationOutcomeSummaries(
        IReadOnlyList<(string Mode, ValidationOutcome Outcome)> outcomesByMode)
    {
        return outcomesByMode
            .Select(o => (object)new
            {
                mode = o.Mode,
                passed = o.Outcome.Passed,
                violation_count = o.Outcome.Violations.Count,
                cycle_count = o.Outcome.Cycles.Count,
            })
            .ToArray();
    }

    private static string BuildValidationOutcomeSummaryText(
        IReadOnlyList<(string Mode, ValidationOutcome Outcome)> outcomesByMode)
    {
        return string.Join(", ", outcomesByMode.Select(o => o.Outcome.Passed
            ? $"{o.Mode}=passed"
            : $"{o.Mode}=failed ({o.Outcome.Violations.Count} violation(s), {o.Outcome.Cycles.Count} cycle(s))"));
    }

    // Catches every error that isn't a structured ArchitecturePolicyDiagnostic — including an
    // expression evaluation failure thrown deep inside contract checking (e.g.
    // ArchitectureExpressionFactService.Evaluate for a `when` predicate), which happens well after
    // policy load succeeds and so is never wrapped with policy-location provenance. Without this,
    // a --format json/sarif run would receive an unstructured stderr line instead of the format it
    // asked for; this emits the same "unexpected error" shape on stdout that format expects, with no
    // location (none is available at this point) rather than silently degrading to plain text.
    // Occurs before any legitimate report content exists for this invocation, so — unlike
    // WriteOutputError — it is safe to route into every configured sink, including files.
    private void WriteExecutionError(ValidateCommandOptions options, string format, string message)
    {
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

        WriteErrorContent(options, format, contentByFormat, allowFileSinks: true);
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
        bool allowFileSinks)
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
            RouteResult errorRouteResult = _coordinator.RouteErrorToAllSinks(options.AdditionalSinks, contentByFormat);
            if (errorRouteResult.Status != ReportRouteStatus.AllSucceeded)
            {
                // Writing the error report itself failed on at least one sink — the diagnostic
                // would otherwise vanish silently. Surface both the diagnostic that couldn't be
                // fully delivered and typed evidence of which destinations committed/failed.
                _console.Error.WriteLine(contentByFormat[errorFormat]);
                _console.Error.WriteLine(FormatOutputError(
                    errorRouteResult.Status == ReportRouteStatus.PartialOutput ? "partial-output" : "output-failed",
                    errorRouteResult));
            }

            return;
        }

        if (_coordinator.TryRouteErrorToStreamSinks(options.AdditionalSinks, contentByFormat))
        {
            return;
        }

        _console.Error.WriteLine(contentByFormat[errorFormat]);
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

    private static bool TryParseModes(string rawMode, out IReadOnlyList<string> modes, out string? error)
    {
        List<string> parsed = rawMode.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).ToList();
        if (parsed.Count == 0 || parsed.Any(mode => mode is not ("strict" or "audit")))
        {
            modes = Array.Empty<string>();
            error = $"Invalid mode: {rawMode}. Use 'strict', 'audit', or a comma-separated combination of both.";
            return false;
        }

        modes = parsed;
        error = null;
        return true;
    }

    private int ExecuteValidation(ValidateCommandOptions options, string errorFormat)
    {
        TryParseModes(options.Mode, out IReadOnlyList<string> modes, out _);

        return modes.Count == 1
            ? ExecuteSingleMode(options, modes[0], errorFormat)
            : ExecuteCombinedModes(options, modes, errorFormat);
    }

    private int ExecuteSingleMode(ValidateCommandOptions options, string mode, string errorFormat)
    {
        ValidationTiming? timing = options.TimingsEnabled ? new ValidationTiming() : null;
        ValidationRequest request = BuildValidationRequest(options, mode);

        ValidationOutcome outcome = _runtime.Validate(request, timing);

        string? importCollision = FindImportFileCollision(options, outcome.PolicyImportPaths);
        if (importCollision is not null)
        {
            _console.Error.WriteLine(importCollision);
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }

        string? receiptCollision = FindReceiptFileCollision(options, outcome.ResolvedAssemblyPaths);
        if (receiptCollision is not null)
        {
            _console.Error.WriteLine(receiptCollision);
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }

        RouteResult result = _coordinator.RouteSingleOutcome(options.Format, mode, outcome, options.AdditionalSinks);
        timing?.WriteReport(_console.Error);
        if (result.Status != ReportRouteStatus.AllSucceeded)
        {
            WriteOutputError(options, errorFormat, result, new[] { (mode, outcome) });
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }

        return outcome.Passed ? CliExitCodes.Success : CliExitCodes.ValidationFailure;
    }

    // One ArchitectureAnalysisSnapshot serves every requested mode: policy composition, project
    // discovery, and assembly loading happen once (inside _runtime.CreateSnapshot), and each
    // requested mode is evaluated against that same snapshot — see issue #363 /
    // openspec/specs/analysis-snapshot/spec.md.
    private int ExecuteCombinedModes(ValidateCommandOptions options, IReadOnlyList<string> modes, string errorFormat)
    {
        ValidationTiming? timing = options.TimingsEnabled ? new ValidationTiming() : null;
        AnalysisSnapshotRequest snapshotRequest = new()
        {
            PolicyPath = options.PolicyPath,
            ConditionSetName = options.ConditionSetName,
            ContractIds = options.ContractIds.ToList(),
            BaselinePath = options.BaselinePath,
            EnforceUnmatchedIgnoredViolationsPolicy = true,
            PreparationMode = options.EnsureBuilt ? BuildPreparationMode.EnsureBuilt : BuildPreparationMode.Ordinary,
            NoRestore = options.NoRestore,
            RequestedConfiguration = options.Configuration,
            RequestedTargetFramework = options.TargetFramework,
        };

        using ArchitectureAnalysisSnapshot snapshot = _runtime.CreateSnapshot(snapshotRequest, timing);

        bool allPassed = true;
        List<(string Mode, ValidationOutcome Outcome)> outcomesByMode = new();
        foreach (string mode in modes)
        {
            ValidationOutcome outcome = snapshot.Evaluate(mode, timing);
            outcomesByMode.Add((mode, outcome));
            allPassed &= outcome.Passed;
        }

        // All modes share the same policy document and build-state snapshot; check imports and
        // receipts from the first outcome.
        string? importCollision = FindImportFileCollision(options, outcomesByMode[0].Outcome.PolicyImportPaths);
        if (importCollision is not null)
        {
            _console.Error.WriteLine(importCollision);
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }

        string? receiptCollision = FindReceiptFileCollision(options, outcomesByMode[0].Outcome.ResolvedAssemblyPaths);
        if (receiptCollision is not null)
        {
            _console.Error.WriteLine(receiptCollision);
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }

        RouteResult result = _coordinator.RouteCombinedOutcomes(options.Format, outcomesByMode, options.AdditionalSinks);

        timing?.WriteReport(_console.Error);
        if (result.Status != ReportRouteStatus.AllSucceeded)
        {
            WriteOutputError(options, errorFormat, result, outcomesByMode);
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }

        return allPassed ? CliExitCodes.Success : CliExitCodes.ValidationFailure;
    }

    private static ValidationRequest BuildValidationRequest(ValidateCommandOptions options, string mode)
    {
        return new ValidationRequest
        {
            PolicyPath = options.PolicyPath,
            Mode = mode,
            ConditionSetName = options.ConditionSetName,
            ContractIds = options.ContractIds.ToList(),
            BaselinePath = options.BaselinePath,
            EnforceUnmatchedIgnoredViolationsPolicy = true,
            PreparationMode = options.EnsureBuilt ? BuildPreparationMode.EnsureBuilt : BuildPreparationMode.Ordinary,
            NoRestore = options.NoRestore,
            RequestedConfiguration = options.Configuration,
            RequestedTargetFramework = options.TargetFramework,
        };
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
