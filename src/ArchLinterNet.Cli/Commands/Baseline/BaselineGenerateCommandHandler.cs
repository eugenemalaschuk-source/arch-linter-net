using System.Text.Json;
using ArchLinterNet.Cli.Abstractions;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Validation;

namespace ArchLinterNet.Cli.Commands.Baseline;

internal sealed class BaselineGenerateCommandHandler(ICliRuntime runtime, ICliConsole console, IFileSystem fileSystem, CancellationToken cancellationToken = default)
{
    public int Execute(BaselineGenerateCommandOptions options)
    {
        if (options.ShowHelp)
        {
            console.Out.WriteLine(BaselineHelpTexts.HelpText);
            return CliExitCodes.Success;
        }

        if (options.Mode is not ("strict" or "audit" or "all"))
        {
            console.Error.WriteLine($"Invalid mode: {options.Mode}. Use 'strict', 'audit', or 'all'.");
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }

        if (!fileSystem.FileExists(options.PolicyPath))
        {
            console.Error.WriteLine($"Policy file not found: {options.PolicyPath}");
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }

        try
        {
            BaselineGenerationOutcome outcome = runtime.GenerateBaseline(new BaselineGenerationRequest
            {
                PolicyPath = options.PolicyPath,
                Mode = options.Mode,
                ConditionSetName = options.ConditionSetName,
                Reason = options.Reasons.Reason,
                ReasonForContract = options.Reasons.ReasonForContract,
                ReasonForFamily = options.Reasons.ReasonForFamily,
                ContractIds = options.ContractIds.ToList(),
                CancellationToken = cancellationToken,
            });

            if (!outcome.Succeeded)
            {
                if (outcome.Error != null)
                {
                    CliErrorOutputWriter.Write(console, options.Format, "configuration-error", outcome.Error);
                }
                else
                {
                    WriteConfigurationViolations(options.Format, outcome.ConfigurationViolations);
                }

                return CliExitCodes.InvalidArgumentsOrRuntimeError;
            }

            bool json = options.Format == "json";

            // Re-checked immediately before the write that actually publishes the baseline.
            cancellationToken.ThrowIfCancellationRequested();

            BaselineWriteGate gate = new(console, fileSystem);
            if (!gate.TryApply(
                    new BaselineWriteGate.Request(
                        "baseline generate", options.OutputPath, options.Write.DryRun, options.Write.Force,
                        outcome.Yaml!, CommentDiagnostic: null, InPlacePath: null, EmitProposalToStdout: !json),
                    out BaselineWriteGate.Disposition disposition, cancellationToken))
            {
                return CliExitCodes.InvalidArgumentsOrRuntimeError;
            }

            Report(options, outcome, disposition);
            return CliExitCodes.Success;
        }
        catch (OperationCanceledException)
        {
            return BaselineCancellationOutput.Write(console, "generation", options.Format == "json");
        }
        catch (Exception ex)
        {
            CliErrorOutputWriter.Write(console, options.Format, "unexpected-tool-failure", $"Baseline generation error: {ex.Message}");
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }
    }

    private void Report(
        BaselineGenerateCommandOptions options,
        BaselineGenerationOutcome outcome,
        BaselineWriteGate.Disposition disposition)
    {
        if (options.Format == "json")
        {
            bool written = disposition == BaselineWriteGate.Disposition.Written;
            console.Out.WriteLine(JsonSerializer.Serialize(new
            {
                status = BaselineWriteGate.StatusFor(disposition, "generated"),
                dryRun = disposition == BaselineWriteGate.Disposition.DryRun,
                output = options.OutputPath,
                candidateCount = outcome.CandidateCount,
                counts = BaselineLifecycleFormatter.Counts(outcome.Entries),
                entries = BaselineLifecycleFormatter.EntriesForJson(outcome.Entries),
                proposedContent = written ? null : outcome.Yaml,
            }));
            return;
        }

        // A stdout preview already is the document; adding a summary after it would corrupt the YAML
        // for anything that redirected it.
        if (disposition == BaselineWriteGate.Disposition.Preview)
        {
            return;
        }

        console.Out.WriteLine($"Generated baseline with {outcome.CandidateCount} violation entries.");
        console.Out.WriteLine(BaselineLifecycleFormatter.FormatForHumans(outcome.Entries));
        if (disposition == BaselineWriteGate.Disposition.Written)
        {
            console.Out.WriteLine($"Output: {options.OutputPath}");
        }
    }

    private void WriteConfigurationViolations(string format, IReadOnlyCollection<ArchitectureViolation> violations)
    {
        CliErrorOutputWriter.WriteConfigurationViolations(console, format, "generated", violations);
    }
}
