using ArchLinterNet.Cli.Abstractions;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;
using ArchLinterNet.Core.Validation;

namespace ArchLinterNet.Cli.Commands.Validate;

internal sealed class ValidateCommandHandler(ICliRuntime runtime, ICliConsole console, IFileSystem fileSystem)
{
    public int Execute(ValidateCommandOptions options)
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

        try
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

            if (options.Format == "json")
            {
                console.Out.WriteLine(runtime.FormatResultForCiArtifacts(
                    options.Mode,
                    outcome.Passed,
                    outcome.Violations,
                    outcome.Cycles,
                    outcome.CoverageFindings,
                    outcome.UnmatchedIgnoredViolations,
                    outcome.PolicyConsistencyConfig == "off"
                        ? Array.Empty<PolicyConsistencyDiagnostic>()
                        : outcome.PolicyConsistencyFindings,
                    outcome.CoverageSummaries,
                    outcome.ClassificationConflicts,
                    outcome.ClassificationMetadataFailures,
                    outcome.ClassificationRoles));
            }
            else if (options.Format == "sarif")
            {
                console.Out.WriteLine(runtime.FormatResultAsSarif(options.Mode, outcome.Violations, outcome.Cycles));
            }
            else
            {
                WriteHumanOutput(outcome);
            }

            timing?.WriteReport(console.Error);
            return outcome.Passed ? CliExitCodes.Success : CliExitCodes.ValidationFailure;
        }
        catch (Exception ex)
        {
            console.Error.WriteLine($"Architecture validation error: {ex.Message}");
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }
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
                console.Out.WriteLine(runtime.FormatCyclesForHumans(outcome.Cycles));
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
            outcome.ClassificationConflicts.Count > 0 || outcome.ClassificationMetadataFailures.Count > 0,
            () => runtime.FormatClassificationFactsForHumans(outcome.ClassificationConflicts, outcome.ClassificationMetadataFailures));
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
