using System.Text.Json;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Validation;

namespace ArchLinterNet.Cli.Commands.Baseline;

internal sealed class BaselineDiffCommandHandler(ICliRuntime runtime, ICliConsole console, IFileSystem fileSystem)
{
    public int Execute(BaselineDiffCommandOptions options)
    {
        if (options.ShowHelp)
        {
            console.Out.WriteLine(BaselineCommandDefinition.DiffHelpText);
            return CliExitCodes.Success;
        }

        if (options.Mode is not ("strict" or "audit" or "all"))
        {
            console.Error.WriteLine($"Invalid mode: {options.Mode}. Use 'strict', 'audit', or 'all'.");
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }

        if (options.BaselinePath == null)
        {
            console.Error.WriteLine("--baseline is required for baseline diff.");
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
            BaselineDiffOutcome outcome = runtime.DiffBaseline(new BaselineDiffRequest
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

            console.Out.WriteLine(options.Format == "json"
                ? FormatBaselineComparisonAsJson(outcome.New, outcome.Frozen, outcome.Resolved, outcome.ConfigurationErrors)
                : FormatBaselineComparisonForHumans(outcome.New, outcome.Frozen, outcome.Resolved, outcome.ConfigurationErrors));

            return CliExitCodes.Success;
        }
        catch (Exception ex)
        {
            console.Error.WriteLine($"Baseline diff error: {ex.Message}");
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }
    }

    private void WriteConfigurationViolations(IReadOnlyCollection<ArchitectureViolation> violations)
    {
        console.Error.WriteLine("Configuration violations detected — baseline cannot be diffed:");
        foreach (ArchitectureViolation violation in violations)
        {
            console.Error.WriteLine($"  {violation.SourceType}: {violation.ForbiddenNamespace}");
        }
    }

    internal static string FormatBaselineComparisonForHumans(
        IReadOnlyList<ArchitectureBaselineComparisonEntry> newEntries,
        IReadOnlyList<ArchitectureBaselineComparisonEntry> frozen,
        IReadOnlyList<ArchitectureBaselineComparisonEntry> resolved,
        IReadOnlyList<ArchitectureBaselineComparisonEntry> configurationErrors)
    {
        List<string> lines =
        [
            $"New (unbaselined) violations: {newEntries.Count}",
        ];

        AppendEntryLines(lines, newEntries);
        lines.Add($"Existing (frozen) baseline entries: {frozen.Count}");
        AppendEntryLines(lines, frozen);
        lines.Add($"Resolved (stale) baseline entries: {resolved.Count}");
        AppendEntryLines(lines, resolved);
        lines.Add($"Configuration errors (unknown contract id): {configurationErrors.Count}");
        AppendEntryLines(lines, configurationErrors);
        return string.Join(Environment.NewLine, lines);
    }

    private static void AppendEntryLines(List<string> lines, IReadOnlyList<ArchitectureBaselineComparisonEntry> entries)
    {
        foreach (ArchitectureBaselineComparisonEntry entry in entries)
        {
            lines.Add($"  {entry.ContractGroup}/{entry.ContractId}: {entry.SourceType} -> {entry.ForbiddenReference}");
        }
    }

    private static string FormatBaselineComparisonAsJson(
        IReadOnlyList<ArchitectureBaselineComparisonEntry> newEntries,
        IReadOnlyList<ArchitectureBaselineComparisonEntry> frozen,
        IReadOnlyList<ArchitectureBaselineComparisonEntry> resolved,
        IReadOnlyList<ArchitectureBaselineComparisonEntry> configurationErrors)
    {
        return JsonSerializer.Serialize(new
        {
            @new = newEntries.Select(FormatEntryForJson),
            frozen = frozen.Select(FormatEntryForJson),
            resolved = resolved.Select(FormatEntryForJson),
            configurationErrors = configurationErrors.Select(FormatEntryForJson),
        });
    }

    private static object FormatEntryForJson(ArchitectureBaselineComparisonEntry entry)
    {
        return new
        {
            contractGroup = entry.ContractGroup,
            contractId = entry.ContractId,
            sourceType = entry.SourceType,
            forbiddenReference = entry.ForbiddenReference,
            reason = entry.Reason,
        };
    }
}
