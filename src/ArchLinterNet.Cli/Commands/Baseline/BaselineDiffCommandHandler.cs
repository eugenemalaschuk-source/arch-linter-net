using System.Text.Json;
using ArchLinterNet.Cli.Abstractions;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;
using ArchLinterNet.Core.Validation;

namespace ArchLinterNet.Cli.Commands.Baseline;

internal sealed class BaselineDiffCommandHandler(ICliRuntime runtime, ICliConsole console, IFileSystem fileSystem)
{
    public int Execute(BaselineDiffCommandOptions options)
    {
        if (options.ShowHelp)
        {
            console.Out.WriteLine(BaselineHelpTexts.DiffHelpText);
            return CliExitCodes.Success;
        }

        if (options.Mode is not ("strict" or "audit" or "all"))
        {
            console.Error.WriteLine($"Invalid mode: {options.Mode}. Use 'strict', 'audit', or 'all'.");
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }

        if (options.HasFormatConflict)
        {
            console.Error.WriteLine("--json cannot be combined with --format.");
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }

        if (options.Format is not ("human" or "json" or "sarif"))
        {
            console.Error.WriteLine("Invalid format. Use 'human', 'json', or 'sarif'.");
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

            BaselineComparisonReport report = new(
                outcome.New, outcome.Frozen, outcome.Resolved, outcome.Ambiguous, outcome.ConfigurationErrors,
                outcome.Entries);

            console.Out.WriteLine(options.Format switch
            {
                "json" => FormatBaselineComparisonAsJson(report),
                "sarif" => ArchitectureBaselineSarifFormatter.Format(report.LifecycleEntries, runtime.Version),
                _ => FormatBaselineComparisonForHumans(report),
            });

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

    /// <summary>
    /// One read-only comparison, in the shared lifecycle vocabulary. `diff` and `verify` project the
    /// identical shape so a consumer can switch between them without changing how it reads the output.
    /// </summary>
    /// <param name="Matched">Entries whose canonical identity equals exactly one current finding.</param>
    /// <param name="Resolved">Valid, evaluable entries with no current finding — the debt was fixed.</param>
    /// <param name="Stale">
    /// Entries referencing a contract that is no longer valid or evaluable. Kept as its own category
    /// rather than folded into `resolved`, which means the opposite: the entry is fine, the debt is gone.
    /// </param>
    /// <param name="LifecycleEntries">
    /// The same entries in the shared vocabulary, classified once by Core so human and JSON output
    /// cannot disagree about a status.
    /// </param>
    internal sealed record BaselineComparisonReport(
        IReadOnlyList<ArchitectureBaselineComparisonEntry> New,
        IReadOnlyList<ArchitectureBaselineComparisonEntry> Matched,
        IReadOnlyList<ArchitectureBaselineComparisonEntry> Resolved,
        IReadOnlyList<ArchitectureBaselineComparisonEntry> Ambiguous,
        IReadOnlyList<ArchitectureBaselineComparisonEntry> Stale,
        IReadOnlyList<BaselineLifecycleEntry> LifecycleEntries);

    internal static string FormatBaselineComparisonForHumans(BaselineComparisonReport report)
    {
        List<string> lines =
        [
            $"New (unbaselined) violations: {report.New.Count}",
        ];

        AppendEntryLines(lines, report.New);
        lines.Add($"Matched baseline entries: {report.Matched.Count}");
        AppendEntryLines(lines, report.Matched);
        lines.Add($"Resolved baseline entries (debt fixed): {report.Resolved.Count}");
        AppendEntryLines(lines, report.Resolved);
        lines.Add($"Ambiguous baseline entries (match more than one violation): {report.Ambiguous.Count}");
        AppendEntryLines(lines, report.Ambiguous);
        lines.Add($"Stale baseline entries (contract no longer valid): {report.Stale.Count}");
        AppendEntryLines(lines, report.Stale);
        return string.Join(Environment.NewLine, lines);
    }

    private static void AppendEntryLines(List<string> lines, IReadOnlyList<ArchitectureBaselineComparisonEntry> entries)
    {
        foreach (ArchitectureBaselineComparisonEntry entry in entries)
        {
            lines.Add(BaselineLifecycleFormatter.Describe(entry));
        }
    }

    internal static string FormatBaselineComparisonAsJson(BaselineComparisonReport report)
    {
        return JsonSerializer.Serialize(BuildJsonPayload(report, inSync: null));
    }

    /// <summary>
    /// The shared JSON payload. `verify` passes its in-sync verdict; `diff` has none, being a report
    /// rather than a gate, and omits the field.
    /// </summary>
    internal static object BuildJsonPayload(BaselineComparisonReport report, bool? inSync)
    {
        return new
        {
            inSync,
            counts = BaselineLifecycleFormatter.Counts(report.LifecycleEntries),
            // `entries` is the shape to read: one flat list in the shared vocabulary. The per-category
            // keys below are kept for existing consumers, and their historical names (`frozen`,
            // `configurationErrors`) no longer decide the status — `status` does.
            entries = BaselineLifecycleFormatter.EntriesForJson(report.LifecycleEntries),
            @new = report.New.Select(e => BaselineLifecycleFormatter.EntryForJson(e, BaselineEntryLifecycle.New)),
            frozen = report.Matched.Select(e => BaselineLifecycleFormatter.EntryForJson(e, BaselineEntryLifecycle.Matched)),
            resolved = report.Resolved.Select(e => BaselineLifecycleFormatter.EntryForJson(e, BaselineEntryLifecycle.Resolved)),
            ambiguous = report.Ambiguous.Select(e => BaselineLifecycleFormatter.EntryForJson(e, BaselineEntryLifecycle.Ambiguous)),
            configurationErrors = report.Stale.Select(
                e => BaselineLifecycleFormatter.EntryForJson(e, BaselineEntryLifecycle.Stale)),
        };
    }
}
