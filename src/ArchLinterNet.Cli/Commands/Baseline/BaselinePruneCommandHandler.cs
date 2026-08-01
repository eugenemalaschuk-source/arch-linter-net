using System.Text.Json;
using ArchLinterNet.Cli.Abstractions;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Validation;

namespace ArchLinterNet.Cli.Commands.Baseline;

internal sealed class BaselinePruneCommandHandler(ICliRuntime runtime, ICliConsole console, IFileSystem fileSystem, CancellationToken cancellationToken = default)
{
    public int Execute(BaselinePruneCommandOptions options)
    {
        if (options.ShowHelp)
        {
            console.Out.WriteLine(BaselineHelpTexts.PruneHelpText);
            return CliExitCodes.Success;
        }

        if (options.Mode is not ("strict" or "audit" or "all"))
        {
            console.Error.WriteLine($"Invalid mode: {options.Mode}. Use 'strict', 'audit', or 'all'.");
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }

        if (options.BaselinePath == null)
        {
            console.Error.WriteLine("--baseline is required for baseline prune.");
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
            BaselinePruneOutcome outcome = runtime.PruneBaseline(new BaselinePruneRequest
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
                WriteConfigurationViolations(outcome.ConfigurationViolations);
                return CliExitCodes.InvalidArgumentsOrRuntimeError;
            }

            bool json = options.Format == "json";
            BaselineWriteGate.Disposition disposition;
            if (outcome.IsNoOp && !options.Write.DryRun && options.OutputPath != null && SamePath(options.OutputPath, options.BaselinePath))
            {
                // Do not turn a no-op into a read/decode/re-encode/write cycle: that can alter a
                // BOM or original encoding even when the text is unchanged.
                disposition = BaselineWriteGate.Disposition.Unchanged;
            }
            else if (outcome.IsNoOp && !options.Write.DryRun && options.OutputPath != null)
            {
                BaselineWriteGate gate = new(console, fileSystem);
                if (!gate.TryCopySource(
                        new BaselineWriteGate.Request(
                            "baseline prune", options.OutputPath, options.Write.DryRun, options.Write.Force,
                            outcome.Yaml!, outcome.CommentDiagnostic, options.BaselinePath, !json),
                        options.BaselinePath,
                        out disposition))
                {
                    return CliExitCodes.InvalidArgumentsOrRuntimeError;
                }
            }
            else
            {
                BaselineWriteGate gate = new(console, fileSystem);
                if (!gate.TryApply(
                        new BaselineWriteGate.Request(
                            "baseline prune", options.OutputPath, options.Write.DryRun, options.Write.Force,
                            outcome.Yaml!, outcome.CommentDiagnostic, options.BaselinePath, !json),
                        out disposition))
                {
                    return CliExitCodes.InvalidArgumentsOrRuntimeError;
                }
            }

            Report(options, outcome, disposition);
            return CliExitCodes.Success;
        }
        catch (Exception ex)
        {
            console.Error.WriteLine($"Baseline prune error: {ex.Message}");
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }
    }

    private void Report(
        BaselinePruneCommandOptions options,
        BaselinePruneOutcome outcome,
        BaselineWriteGate.Disposition disposition)
    {
        if (options.Format == "json")
        {
            bool written = disposition == BaselineWriteGate.Disposition.Written;
            console.Out.WriteLine(JsonSerializer.Serialize(new
            {
                status = BaselineWriteGate.StatusFor(disposition, "pruned"),
                dryRun = disposition == BaselineWriteGate.Disposition.DryRun,
                output = options.OutputPath,
                commentDiagnostic = outcome.CommentDiagnostic,
                counts = BaselineLifecycleFormatter.Counts(outcome.Entries),
                entries = BaselineLifecycleFormatter.EntriesForJson(outcome.Entries),
                removed = outcome.RemovedEntries.Select(r => new
                {
                    contractGroup = r.Entry.ContractGroup,
                    contractId = r.Entry.ContractId,
                    sourceType = r.Entry.SourceType,
                    forbiddenReference = r.Entry.ForbiddenReference,
                    removalReason = r.RemovalReason,
                    identity = BaselineLifecycleFormatter.IdentityForJson(r.Entry.Identity),
                }),
                proposedContent = written ? null : outcome.Yaml,
            }));
            return;
        }

        if (disposition == BaselineWriteGate.Disposition.Preview)
        {
            return;
        }

        console.Out.WriteLine($"Pruned baseline: removed {outcome.RemovedEntries.Count} entries.");
        console.Out.WriteLine(BaselineLifecycleFormatter.FormatForHumans(outcome.Entries));

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
        console.Error.WriteLine("Configuration violations detected — baseline cannot be pruned:");
        foreach (ArchitectureViolation violation in violations)
        {
            console.Error.WriteLine($"  {violation.SourceType}: {violation.ForbiddenNamespace}");
        }
    }

    private static bool SamePath(string left, string right)
    {
        return string.Equals(Path.GetFullPath(left), Path.GetFullPath(right), StringComparison.Ordinal);
    }
}
