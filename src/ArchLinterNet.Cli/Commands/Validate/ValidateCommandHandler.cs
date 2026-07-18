using System.Text.Json;
using ArchLinterNet.Cli.Abstractions;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;
using ArchLinterNet.Core.Validation;

namespace ArchLinterNet.Cli.Commands.Validate;

internal sealed class ValidateCommandHandler(ICliRuntime runtime, ICliConsole console, IFileSystem fileSystem)
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
            WritePolicyDiagnostic(options.Format, ex.Message, diagnostic!);
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

        if (options.Mode is not ("strict" or "audit"))
        {
            console.Error.WriteLine($"Invalid mode: {options.Mode}. Use 'strict' or 'audit'.");
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }

        if (options.Format is not ("human" or "json" or "sarif"))
        {
            console.Error.WriteLine($"Invalid format: {options.Format}. Use 'human', 'json', or 'sarif'.");
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }

        if (!fileSystem.FileExists(options.PolicyPath))
        {
            console.Error.WriteLine($"Policy file not found: {options.PolicyPath}");
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }

        return null;
    }

    private int ExecuteValidation(ValidateCommandOptions options)
    {
        ValidationTiming? timing = options.TimingsEnabled ? new ValidationTiming() : null;
        ValidationRequest request = new()
        {
            PolicyPath = options.PolicyPath,
            Mode = options.Mode,
            ConditionSetName = options.ConditionSetName,
            ContractIds = options.ContractIds.ToList(),
            BaselinePath = options.BaselinePath,
            EnforceUnmatchedIgnoredViolationsPolicy = true,
        };

        ValidationOutcome outcome = runtime.Validate(request, timing);
        WriteOutcome(options, outcome);
        timing?.WriteReport(console.Error);
        return outcome.Passed ? CliExitCodes.Success : CliExitCodes.ValidationFailure;
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
                outcome.ClassificationRoles, outcome.ClassificationPathDeferred));
            return;
        }

        if (options.Format == "sarif")
        {
            console.Out.WriteLine(runtime.FormatResultAsSarif(options.Mode, outcome.Violations, outcome.Cycles, outcome.CycleFindings));
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

    private void WritePolicyDiagnostic(string format, string message, ArchitecturePolicyDiagnostic diagnostic)
    {
        if (format == "json")
        {
            console.Out.WriteLine(JsonSerializer.Serialize(new
            {
                kind = "architecture_policy_error",
                message,
                policy_location = diagnostic.Location is null ? null : ArchitectureDiagnosticFormatter.FormatPolicyLocationForJson(diagnostic.Location),
                related_policy_locations = diagnostic.RelatedLocations.Select(ArchitectureDiagnosticFormatter.FormatPolicyLocationForJson),
                import_chain = diagnostic.ImportChain,
            }));
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

        string location = diagnostic.Location is null
            ? string.Empty
            : $" (policy: {diagnostic.Location.SourcePath}:{diagnostic.Location.YamlPath}; root: {diagnostic.Location.RootPath})";
        console.Error.WriteLine($"Architecture validation error: {message}{location}");
    }

    private void WriteHumanOutput(ValidationOutcome outcome)
    {
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
