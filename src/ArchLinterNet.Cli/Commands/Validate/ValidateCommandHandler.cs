using System.Text.Json;
using ArchLinterNet.Cli.Abstractions;
using ArchLinterNet.Cli.Commands;
using ArchLinterNet.Core.BuildState;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;
using ArchLinterNet.Core.Validation;

namespace ArchLinterNet.Cli.Commands.Validate;

internal sealed class ValidateCommandHandler(ICliRuntime runtime, ICliConsole console)
{
    public int Execute(ValidateCommandOptions options)
    {
        int? immediateResult = TryWriteImmediateResponse(options);
        if (immediateResult is not null)
        {
            return immediateResult.Value;
        }

        try
        {
            return ExecuteValidation(options);
        }
        catch (Exception ex) when (TryGetPolicyDiagnostic(ex, out ArchitecturePolicyDiagnostic? diagnostic))
        {
            WritePolicyDiagnostic(options.Format, ex, diagnostic!);
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }
        catch (Exception ex)
        {
            WriteExecutionError(options.Format, ex.Message);
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }
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
            console.Out.WriteLine(JsonSerializer.Serialize(new
            {
                kind = "architecture_execution_error",
                message,
            }));
            return;
        }

        if (format == "sarif")
        {
            console.Out.WriteLine(JsonSerializer.Serialize(new
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

        console.Error.WriteLine($"Architecture validation error: {message}");
    }

    private int? TryWriteImmediateResponse(ValidateCommandOptions options)
    {
        if (options.ShowHelp)
        {
            console.Out.WriteLine(ValidateCommandDefinition.HelpText);
            return CliExitCodes.Success;
        }

        if (options.ShowVersion)
        {
            console.Out.WriteLine($"arch-linter-net {runtime.Version}");
            return CliExitCodes.Success;
        }

        if (!TryParseModes(options.Mode, out _, out string? modeError))
        {
            console.Error.WriteLine(modeError);
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }

        if (options.Format is not ("human" or "json" or "sarif"))
        {
            console.Error.WriteLine($"Invalid format: {options.Format}. Use 'human', 'json', or 'sarif'.");
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }

        return null;
    }

    // "strict,audit" (or any comma-separated combination) is parsed here, ahead of dispatch, so a
    // single requested mode keeps its exact original single-call path below and only a genuinely
    // combined request pays for building an ArchitectureAnalysisSnapshot.
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

        ValidationOutcome outcome = runtime.Validate(request, timing);
        WriteOutcome(options with { Mode = mode }, outcome);
        timing?.WriteReport(console.Error);
        return outcome.Passed ? CliExitCodes.Success : CliExitCodes.ValidationFailure;
    }

    // One ArchitectureAnalysisSnapshot serves every requested mode: policy composition, project
    // discovery, and assembly loading happen once (inside runtime.CreateSnapshot), and each
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

        using ArchitectureAnalysisSnapshot snapshot = runtime.CreateSnapshot(snapshotRequest, timing);

        bool allPassed = true;
        foreach (string mode in modes)
        {
            ValidationOutcome outcome = snapshot.Evaluate(mode, timing);
            WriteOutcome(options with { Mode = mode }, outcome);
            allPassed &= outcome.Passed;
        }

        timing?.WriteReport(console.Error);
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

    private void WriteOutcome(ValidateCommandOptions options, ValidationOutcome outcome)
    {
        if (options.Format == "json")
        {
            console.Out.WriteLine(runtime.FormatResultForCiArtifacts(
                options.Mode, outcome.Passed, outcome.Violations, outcome.Cycles, outcome.CycleFindings, outcome.CoverageFindings,
                outcome.UnmatchedIgnoredViolations,
                outcome.PolicyConsistencyConfig == "off" ? Array.Empty<PolicyConsistencyDiagnostic>() : outcome.PolicyConsistencyFindings,
                outcome.CoverageSummaries, outcome.ClassificationConflicts, outcome.ClassificationMetadataFailures,
                outcome.ClassificationRoles, outcome.ClassificationPathDeferred, outcome.PreflightDiagnostics));
            return;
        }

        if (options.Format == "sarif")
        {
            console.Out.WriteLine(runtime.FormatResultAsSarif(
                options.Mode, outcome.Violations, outcome.Cycles, outcome.CycleFindings, outcome.PreflightDiagnostics));
            return;
        }

        WriteHumanOutput(outcome);
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
            PolicyDiagnosticOutputWriter.WriteJson(console, message, diagnostic, category?.ToString());
            return;
        }

        if (format == "sarif")
        {
            object[] relatedLocations = ArchitectureSarifFormatter.FormatPolicyLocationsForSarif(
                diagnostic.Location,
                diagnostic.RelatedLocations);
            console.Out.WriteLine(JsonSerializer.Serialize(new
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

        PolicyDiagnosticOutputWriter.WriteHuman(console, "Architecture validation error", message, diagnostic);
    }

    private void WriteHumanOutput(ValidationOutcome outcome)
    {
        WriteOptionalSection(
            outcome.PreflightDiagnostics.Count > 0,
            () => runtime.FormatBuildStatePreflightForHumans(outcome.PreflightDiagnostics));

        if (outcome.PreflightBlocked)
        {
            return;
        }

        if (outcome.Passed)
        {
            console.Out.WriteLine("Architecture validation passed.");
        }
        else
        {
            if (outcome.Violations.Count > 0)
            {
                console.Out.WriteLine(runtime.FormatViolationsForHumans(outcome.Violations));
            }

            if (outcome.Cycles.Count > 0)
            {
                console.Out.WriteLine(runtime.FormatCyclesForHumans(outcome.Cycles, outcome.CycleFindings));
            }
        }

        WriteOptionalSection(
            outcome.PolicyConsistencyConfig != "off" && outcome.PolicyConsistencyFindings.Count > 0,
            () => runtime.FormatPolicyConsistencyForHumans(outcome.PolicyConsistencyFindings));

        WriteOptionalSection(
            outcome.UnmatchedIgnoredViolations.Count > 0 && outcome.UnmatchedIgnoredViolationsConfig != "off",
            () => runtime.FormatUnmatchedForHumans(outcome.UnmatchedIgnoredViolations));

        WriteOptionalSection(
            outcome.CoverageConfig != "off" && outcome.CoverageFindings.Count > 0,
            () => runtime.FormatCoverageForHumans(outcome.CoverageFindings));

        WriteOptionalSection(
            outcome.CoverageSummaries.Count > 0,
            () => runtime.FormatCoverageSummaryForHumans(outcome.CoverageSummaries));

        WriteOptionalSection(
            outcome.ClassificationConflicts.Count > 0 || outcome.ClassificationMetadataFailures.Count > 0
                || outcome.ClassificationPathDeferred != null,
            () => runtime.FormatClassificationFactsForHumans(
                outcome.ClassificationConflicts, outcome.ClassificationMetadataFailures, outcome.ClassificationPathDeferred));
    }

    private void WriteOptionalSection(bool shouldWrite, Func<string> contentFactory)
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

        console.Out.WriteLine();
        console.Out.WriteLine(content);
    }
}
