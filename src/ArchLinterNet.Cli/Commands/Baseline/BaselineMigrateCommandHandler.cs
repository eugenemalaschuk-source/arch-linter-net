using System.Text.Json;
using ArchLinterNet.Cli.Abstractions;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;
using ArchLinterNet.Core.Validation;

namespace ArchLinterNet.Cli.Commands.Baseline;

internal sealed class BaselineMigrateCommandHandler(ICliRuntime runtime, ICliConsole console, IFileSystem fileSystem, CancellationToken cancellationToken = default)
{
    public int Execute(BaselineMigrateCommandOptions options)
    {
        if (options.ShowHelp)
        {
            console.Out.WriteLine(BaselineHelpTexts.MigrateHelpText);
            return CliExitCodes.Success;
        }

        if (!BaselineCommandGuards.TryRequireBaselinePath(console, options.Format, "baseline migrate", options.BaselinePath)
            || !BaselineCommandGuards.TryValidateFormat(console, options.Format, options.HasFormatConflict))
        {
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }

        if (!options.DryRun && options.OutputPath == null)
        {
            CliErrorOutputWriter.Write(console, options.Format, "invalid-arguments", "--output is required for baseline migrate unless --dry-run/--check is used.");
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }

        if (!BaselineCommandGuards.TryValidatePolicyFile(console, fileSystem, options.Format, options.PolicyPath)
            || !BaselineCommandGuards.TryValidateBaselineFile(console, fileSystem, options.Format, options.BaselinePath))
        {
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }

        try
        {
            BaselineMigrateOutcome outcome = runtime.MigrateBaseline(new BaselineMigrateRequest
            {
                PolicyPath = options.PolicyPath,
                BaselinePath = options.BaselinePath,
                OutputPath = options.OutputPath,
                ConditionSetName = options.ConditionSetName,
                CancellationToken = cancellationToken,
                DryRun = options.DryRun,
            });

            if (outcome.Error != null)
            {
                CliErrorOutputWriter.Write(
                    console, options.Format, "configuration-error", $"Baseline migrate error: {outcome.Error}");
                return CliExitCodes.InvalidArgumentsOrRuntimeError;
            }

            if (!outcome.Succeeded && outcome.ConfigurationViolations.Count > 0)
            {
                WriteConfigurationViolations(options.Format, outcome.ConfigurationViolations);
                return CliExitCodes.InvalidArgumentsOrRuntimeError;
            }

            // Migrate writes a reviewed file like every other baseline command, so it goes through the
            // same gate: preview, explicit overwrite intent, atomic replacement. Ambiguity still blocks
            // the write outright, upstream of the gate.
            bool wrote = false;
            if (outcome.Yaml != null && outcome.AmbiguousCount == 0)
            {
                // Re-checked immediately before the write that actually publishes the baseline.
                cancellationToken.ThrowIfCancellationRequested();

                BaselineWriteGate gate = new(console, fileSystem);
                if (!gate.TryApply(
                        new BaselineWriteGate.Request(
                            "baseline migrate", options.OutputPath, options.DryRun, options.Force,
                            outcome.Yaml, CommentDiagnostic: null, InPlacePath: null,
                            EmitProposalToStdout: options.Format == "human", Format: options.Format),
                        out BaselineWriteGate.Disposition disposition, cancellationToken))
                {
                    return CliExitCodes.InvalidArgumentsOrRuntimeError;
                }

                wrote = disposition == BaselineWriteGate.Disposition.Written;
            }
            else if (outcome.Yaml != null && options.DryRun && options.Format == "human")
            {
                console.Out.WriteLine("Dry run: ambiguous entries found, no file would be written. Proposed content:");
                console.Out.WriteLine(outcome.Yaml);
            }

            console.Out.WriteLine(options.Format switch
            {
                "json" => FormatAsJson(outcome, options.OutputPath, options.DryRun, wrote),
                "sarif" => ArchitectureBaselineSarifFormatter.Format(ToLifecycleEntries(outcome.Report), runtime.Version),
                _ => FormatForHumans(outcome, options.OutputPath, options.DryRun, wrote),
            });

            if (outcome.AmbiguousCount > 0)
            {
                return CliExitCodes.ValidationFailure;
            }

            return CliExitCodes.Success;
        }
        catch (OperationCanceledException)
        {
            return BaselineCancellationOutput.Write(console, "migrate", options.Format == "json");
        }
        catch (Exception ex)
        {
            CliErrorOutputWriter.Write(console, options.Format, "unexpected-tool-failure", $"Baseline migrate error: {ex.Message}");
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }
    }

    private void WriteConfigurationViolations(string format, IReadOnlyCollection<ArchitectureViolation> violations)
    {
        CliErrorOutputWriter.WriteConfigurationViolations(console, format, "migrated", violations);
    }

    private static string FormatForHumans(BaselineMigrateOutcome outcome, string? outputPath, bool dryRun, bool wrote)
    {
        List<string> lines =
        [
            $"Matched (migrated to version 2): {outcome.MatchedCount}",
            $"Stale (no current match, dropped): {outcome.StaleCount}",
            $"Ambiguous (multiple current matches, requires manual review): {outcome.AmbiguousCount}",
        ];

        foreach (BaselineMigrateEntryReport entry in outcome.Report.Where(e => e.Status != "matched"))
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
        else if (wrote)
        {
            lines.Add($"Output: {outputPath}");
        }
        else if (outcome.AmbiguousCount > 0)
        {
            lines.Add("No file written: ambiguous entries must be resolved first.");
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static string FormatAsJson(BaselineMigrateOutcome outcome, string? outputPath, bool dryRun, bool wrote)
    {
        return JsonSerializer.Serialize(new
        {
            dryRun,
            output = wrote ? outputPath : null,
            // The proposal travels in the document under --dry-run (and whenever no file was written),
            // so a review run can see exactly what would land.
            proposedContent = wrote ? null : outcome.Yaml,
            matchedCount = outcome.MatchedCount,
            staleCount = outcome.StaleCount,
            ambiguousCount = outcome.AmbiguousCount,
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

    private static IReadOnlyList<BaselineLifecycleEntry> ToLifecycleEntries(
        IReadOnlyList<BaselineMigrateEntryReport> report)
    {
        return report.Select(entry => new BaselineLifecycleEntry(
            new ArchitectureBaselineComparisonEntry(
                entry.ContractGroup, entry.ContractId, entry.SourceType, entry.ForbiddenReference, null, entry.Identity),
            entry.Status switch
            {
                "matched" => BaselineEntryLifecycle.Matched,
                "stale" => BaselineEntryLifecycle.Stale,
                "ambiguous" => BaselineEntryLifecycle.Ambiguous,
                _ => BaselineEntryLifecycle.ConfigurationError,
            })).ToArray();
    }
}
