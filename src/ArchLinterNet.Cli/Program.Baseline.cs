using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Validation;

namespace ArchLinterNet.Cli;

public static partial class Program
{
    private static int RunBaselineCommand(string[] args)
    {
        if (args.Length > 0 && args[0] is "--help" or "-h")
        {
            PrintBaselineHelp();
            return 0;
        }

        return args.Length > 0
            ? args[0] switch
            {
                "generate" => RunBaselineGenerateCommand(args[1..]),
                "update" => RunBaselineUpdateCommand(args[1..]),
                "prune" => RunBaselinePruneCommand(args[1..]),
                "diff" => RunBaselineDiffCommand(args[1..]),
                "verify" => RunBaselineVerifyCommand(args[1..]),
                _ => RunBaselineGenerateCommand(args),
            }
            : RunBaselineGenerateCommand(args);
    }

    private static int RunBaselineGenerateCommand(string[] args)
    {
        string policyPath = "architecture/dependencies.arch.yml";
        string? outputPath = null;
        string reason = "generated baseline";
        string mode = "all";
        string? conditionSetName = null;
        List<string> contractIds = new();

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--help" or "-h":
                    PrintBaselineHelp();
                    return 0;
                case "--config" when i + 1 < args.Length:
                    policyPath = args[++i];
                    break;
                case "--output" when i + 1 < args.Length:
                    outputPath = args[++i];
                    break;
                case "--reason" when i + 1 < args.Length:
                    reason = args[++i];
                    break;
                case "--mode" or "-m" when i + 1 < args.Length:
                    mode = args[++i];
                    break;
                case "--condition-set" when i + 1 < args.Length:
                    conditionSetName = args[++i];
                    break;
                case "--contract" when i + 1 < args.Length:
                    contractIds.Add(args[++i]);
                    break;
                default:
                    Console.Error.WriteLine($"Unknown option: {args[i]}");
                    Console.Error.WriteLine("Run 'arch-linter-net baseline --help' for usage information.");
                    return 2;
            }
        }

        if (mode is not ("strict" or "audit" or "all"))
        {
            Console.Error.WriteLine($"Invalid mode: {mode}. Use 'strict', 'audit', or 'all'.");
            return 2;
        }

        if (outputPath == null)
        {
            Console.Error.WriteLine("--output is required for baseline generate.");
            return 2;
        }

        if (!File.Exists(policyPath))
        {
            Console.Error.WriteLine($"Policy file not found: {policyPath}");
            return 2;
        }

        try
        {
            BaselineGenerationRequest request = new()
            {
                PolicyPath = policyPath,
                Mode = mode,
                ConditionSetName = conditionSetName,
                Reason = reason,
                ContractIds = contractIds,
            };

            BaselineGenerationOutcome outcome = _engine.Value.GenerateBaseline(request);

            if (!outcome.Succeeded)
            {
                Console.Error.WriteLine("Configuration violations detected — baseline cannot be generated:");
                foreach (ArchitectureViolation v in outcome.ConfigurationViolations)
                {
                    Console.Error.WriteLine($"  {v.SourceType}: {v.ForbiddenNamespace}");
                }
                return 2;
            }

            File.WriteAllText(outputPath, outcome.Yaml);

            Console.WriteLine($"Generated baseline with {outcome.CandidateCount} violation entries.");
            Console.WriteLine($"Output: {outputPath}");

            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Baseline generation error: {ex.Message}");
            return 2;
        }
    }

    private static int RunBaselineUpdateCommand(string[] args)
    {
        string policyPath = "architecture/dependencies.arch.yml";
        string? baselinePath = null;
        string? outputPath = null;
        string reason = "generated baseline";
        string mode = "all";
        string? conditionSetName = null;
        List<string> contractIds = new();

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--help" or "-h":
                    PrintBaselineUpdateHelp();
                    return 0;
                case "--config" when i + 1 < args.Length:
                    policyPath = args[++i];
                    break;
                case "--baseline" when i + 1 < args.Length:
                    baselinePath = args[++i];
                    break;
                case "--output" when i + 1 < args.Length:
                    outputPath = args[++i];
                    break;
                case "--reason" when i + 1 < args.Length:
                    reason = args[++i];
                    break;
                case "--mode" or "-m" when i + 1 < args.Length:
                    mode = args[++i];
                    break;
                case "--condition-set" when i + 1 < args.Length:
                    conditionSetName = args[++i];
                    break;
                case "--contract" when i + 1 < args.Length:
                    contractIds.Add(args[++i]);
                    break;
                default:
                    Console.Error.WriteLine($"Unknown option: {args[i]}");
                    Console.Error.WriteLine("Run 'arch-linter-net baseline update --help' for usage information.");
                    return 2;
            }
        }

        if (mode is not ("strict" or "audit" or "all"))
        {
            Console.Error.WriteLine($"Invalid mode: {mode}. Use 'strict', 'audit', or 'all'.");
            return 2;
        }

        if (baselinePath == null)
        {
            Console.Error.WriteLine("--baseline is required for baseline update.");
            return 2;
        }

        if (outputPath == null)
        {
            Console.Error.WriteLine("--output is required for baseline update.");
            return 2;
        }

        if (!File.Exists(policyPath))
        {
            Console.Error.WriteLine($"Policy file not found: {policyPath}");
            return 2;
        }

        if (!File.Exists(baselinePath))
        {
            Console.Error.WriteLine($"Baseline file not found: {baselinePath}");
            return 2;
        }

        try
        {
            BaselineUpdateRequest request = new()
            {
                PolicyPath = policyPath,
                BaselinePath = baselinePath,
                Mode = mode,
                ConditionSetName = conditionSetName,
                Reason = reason,
                ContractIds = contractIds,
            };

            BaselineUpdateOutcome outcome = _engine.Value.UpdateBaseline(request);

            if (!outcome.Succeeded)
            {
                Console.Error.WriteLine("Configuration violations detected — baseline cannot be updated:");
                foreach (ArchitectureViolation v in outcome.ConfigurationViolations)
                {
                    Console.Error.WriteLine($"  {v.SourceType}: {v.ForbiddenNamespace}");
                }
                return 2;
            }

            File.WriteAllText(outputPath, outcome.Yaml);

            Console.WriteLine($"Updated baseline: preserved {outcome.PreservedCount}, added {outcome.NewCount} new entries.");
            Console.WriteLine($"Output: {outputPath}");

            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Baseline update error: {ex.Message}");
            return 2;
        }
    }

    private static int RunBaselinePruneCommand(string[] args)
    {
        string policyPath = "architecture/dependencies.arch.yml";
        string? baselinePath = null;
        string? outputPath = null;
        string mode = "all";
        string? conditionSetName = null;
        string format = "human";
        List<string> contractIds = new();

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--help" or "-h":
                    PrintBaselinePruneHelp();
                    return 0;
                case "--config" when i + 1 < args.Length:
                    policyPath = args[++i];
                    break;
                case "--baseline" when i + 1 < args.Length:
                    baselinePath = args[++i];
                    break;
                case "--output" when i + 1 < args.Length:
                    outputPath = args[++i];
                    break;
                case "--mode" or "-m" when i + 1 < args.Length:
                    mode = args[++i];
                    break;
                case "--condition-set" when i + 1 < args.Length:
                    conditionSetName = args[++i];
                    break;
                case "--contract" when i + 1 < args.Length:
                    contractIds.Add(args[++i]);
                    break;
                case "--json":
                    format = "json";
                    break;
                default:
                    Console.Error.WriteLine($"Unknown option: {args[i]}");
                    Console.Error.WriteLine("Run 'arch-linter-net baseline prune --help' for usage information.");
                    return 2;
            }
        }

        if (mode is not ("strict" or "audit" or "all"))
        {
            Console.Error.WriteLine($"Invalid mode: {mode}. Use 'strict', 'audit', or 'all'.");
            return 2;
        }

        if (baselinePath == null)
        {
            Console.Error.WriteLine("--baseline is required for baseline prune.");
            return 2;
        }

        if (outputPath == null)
        {
            Console.Error.WriteLine("--output is required for baseline prune.");
            return 2;
        }

        if (!File.Exists(policyPath))
        {
            Console.Error.WriteLine($"Policy file not found: {policyPath}");
            return 2;
        }

        if (!File.Exists(baselinePath))
        {
            Console.Error.WriteLine($"Baseline file not found: {baselinePath}");
            return 2;
        }

        try
        {
            BaselinePruneRequest request = new()
            {
                PolicyPath = policyPath,
                BaselinePath = baselinePath,
                Mode = mode,
                ConditionSetName = conditionSetName,
                ContractIds = contractIds,
            };

            BaselinePruneOutcome outcome = _engine.Value.PruneBaseline(request);

            if (!outcome.Succeeded)
            {
                Console.Error.WriteLine("Configuration violations detected — baseline cannot be pruned:");
                foreach (ArchitectureViolation v in outcome.ConfigurationViolations)
                {
                    Console.Error.WriteLine($"  {v.SourceType}: {v.ForbiddenNamespace}");
                }
                return 2;
            }

            File.WriteAllText(outputPath, outcome.Yaml);

            if (format == "json")
            {
                Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new
                {
                    output = outputPath,
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
                Console.WriteLine($"Pruned baseline: removed {outcome.RemovedEntries.Count} entries.");
                foreach (BaselineRemovedEntry removedEntry in outcome.RemovedEntries)
                {
                    Console.WriteLine(
                        $"  [{removedEntry.RemovalReason}] {removedEntry.Entry.ContractGroup}/{removedEntry.Entry.ContractId}: " +
                        $"{removedEntry.Entry.SourceType} -> {removedEntry.Entry.ForbiddenReference}");
                }

                Console.WriteLine($"Output: {outputPath}");
            }

            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Baseline prune error: {ex.Message}");
            return 2;
        }
    }

    private static int RunBaselineDiffCommand(string[] args)
    {
        string policyPath = "architecture/dependencies.arch.yml";
        string? baselinePath = null;
        string mode = "all";
        string? conditionSetName = null;
        string format = "human";
        List<string> contractIds = new();

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--help" or "-h":
                    PrintBaselineDiffHelp();
                    return 0;
                case "--config" when i + 1 < args.Length:
                    policyPath = args[++i];
                    break;
                case "--baseline" when i + 1 < args.Length:
                    baselinePath = args[++i];
                    break;
                case "--mode" or "-m" when i + 1 < args.Length:
                    mode = args[++i];
                    break;
                case "--condition-set" when i + 1 < args.Length:
                    conditionSetName = args[++i];
                    break;
                case "--contract" when i + 1 < args.Length:
                    contractIds.Add(args[++i]);
                    break;
                case "--json":
                    format = "json";
                    break;
                default:
                    Console.Error.WriteLine($"Unknown option: {args[i]}");
                    Console.Error.WriteLine("Run 'arch-linter-net baseline diff --help' for usage information.");
                    return 2;
            }
        }

        if (mode is not ("strict" or "audit" or "all"))
        {
            Console.Error.WriteLine($"Invalid mode: {mode}. Use 'strict', 'audit', or 'all'.");
            return 2;
        }

        if (baselinePath == null)
        {
            Console.Error.WriteLine("--baseline is required for baseline diff.");
            return 2;
        }

        if (!File.Exists(policyPath))
        {
            Console.Error.WriteLine($"Policy file not found: {policyPath}");
            return 2;
        }

        if (!File.Exists(baselinePath))
        {
            Console.Error.WriteLine($"Baseline file not found: {baselinePath}");
            return 2;
        }

        try
        {
            BaselineDiffRequest request = new()
            {
                PolicyPath = policyPath,
                BaselinePath = baselinePath,
                Mode = mode,
                ConditionSetName = conditionSetName,
                ContractIds = contractIds,
            };

            BaselineDiffOutcome outcome = _engine.Value.DiffBaseline(request);

            if (!outcome.Succeeded)
            {
                Console.Error.WriteLine("Configuration violations detected — baseline cannot be diffed:");
                foreach (ArchitectureViolation v in outcome.ConfigurationViolations)
                {
                    Console.Error.WriteLine($"  {v.SourceType}: {v.ForbiddenNamespace}");
                }
                return 2;
            }

            if (format == "json")
            {
                Console.WriteLine(FormatBaselineComparisonAsJson(
                    outcome.New, outcome.Frozen, outcome.Resolved, outcome.ConfigurationErrors));
            }
            else
            {
                Console.WriteLine(FormatBaselineComparisonForHumans(
                    outcome.New, outcome.Frozen, outcome.Resolved, outcome.ConfigurationErrors));
            }

            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Baseline diff error: {ex.Message}");
            return 2;
        }
    }

    private static int RunBaselineVerifyCommand(string[] args)
    {
        string policyPath = "architecture/dependencies.arch.yml";
        string? baselinePath = null;
        string mode = "all";
        string? conditionSetName = null;
        string format = "human";
        List<string> contractIds = new();

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--help" or "-h":
                    PrintBaselineVerifyHelp();
                    return 0;
                case "--config" when i + 1 < args.Length:
                    policyPath = args[++i];
                    break;
                case "--baseline" when i + 1 < args.Length:
                    baselinePath = args[++i];
                    break;
                case "--mode" or "-m" when i + 1 < args.Length:
                    mode = args[++i];
                    break;
                case "--condition-set" when i + 1 < args.Length:
                    conditionSetName = args[++i];
                    break;
                case "--contract" when i + 1 < args.Length:
                    contractIds.Add(args[++i]);
                    break;
                case "--json":
                    format = "json";
                    break;
                default:
                    Console.Error.WriteLine($"Unknown option: {args[i]}");
                    Console.Error.WriteLine("Run 'arch-linter-net baseline verify --help' for usage information.");
                    return 2;
            }
        }

        if (mode is not ("strict" or "audit" or "all"))
        {
            Console.Error.WriteLine($"Invalid mode: {mode}. Use 'strict', 'audit', or 'all'.");
            return 2;
        }

        if (baselinePath == null)
        {
            Console.Error.WriteLine("--baseline is required for baseline verify.");
            return 2;
        }

        if (!File.Exists(policyPath))
        {
            Console.Error.WriteLine($"Policy file not found: {policyPath}");
            return 2;
        }

        if (!File.Exists(baselinePath))
        {
            Console.Error.WriteLine($"Baseline file not found: {baselinePath}");
            return 2;
        }

        try
        {
            BaselineVerifyRequest request = new()
            {
                PolicyPath = policyPath,
                BaselinePath = baselinePath,
                Mode = mode,
                ConditionSetName = conditionSetName,
                ContractIds = contractIds,
            };

            BaselineVerifyOutcome outcome = _engine.Value.VerifyBaseline(request);

            if (!outcome.Succeeded)
            {
                Console.Error.WriteLine("Configuration violations detected — baseline cannot be verified:");
                foreach (ArchitectureViolation v in outcome.ConfigurationViolations)
                {
                    Console.Error.WriteLine($"  {v.SourceType}: {v.ForbiddenNamespace}");
                }
                return 2;
            }

            if (format == "json")
            {
                Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new
                {
                    inSync = outcome.InSync,
                    @new = outcome.New.Select(FormatEntryForJson),
                    frozen = outcome.Frozen.Select(FormatEntryForJson),
                    resolved = outcome.Resolved.Select(FormatEntryForJson),
                    configurationErrors = outcome.ConfigurationErrors.Select(FormatEntryForJson),
                }));
            }
            else
            {
                Console.WriteLine(FormatBaselineComparisonForHumans(
                    outcome.New, outcome.Frozen, outcome.Resolved, outcome.ConfigurationErrors));
                Console.WriteLine(outcome.InSync
                    ? "Baseline is in sync."
                    : "Baseline is out of sync.");
            }

            return outcome.InSync ? 0 : 1;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Baseline verify error: {ex.Message}");
            return 2;
        }
    }

    private static string FormatBaselineComparisonForHumans(
        IReadOnlyList<ArchitectureBaselineComparisonEntry> newEntries,
        IReadOnlyList<ArchitectureBaselineComparisonEntry> frozen,
        IReadOnlyList<ArchitectureBaselineComparisonEntry> resolved,
        IReadOnlyList<ArchitectureBaselineComparisonEntry> configurationErrors)
    {
        var lines = new List<string>
        {
            $"New (unbaselined) violations: {newEntries.Count}",
        };
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
        return System.Text.Json.JsonSerializer.Serialize(new
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
