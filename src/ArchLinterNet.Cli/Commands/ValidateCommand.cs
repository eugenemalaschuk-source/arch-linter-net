using ArchLinterNet.Core.Composition;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;
using ArchLinterNet.Core.Validation;

namespace ArchLinterNet.Cli.Commands;

internal static class ValidateCommand
{
    public static int Run(string[] args)
    {
        string version = typeof(ArchitectureEngine).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";

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

        if (format is not ("human" or "json" or "sarif"))
        {
            Console.Error.WriteLine($"Invalid format: {format}. Use 'human', 'json', or 'sarif'.");
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

            ValidationOutcome outcome = CliEngine.Engine.Value.Validate(request, timing);

            if (format == "json")
            {
                Console.WriteLine(CliEngine.Formatter.FormatResultForCiArtifacts(
                    mode, outcome.Passed, outcome.Violations, outcome.Cycles, outcome.CoverageFindings,
                    outcome.UnmatchedIgnoredViolations,
                    outcome.PolicyConsistencyConfig == "off"
                        ? Array.Empty<PolicyConsistencyDiagnostic>()
                        : outcome.PolicyConsistencyFindings,
                    outcome.CoverageSummaries));
            }
            else if (format == "sarif")
            {
                Console.WriteLine(CliEngine.SarifFormatter.FormatResultAsSarif(
                    mode, outcome.Violations, outcome.Cycles, version));
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
                        Console.WriteLine(CliEngine.Formatter.FormatViolationsForHumans(outcome.Violations));
                    }

                    if (outcome.Cycles.Count > 0)
                    {
                        Console.WriteLine(CliEngine.Formatter.FormatCyclesForHumans(outcome.Cycles));
                    }
                }

                if (outcome.PolicyConsistencyConfig != "off" && outcome.PolicyConsistencyFindings.Count > 0)
                {
                    string policyConsistencySection =
                        CliEngine.Formatter.FormatPolicyConsistencyForHumans(outcome.PolicyConsistencyFindings);
                    if (!string.IsNullOrEmpty(policyConsistencySection))
                    {
                        Console.WriteLine();
                        Console.WriteLine(policyConsistencySection);
                    }
                }

                if (outcome.UnmatchedIgnoredViolations.Count > 0 && outcome.UnmatchedIgnoredViolationsConfig != "off")
                {
                    string unmatchedSection =
                        CliEngine.Formatter.FormatUnmatchedForHumans(outcome.UnmatchedIgnoredViolations);
                    if (!string.IsNullOrEmpty(unmatchedSection))
                    {
                        Console.WriteLine();
                        Console.WriteLine(unmatchedSection);
                    }
                }

                if (outcome.CoverageConfig != "off" && outcome.CoverageFindings.Count > 0)
                {
                    string coverageSection =
                        CliEngine.Formatter.FormatCoverageForHumans(outcome.CoverageFindings);
                    if (!string.IsNullOrEmpty(coverageSection))
                    {
                        Console.WriteLine();
                        Console.WriteLine(coverageSection);
                    }
                }

                if (outcome.CoverageSummaries.Count > 0)
                {
                    string coverageSummarySection =
                        CliEngine.Formatter.FormatCoverageSummaryForHumans(outcome.CoverageSummaries);
                    if (!string.IsNullOrEmpty(coverageSummarySection))
                    {
                        Console.WriteLine();
                        Console.WriteLine(coverageSummarySection);
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
              arch-linter-net graph [options]
              arch-linter-net explain --source <id> --target <id> [options]

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
              -f, --format <fmt>    Output format: human, json, or sarif (default: human)
                                    sarif covers violations and cycles only; coverage,
                                    unmatched-ignore, and policy-consistency findings can
                                    still fail the run (exit code 1) without appearing in
                                    SARIF results — use --format json to see those
                  --json            Shortcut for --format json
              -h, --help            Show this help message
              -v, --version         Show version

            Exit codes:
              0   All contracts passed
              1   One or more contracts failed
              2   Runtime error (invalid arguments, file not found, etc.)
            """);
    }
}
