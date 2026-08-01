using System.Text.Json;
using ArchLinterNet.Cli.Abstractions;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Validation;

namespace ArchLinterNet.Cli.Commands.Baseline;

internal sealed class BaselineUpdateCommandHandler(ICliRuntime runtime, ICliConsole console, IFileSystem fileSystem, CancellationToken cancellationToken = default)
{
    public int Execute(BaselineUpdateCommandOptions options)
    {
        if (options.ShowHelp)
        {
            console.Out.WriteLine(BaselineHelpTexts.UpdateHelpText);
            return CliExitCodes.Success;
        }

        if (options.Mode is not ("strict" or "audit" or "all"))
        {
            console.Error.WriteLine($"Invalid mode: {options.Mode}. Use 'strict', 'audit', or 'all'.");
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }

        if (options.BaselinePath == null)
        {
            console.Error.WriteLine("--baseline is required for baseline update.");
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
            BaselineUpdateOutcome outcome = runtime.UpdateBaseline(new BaselineUpdateRequest
            {
                PolicyPath = options.PolicyPath,
                BaselinePath = options.BaselinePath,
                Mode = options.Mode,
                ConditionSetName = options.ConditionSetName,
                CancellationToken = cancellationToken,
                Reason = options.Reasons.Reason,
                ReasonForContract = options.Reasons.ReasonForContract,
                ReasonForFamily = options.Reasons.ReasonForFamily,
                ContractIds = options.ContractIds.ToList(),
            });

            if (!outcome.Succeeded)
            {
                if (outcome.Error != null)
                {
                    console.Error.WriteLine(outcome.Error);
                }
                else
                {
                    WriteConfigurationViolations(outcome.ConfigurationViolations);
                }

                return CliExitCodes.InvalidArgumentsOrRuntimeError;
            }

            bool json = options.Format == "json";

            // Re-checked immediately before the write that actually publishes the baseline —
            // outcome above may have taken long enough that a Ctrl+C/SIGTERM arrived after Core's
            // own last check but before this handler commits anything.
            cancellationToken.ThrowIfCancellationRequested();

            BaselineWriteGate gate = new(console, fileSystem);
            if (!gate.TryApply(
                    new BaselineWriteGate.Request(
                        "baseline update", options.OutputPath, options.Write.DryRun, options.Write.Force,
                        outcome.Yaml!, outcome.CommentDiagnostic, options.BaselinePath, !json),
                    out BaselineWriteGate.Disposition disposition, cancellationToken))
            {
                return CliExitCodes.InvalidArgumentsOrRuntimeError;
            }

            Report(options, outcome, disposition);
            return CliExitCodes.Success;
        }
        catch (OperationCanceledException)
        {
            return BaselineCancellationOutput.Write(console, "update", options.Format == "json");
        }
        catch (Exception ex)
        {
            console.Error.WriteLine($"Baseline update error: {ex.Message}");
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }
    }

    private void Report(
        BaselineUpdateCommandOptions options,
        BaselineUpdateOutcome outcome,
        BaselineWriteGate.Disposition disposition)
    {
        if (options.Format == "json")
        {
            bool written = disposition == BaselineWriteGate.Disposition.Written;
            console.Out.WriteLine(JsonSerializer.Serialize(new
            {
                status = BaselineWriteGate.StatusFor(disposition, "updated"),
                dryRun = disposition == BaselineWriteGate.Disposition.DryRun,
                output = options.OutputPath,
                preservedCount = outcome.PreservedCount,
                newCount = outcome.NewCount,
                commentDiagnostic = outcome.CommentDiagnostic,
                counts = BaselineLifecycleFormatter.Counts(outcome.Entries),
                entries = BaselineLifecycleFormatter.EntriesForJson(outcome.Entries),
                proposedContent = written ? null : outcome.Yaml,
            }));
            return;
        }

        if (disposition == BaselineWriteGate.Disposition.Preview)
        {
            return;
        }

        console.Out.WriteLine($"Updated baseline: preserved {outcome.PreservedCount}, added {outcome.NewCount} new entries.");
        console.Out.WriteLine(BaselineLifecycleFormatter.FormatForHumans(outcome.Entries));

        // Surfaced even on a dry run: the run that would actually write is the one this blocks, and the
        // reviewer needs to know before they drop --dry-run.
        if (outcome.CommentDiagnostic != null)
        {
            console.Out.WriteLine(outcome.CommentDiagnostic);
        }

        if (disposition == BaselineWriteGate.Disposition.Written)
        {
            console.Out.WriteLine($"Output: {options.OutputPath}");
        }
    }

    private void WriteConfigurationViolations(IReadOnlyCollection<ArchitectureViolation> violations)
    {
        console.Error.WriteLine("Configuration violations detected — baseline cannot be updated:");
        foreach (ArchitectureViolation violation in violations)
        {
            console.Error.WriteLine($"  {violation.SourceType}: {violation.ForbiddenNamespace}");
        }
    }
}
