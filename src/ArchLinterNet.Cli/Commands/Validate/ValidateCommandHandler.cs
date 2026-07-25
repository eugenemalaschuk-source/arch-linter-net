using System.Text.Json;
using ArchLinterNet.Cli.Abstractions;
using ArchLinterNet.Cli.Commands;
using ArchLinterNet.Core.BuildState;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;
using ArchLinterNet.Core.Validation;

namespace ArchLinterNet.Cli.Commands.Validate;

internal sealed class ValidateCommandHandler
{
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
            return ExecuteValidation(options);
        }
        catch (Exception ex) when (TryGetPolicyDiagnostic(ex, out ArchitecturePolicyDiagnostic? diagnostic))
        {
            WritePolicyDiagnostic(errorFormat, ex, diagnostic!);
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }
        catch (Exception ex)
        {
            WriteExecutionError(errorFormat, ex.Message);
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }
    }

    private string ResolveEffectiveFormat(ValidateCommandOptions options)
    {
        if (options.IsFormatExplicit || options.AdditionalSinks.Count == 0)
        {
            return options.Format;
        }

        foreach (ReportSink sink in options.AdditionalSinks)
        {
            if (sink.DestinationType == ReportDestinationType.Stdout)
            {
                return sink.Format;
            }
        }

        return "human";
    }

    private void WriteOutputError(string format, RouteResult result)
    {
        string status = result.Status == ReportRouteStatus.PartialOutput ? "partial-output" : "output-failed";
        string message = FormatOutputError(status, result);

        if (format == "json" || format == "sarif")
        {
            _console.Error.WriteLine(JsonSerializer.Serialize(new
            {
                kind = "architecture_execution_error",
                output_status = status,
                message,
                failed_paths = result.FailedPaths,
                committed_paths = result.CommittedPaths,
                staged_paths = result.StagedPaths,
                errors = result.ErrorDetails,
            }));
            return;
        }

        _console.Error.WriteLine(message);
        if (result.StagedPaths.Count > 0)
        {
            _console.Error.WriteLine($"  staged (uncommitted): {string.Join(", ", result.StagedPaths)}");
        }
        foreach (string detail in result.ErrorDetails)
        {
            _console.Error.WriteLine($"  {detail}");
        }
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
    private void WriteExecutionError(string format, string message)
    {
        if (format == "json")
        {
            _console.Out.WriteLine(JsonSerializer.Serialize(new
            {
                kind = "architecture_execution_error",
                message,
            }));
            return;
        }

        if (format == "sarif")
        {
            _console.Out.WriteLine(JsonSerializer.Serialize(new
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
            }));
            return;
        }

        _console.Error.WriteLine($"Architecture validation error: {message}");
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

        if (options.Format is not ("human" or "json" or "sarif"))
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
            foreach (string importPath in policyImportPaths)
            {
                if (string.Equals(sinkFullPath, importPath, StringComparison.OrdinalIgnoreCase))
                {
                    return $"--report destination '{sink.FilePath}' matches imported policy file '{importPath}'";
                }
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

    private int ExecuteValidation(ValidateCommandOptions options)
    {
        TryParseModes(options.Mode, out IReadOnlyList<string> modes, out _);

        return modes.Count == 1
            ? ExecuteSingleMode(options, modes[0])
            : ExecuteCombinedModes(options, modes);
    }

    private int ExecuteSingleMode(ValidateCommandOptions options, string mode)
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

        RouteResult result = _coordinator.RouteSingleOutcome(options.Format, mode, outcome, options.AdditionalSinks);
        timing?.WriteReport(_console.Error);
        if (result.Status != ReportRouteStatus.AllSucceeded)
        {
            WriteOutputError(options.Format, result);
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }

        return outcome.Passed ? CliExitCodes.Success : CliExitCodes.ValidationFailure;
    }

    // One ArchitectureAnalysisSnapshot serves every requested mode: policy composition, project
    // discovery, and assembly loading happen once (inside _runtime.CreateSnapshot), and each
    // requested mode is evaluated against that same snapshot — see issue #363 /
    // openspec/specs/analysis-snapshot/spec.md.
    private int ExecuteCombinedModes(ValidateCommandOptions options, IReadOnlyList<string> modes)
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

        // All modes share the same policy document; check imports from first outcome
        string? importCollision = FindImportFileCollision(options, outcomesByMode[0].Outcome.PolicyImportPaths);
        if (importCollision is not null)
        {
            _console.Error.WriteLine(importCollision);
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }

        RouteResult result = _coordinator.RouteCombinedOutcomes(options.Format, outcomesByMode, options.AdditionalSinks);

        timing?.WriteReport(_console.Error);
        if (result.Status != ReportRouteStatus.AllSucceeded)
        {
            WriteOutputError(options.Format, result);
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
            ArchitecturePolicyImportException importException => importException.Diagnostic,
            ArchitecturePolicyValidationException validationException => validationException.Diagnostic,
            _ => null,
        };
        return diagnostic is not null;
    }

    private void WritePolicyDiagnostic(string format, Exception exception, ArchitecturePolicyDiagnostic diagnostic)
    {
        ArchitecturePolicyImportErrorCategory? category = (exception as ArchitecturePolicyImportException)?.Category;
        string message = exception.Message;
        if (format == "json")
        {
            PolicyDiagnosticOutputWriter.WriteJson(_console, message, diagnostic, category?.ToString());
            return;
        }

        if (format == "sarif")
        {
            object[] relatedLocations = ArchitectureSarifFormatter.FormatPolicyLocationsForSarif(
                diagnostic.Location,
                diagnostic.RelatedLocations);
            _console.Out.WriteLine(JsonSerializer.Serialize(new
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
                                properties = new { error_category = category?.ToString(), import_chain = diagnostic.ImportChain },
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
            }));
            return;
        }

        PolicyDiagnosticOutputWriter.WriteHuman(_console, "Architecture validation error", message, diagnostic);
    }
}
