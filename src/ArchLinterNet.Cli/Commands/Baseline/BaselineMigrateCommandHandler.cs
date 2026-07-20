using System.Text.Json;
using ArchLinterNet.Cli.Abstractions;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Validation;

namespace ArchLinterNet.Cli.Commands.Baseline;

internal sealed class BaselineMigrateCommandHandler(ICliRuntime runtime, ICliConsole console, IFileSystem fileSystem)
{
    public int Execute(BaselineMigrateCommandOptions options)
    {
        if (options.ShowHelp)
        {
            console.Out.WriteLine(BaselineHelpTexts.MigrateHelpText);
            return CliExitCodes.Success;
        }

        if (options.Mode is not ("strict" or "audit" or "all"))
        {
            console.Error.WriteLine($"Invalid mode: {options.Mode}. Use 'strict', 'audit', or 'all'.");
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }

        if (options.BaselinePath == null)
        {
            console.Error.WriteLine("--baseline is required for baseline migrate.");
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }

        if (!options.DryRun && options.OutputPath == null)
        {
            console.Error.WriteLine("--output is required for baseline migrate unless --dry-run/--check is used.");
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
            BaselineMigrateOutcome outcome = runtime.MigrateBaseline(new BaselineMigrateRequest
            {
                PolicyPath = options.PolicyPath,
                BaselinePath = options.BaselinePath,
                OutputPath = options.OutputPath,
                Mode = options.Mode,
                ConditionSetName = options.ConditionSetName,
                ContractIds = options.ContractIds.ToList(),
                DryRun = options.DryRun,
            });

            if (outcome.Error != null)
            {
                console.Error.WriteLine($"Baseline migrate error: {outcome.Error}");
                return CliExitCodes.InvalidArgumentsOrRuntimeError;
            }

            if (!outcome.Succeeded && outcome.ConfigurationViolations.Count > 0)
            {
                WriteConfigurationViolations(outcome.ConfigurationViolations);
                return CliExitCodes.InvalidArgumentsOrRuntimeError;
            }

            if (outcome.Yaml != null && options.OutputPath != null)
            {
                fileSystem.WriteAllText(options.OutputPath, outcome.Yaml);
            }

            console.Out.WriteLine(options.Format == "json"
                ? FormatAsJson(outcome, options.OutputPath, options.DryRun)
                : FormatForHumans(outcome, options.OutputPath, options.DryRun));

            if (outcome.AmbiguousCount > 0)
            {
                return CliExitCodes.ValidationFailure;
            }

            return CliExitCodes.Success;
        }
        catch (Exception ex)
        {
            console.Error.WriteLine($"Baseline migrate error: {ex.Message}");
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }
    }

    private void WriteConfigurationViolations(IReadOnlyCollection<ArchitectureViolation> violations)
    {
        console.Error.WriteLine("Configuration violations detected — baseline cannot be migrated:");
        foreach (ArchitectureViolation violation in violations)
        {
            console.Error.WriteLine($"  {violation.SourceType}: {violation.ForbiddenNamespace}");
        }
    }

    private static string FormatForHumans(BaselineMigrateOutcome outcome, string? outputPath, bool dryRun)
    {
        List<string> lines =
        [
            $"Matched (migrated to version 2): {outcome.MatchedCount}",
            $"Stale (no current match, dropped): {outcome.StaleCount}",
            $"Ambiguous (multiple current matches, requires manual review): {outcome.AmbiguousCount}",
            $"Out of scope (carried through unchanged): {outcome.Report.Count(e => e.Status == "out_of_scope")}",
        ];

        foreach (BaselineMigrateEntryReport entry in outcome.Report.Where(e => e.Status is not ("matched" or "out_of_scope")))
        {
            lines.Add($"  [{entry.Status}] {entry.ContractGroup}/{entry.ContractId}: {entry.SourceType} -> {entry.ForbiddenReference}"
                + (entry.Status == "ambiguous" ? $" ({entry.MatchCount} current matches)" : string.Empty));
        }

        if (dryRun)
        {
            lines.Add(outcome.AmbiguousCount > 0
                ? "Dry run: ambiguous entries found, no file would be written."
                : "Dry run: no file written.");
        }
        else if (outputPath != null && outcome.Yaml != null)
        {
            lines.Add($"Output: {outputPath}");
        }
        else
        {
            lines.Add("No file written: ambiguous entries must be resolved first.");
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static string FormatAsJson(BaselineMigrateOutcome outcome, string? outputPath, bool dryRun)
    {
        return JsonSerializer.Serialize(new
        {
            dryRun,
            output = !dryRun && outcome.Yaml != null ? outputPath : null,
            matchedCount = outcome.MatchedCount,
            staleCount = outcome.StaleCount,
            ambiguousCount = outcome.AmbiguousCount,
            outOfScopeCount = outcome.Report.Count(e => e.Status == "out_of_scope"),
            entries = outcome.Report.Select(e => new
            {
                contractGroup = e.ContractGroup,
                contractId = e.ContractId,
                sourceType = e.SourceType,
                forbiddenReference = e.ForbiddenReference,
                status = e.Status,
                matchCount = e.MatchCount,
            }),
        });
    }
}
