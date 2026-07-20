using System.Text.Json;
using ArchLinterNet.Cli.Abstractions;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Validation;

namespace ArchLinterNet.Cli.Commands.Baseline;

internal sealed class BaselineVerifyCommandHandler(ICliRuntime runtime, ICliConsole console, IFileSystem fileSystem)
{
    public int Execute(BaselineVerifyCommandOptions options)
    {
        if (options.ShowHelp)
        {
            console.Out.WriteLine(BaselineHelpTexts.VerifyHelpText);
            return CliExitCodes.Success;
        }

        if (options.Mode is not ("strict" or "audit" or "all"))
        {
            console.Error.WriteLine($"Invalid mode: {options.Mode}. Use 'strict', 'audit', or 'all'.");
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }

        if (options.BaselinePath == null)
        {
            console.Error.WriteLine("--baseline is required for baseline verify.");
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }

        if (!fileSystem.FileExists(options.PolicyPath))
        {
            console.Error.WriteLine($"Policy file not found: {options.PolicyPath}");
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }

        if (!fileSystem.FileExists(options.BaselinePath))
        {
            console.Error.WriteLine($"Baseline file not found: {options.BaselinePath}");
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }

        try
        {
            BaselineVerifyOutcome outcome = runtime.VerifyBaseline(new BaselineVerifyRequest
            {
                PolicyPath = options.PolicyPath,
                BaselinePath = options.BaselinePath,
                Mode = options.Mode,
                ConditionSetName = options.ConditionSetName,
                ContractIds = options.ContractIds.ToList(),
            });

            if (!outcome.Succeeded)
            {
                WriteConfigurationViolations(outcome.ConfigurationViolations);
                return CliExitCodes.InvalidArgumentsOrRuntimeError;
            }

            if (options.Format == "json")
            {
                console.Out.WriteLine(JsonSerializer.Serialize(new
                {
                    inSync = outcome.InSync,
                    @new = outcome.New.Select(e => BaselineDiffCommandHandler.FormatEntryForJson(e, "new")),
                    frozen = outcome.Frozen.Select(e => BaselineDiffCommandHandler.FormatEntryForJson(e, "matched")),
                    resolved = outcome.Resolved.Select(e => BaselineDiffCommandHandler.FormatEntryForJson(e, "stale")),
                    configurationErrors = outcome.ConfigurationErrors.Select(
                        e => BaselineDiffCommandHandler.FormatEntryForJson(e, "configuration_error")),
                }));
            }
            else
            {
                console.Out.WriteLine(BaselineDiffCommandHandler.FormatBaselineComparisonForHumans(
                    outcome.New,
                    outcome.Frozen,
                    outcome.Resolved,
                    outcome.ConfigurationErrors));
                console.Out.WriteLine(outcome.InSync ? "Baseline is in sync." : "Baseline is out of sync.");
            }

            return outcome.InSync ? CliExitCodes.Success : CliExitCodes.ValidationFailure;
        }
        catch (Exception ex)
        {
            console.Error.WriteLine($"Baseline verify error: {ex.Message}");
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }
    }

    private void WriteConfigurationViolations(IReadOnlyCollection<ArchitectureViolation> violations)
    {
        console.Error.WriteLine("Configuration violations detected — baseline cannot be verified:");
        foreach (ArchitectureViolation violation in violations)
        {
            console.Error.WriteLine($"  {violation.SourceType}: {violation.ForbiddenNamespace}");
        }
    }
}
