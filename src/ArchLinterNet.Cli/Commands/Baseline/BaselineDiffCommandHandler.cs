using System.Text.Json;
using ArchLinterNet.Cli.Abstractions;
using ArchLinterNet.Core.Model;
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
                outcome.New, outcome.Frozen, outcome.Resolved, outcome.Ambiguous, outcome.ConfigurationErrors);

            console.Out.WriteLine(options.Format == "json"
                ? FormatBaselineComparisonAsJson(report)
                : FormatBaselineComparisonForHumans(report));

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
    internal sealed record BaselineComparisonReport(
        IReadOnlyList<ArchitectureBaselineComparisonEntry> New,
        IReadOnlyList<ArchitectureBaselineComparisonEntry> Existing,
        IReadOnlyList<ArchitectureBaselineComparisonEntry> Stale,
        IReadOnlyList<ArchitectureBaselineComparisonEntry> Ambiguous,
        IReadOnlyList<ArchitectureBaselineComparisonEntry> Configuration)
    {
        public IReadOnlyList<BaselineLifecycleEntry> LifecycleEntries()
        {
            List<BaselineLifecycleEntry> entries = new();
            Append(entries, New, BaselineEntryLifecycle.New);
            Append(entries, Existing, BaselineEntryLifecycle.Existing);
            Append(entries, Stale, BaselineEntryLifecycle.Stale);
            Append(entries, Ambiguous, BaselineEntryLifecycle.Ambiguous);
            Append(entries, Configuration, BaselineEntryLifecycle.Configuration);
            return entries;
        }

        private static void Append(
            List<BaselineLifecycleEntry> target,
            IReadOnlyList<ArchitectureBaselineComparisonEntry> source,
            BaselineEntryLifecycle lifecycle)
        {
            target.AddRange(source.Select(e => new BaselineLifecycleEntry(e, lifecycle)));
        }
    }

    internal static string FormatBaselineComparisonForHumans(BaselineComparisonReport report)
    {
        List<string> lines =
        [
            $"New (unbaselined) violations: {report.New.Count}",
        ];

        AppendEntryLines(lines, report.New);
        lines.Add($"Existing (frozen) baseline entries: {report.Existing.Count}");
        AppendEntryLines(lines, report.Existing);
        lines.Add($"Resolved (stale) baseline entries: {report.Stale.Count}");
        AppendEntryLines(lines, report.Stale);
        lines.Add($"Ambiguous baseline entries (match more than one violation): {report.Ambiguous.Count}");
        AppendEntryLines(lines, report.Ambiguous);
        lines.Add($"Configuration errors (unknown contract id): {report.Configuration.Count}");
        AppendEntryLines(lines, report.Configuration);
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
            counts = BaselineLifecycleFormatter.Counts(report.LifecycleEntries()),
            @new = report.New.Select(e => BaselineLifecycleFormatter.EntryForJson(e, BaselineEntryLifecycle.New)),
            // `frozen` keeps its historical key while its status value moves to the shared `existing`.
            frozen = report.Existing.Select(e => BaselineLifecycleFormatter.EntryForJson(e, BaselineEntryLifecycle.Existing)),
            resolved = report.Stale.Select(e => BaselineLifecycleFormatter.EntryForJson(e, BaselineEntryLifecycle.Stale)),
            ambiguous = report.Ambiguous.Select(e => BaselineLifecycleFormatter.EntryForJson(e, BaselineEntryLifecycle.Ambiguous)),
            configurationErrors = report.Configuration.Select(
                e => BaselineLifecycleFormatter.EntryForJson(e, BaselineEntryLifecycle.Configuration)),
        };
    }
}
