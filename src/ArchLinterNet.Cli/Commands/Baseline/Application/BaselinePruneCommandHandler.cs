using System.Text.Json;
using ArchLinterNet.Cli.Abstractions;
using ArchLinterNet.Core.BuildState;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Validation;

namespace ArchLinterNet.Cli.Commands.Baseline.Application;

internal sealed class BaselinePruneCommandHandler(ICliRuntime runtime, ICliConsole console, IFileSystem fileSystem, CancellationToken cancellationToken = default)
{
    public int Execute(BaselinePruneCommandOptions options)
    {
        if (options.ShowHelp)
        {
            console.Out.WriteLine(BaselineHelpTexts.PruneHelpText);
            return CliExitCodes.Success;
        }

        if (!TryValidate(options))
        {
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }

        try
        {
            return ExecuteCore(options);
        }
        catch (OperationCanceledException)
        {
            return BaselineCancellationOutput.Write(console, "prune", options.Format == "json");
        }
        catch (Exception ex)
        {
            CliErrorOutputWriter.Write(console, options.Format, "unexpected-tool-failure", $"Baseline prune error: {ex.Message}");
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }
    }

    private bool TryValidate(BaselinePruneCommandOptions options)
    {
        return BaselineCommandGuards.TryValidateMode(console, options.Format, options.Mode)
            && BaselineCommandGuards.TryRequireBaselinePath(console, options.Format, "baseline prune", options.BaselinePath)
            && BaselineCommandGuards.TryValidatePolicyFile(console, fileSystem, options.Format, options.PolicyPath)
            && BaselineCommandGuards.TryValidateBaselineFile(console, fileSystem, options.Format, options.BaselinePath);
    }

    private int ExecuteCore(BaselinePruneCommandOptions options)
    {
        BaselinePruneOutcome outcome = runtime.PruneBaseline(new BaselinePruneRequest
        {
            PolicyPath = options.PolicyPath,
            BaselinePath = options.BaselinePath!,
            Mode = options.Mode,
            ConditionSetName = options.ConditionSetName,
            CancellationToken = cancellationToken,
            ContractIds = options.ContractIds.ToList(),
            PreparationMode = options.EnsureBuilt ? BuildPreparationMode.EnsureBuilt : BuildPreparationMode.Ordinary,
            NoRestore = options.NoRestore,
            RequestedConfiguration = options.Configuration,
            RequestedTargetFramework = options.TargetFramework,
            RequestedPlatform = options.Platform,
            RequestedRuntimeIdentifier = options.RuntimeIdentifier,
        });

        if (!outcome.Succeeded)
        {
            return HandleFailure(options, outcome);
        }

        if (!TryWrite(options, outcome, out BaselineWriteGate.Disposition disposition))
        {
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }

        Report(options, outcome, disposition);
        return CliExitCodes.Success;
    }

    private int HandleFailure(BaselinePruneCommandOptions options, BaselinePruneOutcome outcome)
    {
        if (BaselineCommandGuards.TryHandlePreflightFailure(console, options.Format, "prune", outcome.PreflightDiagnostics))
        {
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }

        WriteConfigurationViolations(options.Format, outcome.ConfigurationViolations);
        return CliExitCodes.InvalidArgumentsOrRuntimeError;
    }

    private bool TryWrite(
        BaselinePruneCommandOptions options,
        BaselinePruneOutcome outcome,
        out BaselineWriteGate.Disposition disposition)
    {
        bool json = options.Format == "json";
        if (outcome.IsNoOp && !options.Write.DryRun && options.OutputPath != null && SamePath(options.OutputPath, options.BaselinePath!))
        {
            // Do not turn a no-op into a read/decode/re-encode/write cycle: that can alter a
            // BOM or original encoding even when the text is unchanged.
            disposition = BaselineWriteGate.Disposition.Unchanged;
            return true;
        }

        // Re-checked immediately before the write that actually publishes the baseline.
        cancellationToken.ThrowIfCancellationRequested();

        BaselineWriteGate gate = new(console, fileSystem);
        BaselineWriteGate.Request request = new(
            "baseline prune", options.OutputPath, options.Write.DryRun, options.Write.Force,
            outcome.Yaml!, outcome.CommentDiagnostic, options.BaselinePath, !json, options.Format);
        return outcome.IsNoOp && !options.Write.DryRun && options.OutputPath != null
            ? gate.TryCopySource(request, options.BaselinePath!, out disposition, cancellationToken)
            : gate.TryApply(request, out disposition, cancellationToken);
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

    private void WriteConfigurationViolations(string format, IReadOnlyCollection<ArchitectureViolation> violations)
    {
        CliErrorOutputWriter.WriteConfigurationViolations(console, format, "pruned", violations);
    }

    private static bool SamePath(string left, string right)
    {
        return string.Equals(Path.GetFullPath(left), Path.GetFullPath(right), StringComparison.Ordinal);
    }
}
