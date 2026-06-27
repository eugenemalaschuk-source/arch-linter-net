using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;
using ArchLinterNet.Core.Validation;

namespace ArchLinterNet.Cli;

public static class Program
{
    public static int Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "baseline")
        {
            return RunBaselineCommand(args[1..]);
        }

        return RunValidateCommand(args);
    }

    private static int RunBaselineCommand(string[] args)
    {
        int argIndex = 0;
        if (argIndex < args.Length && args[argIndex] == "generate")
        {
            argIndex++;
        }

        string policyPath = "architecture/dependencies.arch.yml";
        string? outputPath = null;
        string reason = "generated baseline";
        string mode = "all";
        string? conditionSetName = null;

        for (int i = argIndex; i < args.Length; i++)
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
            };

            BaselineGenerationOutcome outcome = ArchitectureBaselineService.Generate(request);

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

    private static int RunValidateCommand(string[] args)
    {
        string version = typeof(ArchitectureValidationService).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";

        string policyPath = "architecture/dependencies.arch.yml";
        string mode = "strict";
        string format = "human";
        List<string> contractIds = new();
        string? conditionSetName = null;
        bool timingsEnabled = false;
        string? baselinePath = null;

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--help" or "-h":
                    PrintHelp();
                    return 0;
                case "--version" or "-v":
                    Console.WriteLine($"arch-linter-net {version}");
                    return 0;
                case "--policy" or "-p" when i + 1 < args.Length:
                    policyPath = args[++i];
                    break;
                case "--mode" or "-m" when i + 1 < args.Length:
                    mode = args[++i];
                    break;
                case "--format" or "-f" when i + 1 < args.Length:
                    format = args[++i];
                    break;
                case "--contract" when i + 1 < args.Length:
                    contractIds.Add(args[++i]);
                    break;
                case "--condition-set" when i + 1 < args.Length:
                    conditionSetName = args[++i];
                    break;
                case "--baseline" when i + 1 < args.Length:
                    baselinePath = args[++i];
                    break;
                case "--strict":
                    mode = "strict";
                    break;
                case "--audit":
                    mode = "audit";
                    break;
                case "--json":
                    format = "json";
                    break;
                case "--timings":
                    timingsEnabled = true;
                    break;
                default:
                    Console.Error.WriteLine($"Unknown option: {args[i]}");
                    Console.Error.WriteLine("Run with --help for usage information.");
                    return 2;
            }
        }

        if (mode is not ("strict" or "audit"))
        {
            Console.Error.WriteLine($"Invalid mode: {mode}. Use 'strict' or 'audit'.");
            return 2;
        }

        if (format is not ("human" or "json"))
        {
            Console.Error.WriteLine($"Invalid format: {format}. Use 'human' or 'json'.");
            return 2;
        }

        if (!File.Exists(policyPath))
        {
            Console.Error.WriteLine($"Policy file not found: {policyPath}");
            return 2;
        }

        try
        {
            ValidationTiming? timing = timingsEnabled ? new ValidationTiming() : null;

            ValidationRequest request = new()
            {
                PolicyPath = policyPath,
                Mode = mode,
                ConditionSetName = conditionSetName,
                ContractIds = contractIds,
                BaselinePath = baselinePath,
                EnforceUnmatchedIgnoredViolationsPolicy = true,
            };

            ValidationOutcome outcome = ArchitectureValidationService.Validate(request, timing);

            if (format == "json")
            {
                Console.WriteLine(ArchitectureDiagnosticFormatter.FormatResultForCiArtifacts(
                    mode, outcome.Passed, outcome.Violations, outcome.Cycles, outcome.CoverageFindings,
                    outcome.UnmatchedIgnoredViolations,
                    outcome.PolicyConsistencyConfig == "off"
                        ? Array.Empty<PolicyConsistencyDiagnostic>()
                        : outcome.PolicyConsistencyFindings));
            }
            else
            {
                if (outcome.Passed)
                {
                    Console.WriteLine("Architecture validation passed.");
                }
                else
                {
                    if (outcome.Violations.Count > 0)
                    {
                        Console.WriteLine(ArchitectureDiagnosticFormatter.FormatViolationsForHumans(outcome.Violations));
                    }

                    if (outcome.Cycles.Count > 0)
                    {
                        Console.WriteLine(ArchitectureDiagnosticFormatter.FormatCyclesForHumans(outcome.Cycles));
                    }
                }

                if (outcome.PolicyConsistencyConfig != "off" && outcome.PolicyConsistencyFindings.Count > 0)
                {
                    string policyConsistencySection =
                        ArchitectureDiagnosticFormatter.FormatPolicyConsistencyForHumans(outcome.PolicyConsistencyFindings);
                    if (!string.IsNullOrEmpty(policyConsistencySection))
                    {
                        Console.WriteLine();
                        Console.WriteLine(policyConsistencySection);
                    }
                }

                if (outcome.UnmatchedIgnoredViolations.Count > 0 && outcome.UnmatchedIgnoredViolationsConfig != "off")
                {
                    string unmatchedSection =
                        ArchitectureDiagnosticFormatter.FormatUnmatchedForHumans(outcome.UnmatchedIgnoredViolations);
                    if (!string.IsNullOrEmpty(unmatchedSection))
                    {
                        Console.WriteLine();
                        Console.WriteLine(unmatchedSection);
                    }
                }

                if (outcome.CoverageConfig != "off" && outcome.CoverageFindings.Count > 0)
                {
                    string coverageSection =
                        ArchitectureDiagnosticFormatter.FormatCoverageForHumans(outcome.CoverageFindings);
                    if (!string.IsNullOrEmpty(coverageSection))
                    {
                        Console.WriteLine();
                        Console.WriteLine(coverageSection);
                    }
                }
            }

            timing?.WriteReport(Console.Error);

            return outcome.Passed ? 0 : 1;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Architecture validation error: {ex.Message}");
            return 2;
        }
    }

    private static void PrintHelp()
    {
        Console.WriteLine("""
            arch-linter-net — architecture contract linter for .NET

            Usage:
              arch-linter-net [options]
              arch-linter-net baseline generate --config <path> --output <path> [options]

            Validate Options:
              -p, --policy <path>   Path to YAML contract file
                                    (default: architecture/dependencies.arch.yml)
              -m, --mode <mode>     Validation mode: strict or audit (default: strict)
                  --strict          Shortcut for --mode strict
                  --audit           Shortcut for --mode audit
                  --contract <id>   Run only the contract with the given ID (may be repeated)
                  --condition-set <name>
                                    Use a named condition set from analysis.condition_sets
                                    to control conditional compilation symbols during
                                    Roslyn source analysis (default: policy default_condition_set,
                                    otherwise empty symbol set)
                  --baseline <path> Path to baseline file to merge with policy ignores
                  --timings         Print phase-level timing report to stderr
              -f, --format <fmt>    Output format: human or json (default: human)
                  --json            Shortcut for --format json
              -h, --help            Show this help message
              -v, --version         Show version

            Exit codes:
              0   All contracts passed
              1   One or more contracts failed
              2   Runtime error (invalid arguments, file not found, etc.)
            """);
    }

    private static void PrintBaselineHelp()
    {
        Console.WriteLine("""
            arch-linter-net baseline generate — generate a baseline of current violations

            Usage:
              arch-linter-net baseline generate --config <path> --output <path> [options]

            Options:
              --config <path>     Path to YAML contract file
                                  (default: architecture/dependencies.arch.yml)
              --output <path>     Path to write the generated baseline file (required)
              --mode <mode>       Contract mode: strict, audit, or all (default: all)
              --reason <text>     Reason text for baseline entries
                                  (default: "generated baseline")
              --condition-set <name>
                                  Use a named condition set from analysis.condition_sets
                                  to control conditional compilation symbols during
                                  Roslyn source analysis (default: policy
                                  default_condition_set, otherwise empty symbol set)
              -h, --help          Show this help message

            Exit codes:
              0   Baseline generated successfully
              2   Runtime error (invalid arguments, file not found, config violations, etc.)
            """);
    }
}
