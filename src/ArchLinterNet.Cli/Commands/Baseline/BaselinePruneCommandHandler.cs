using System.Text.Json;
using ArchLinterNet.Cli.Abstractions;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Validation;

namespace ArchLinterNet.Cli.Commands.Baseline;

internal sealed class BaselinePruneCommandHandler(ICliRuntime runtime, ICliConsole console, IFileSystem fileSystem)
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

        if (options.OutputPath == null)
        {
            console.Error.WriteLine("--output is required for baseline prune.");
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
                ContractIds = options.ContractIds.ToList(),
            });

            if (!outcome.Succeeded)
            {
                WriteConfigurationViolations(outcome.ConfigurationViolations);
                return CliExitCodes.InvalidArgumentsOrRuntimeError;
            }

            fileSystem.WriteAllText(options.OutputPath, outcome.Yaml!);
            if (options.Format == "json")
            {
                console.Out.WriteLine(JsonSerializer.Serialize(new
                {
                    output = options.OutputPath,
                    removed = outcome.RemovedEntries.Select(r => new
                    {
                        contractGroup = r.Entry.ContractGroup,
                        contractId = r.Entry.ContractId,
                        sourceType = r.Entry.SourceType,
                        forbiddenReference = r.Entry.ForbiddenReference,
                        removalReason = r.RemovalReason,
                    }),
                }));
            }
            else
            {
                console.Out.WriteLine($"Pruned baseline: removed {outcome.RemovedEntries.Count} entries.");
                foreach (BaselineRemovedEntry removedEntry in outcome.RemovedEntries)
                {
                    console.Out.WriteLine(
                        $"  [{removedEntry.RemovalReason}] {removedEntry.Entry.ContractGroup}/{removedEntry.Entry.ContractId}: " +
                        $"{removedEntry.Entry.SourceType} -> {removedEntry.Entry.ForbiddenReference}");
                }

                console.Out.WriteLine($"Output: {options.OutputPath}");
            }

            return CliExitCodes.Success;
        }
        catch (Exception ex)
        {
            console.Error.WriteLine($"Baseline prune error: {ex.Message}");
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
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
}
