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

        if (!BaselineCommandGuards.TryValidateMode(console, options.Format, options.Mode)
            || !BaselineCommandGuards.TryValidateFormat(console, options.Format, options.HasFormatConflict)
            || !BaselineCommandGuards.TryRequireBaselinePath(console, options.Format, "baseline verify", options.BaselinePath)
            || !BaselineCommandGuards.TryValidatePolicyFile(console, fileSystem, options.Format, options.PolicyPath)
            || !BaselineCommandGuards.TryValidateBaselineFile(console, fileSystem, options.Format, options.BaselinePath))
        {
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
