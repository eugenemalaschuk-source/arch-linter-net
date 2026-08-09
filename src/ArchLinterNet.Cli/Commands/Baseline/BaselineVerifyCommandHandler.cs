using System.Text.Json;
using ArchLinterNet.Cli.Abstractions;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;
using ArchLinterNet.Core.Validation;

namespace ArchLinterNet.Cli.Commands.Baseline;

internal sealed class BaselineVerifyCommandHandler(ICliRuntime runtime, ICliConsole console, IFileSystem fileSystem, CancellationToken cancellationToken = default)
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
            CliErrorOutputWriter.Write(console, options.Format, "invalid-arguments", $"Invalid mode: {options.Mode}. Use 'strict', 'audit', or 'all'.");
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }

        if (options.HasFormatConflict)
        {
            CliErrorOutputWriter.Write(console, options.Format, "invalid-arguments", "--json cannot be combined with --format.");
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }

        if (options.Format is not ("human" or "json" or "sarif"))
        {
            CliErrorOutputWriter.Write(console, options.Format, "invalid-format", "Invalid format. Use 'human', 'json', or 'sarif'.");
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }

        if (options.BaselinePath == null)
        {
            CliErrorOutputWriter.Write(console, options.Format, "invalid-arguments", "--baseline is required for baseline verify.");
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }

        if (!fileSystem.FileExists(options.PolicyPath))
        {
            CliErrorOutputWriter.Write(console, options.Format, "configuration-error", $"Policy file not found: {options.PolicyPath}");
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }

        if (!fileSystem.FileExists(options.BaselinePath))
        {
            CliErrorOutputWriter.Write(console, options.Format, "configuration-error", $"Baseline file not found: {options.BaselinePath}");
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
                CancellationToken = cancellationToken,
                ContractIds = options.ContractIds.ToList(),
            });

            if (!outcome.Succeeded)
            {
                WriteConfigurationViolations(options.Format, outcome.ConfigurationViolations);
                return CliExitCodes.InvalidArgumentsOrRuntimeError;
            }

            BaselineDiffCommandHandler.BaselineComparisonReport report = new(
                outcome.New, outcome.Frozen, outcome.Resolved, outcome.Ambiguous, outcome.ConfigurationErrors,
                outcome.Entries);

            cancellationToken.ThrowIfCancellationRequested();

            if (options.Format == "json")
            {
                console.Out.WriteLine(JsonSerializer.Serialize(
                    BaselineDiffCommandHandler.BuildJsonPayload(report, outcome.InSync)));
            }
            else if (options.Format == "sarif")
            {
                console.Out.WriteLine(ArchitectureBaselineSarifFormatter.Format(report.LifecycleEntries, runtime.Version));
            }
            else
            {
                console.Out.WriteLine(BaselineDiffCommandHandler.FormatBaselineComparisonForHumans(report));
                console.Out.WriteLine(outcome.InSync ? "Baseline is in sync." : "Baseline is out of sync.");
            }

            return outcome.InSync ? CliExitCodes.Success : CliExitCodes.ValidationFailure;
        }
        catch (OperationCanceledException)
        {
            return BaselineCancellationOutput.Write(console, "verify", options.Format == "json");
        }
        catch (Exception ex)
        {
            CliErrorOutputWriter.Write(console, options.Format, "unexpected-tool-failure", $"Baseline verify error: {ex.Message}");
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }
    }

    private void WriteConfigurationViolations(string format, IReadOnlyCollection<ArchitectureViolation> violations)
    {
        CliErrorOutputWriter.WriteConfigurationViolations(console, format, "verified", violations);
    }
}
