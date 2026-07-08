using ArchLinterNet.Core.Composition;
using ArchLinterNet.Core.Graph;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;
using ArchLinterNet.Core.Validation;

namespace ArchLinterNet.Cli;

public static partial class Program
{
    private static readonly ArchitectureDiagnosticFormatter _formatter = new();
    private static readonly ArchitectureSarifFormatter _sarifFormatter = new();

    private static readonly Lazy<ArchitectureEngine> _engine =
        new(() => new ArchitectureEngineBuilder().AddArchLinterNetCore().Build());

    public static int Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "baseline")
        {
            return RunBaselineCommand(args[1..]);
        }

        if (args.Length > 0 && args[0] == "graph")
        {
            return RunGraphCommand(args[1..]);
        }

        if (args.Length > 0 && args[0] == "explain")
        {
            return RunExplainCommand(args[1..]);
        }

        return RunValidateCommand(args);
    }

    private static int RunValidateCommand(string[] args)
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

            ValidationOutcome outcome = _engine.Value.Validate(request, timing);

            if (format == "json")
            {
                Console.WriteLine(_formatter.FormatResultForCiArtifacts(
                    mode, outcome.Passed, outcome.Violations, outcome.Cycles, outcome.CoverageFindings,
                    outcome.UnmatchedIgnoredViolations,
                    outcome.PolicyConsistencyConfig == "off"
                        ? Array.Empty<PolicyConsistencyDiagnostic>()
                        : outcome.PolicyConsistencyFindings,
                    outcome.CoverageSummaries));
            }
            else if (format == "sarif")
            {
                Console.WriteLine(_sarifFormatter.FormatResultAsSarif(
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
                        Console.WriteLine(_formatter.FormatViolationsForHumans(outcome.Violations));
                    }

                    if (outcome.Cycles.Count > 0)
                    {
                        Console.WriteLine(_formatter.FormatCyclesForHumans(outcome.Cycles));
                    }
                }

                if (outcome.PolicyConsistencyConfig != "off" && outcome.PolicyConsistencyFindings.Count > 0)
                {
                    string policyConsistencySection =
                        _formatter.FormatPolicyConsistencyForHumans(outcome.PolicyConsistencyFindings);
                    if (!string.IsNullOrEmpty(policyConsistencySection))
                    {
                        Console.WriteLine();
                        Console.WriteLine(policyConsistencySection);
                    }
                }

                if (outcome.UnmatchedIgnoredViolations.Count > 0 && outcome.UnmatchedIgnoredViolationsConfig != "off")
                {
                    string unmatchedSection =
                        _formatter.FormatUnmatchedForHumans(outcome.UnmatchedIgnoredViolations);
                    if (!string.IsNullOrEmpty(unmatchedSection))
                    {
                        Console.WriteLine();
                        Console.WriteLine(unmatchedSection);
                    }
                }

                if (outcome.CoverageConfig != "off" && outcome.CoverageFindings.Count > 0)
                {
                    string coverageSection =
                        _formatter.FormatCoverageForHumans(outcome.CoverageFindings);
                    if (!string.IsNullOrEmpty(coverageSection))
                    {
                        Console.WriteLine();
                        Console.WriteLine(coverageSection);
                    }
                }

                if (outcome.CoverageSummaries.Count > 0)
                {
                    string coverageSummarySection =
                        _formatter.FormatCoverageSummaryForHumans(outcome.CoverageSummaries);
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

    private static int RunGraphCommand(string[] args)
    {
        string policyPath = "architecture/dependencies.arch.yml";
        string mode = "all";
        string level = "namespace";
        string format = "json";
        string? conditionSetName = null;
        List<string> contractIds = new();

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--help" or "-h":
                    PrintGraphHelp();
                    return 0;
                case "--policy" or "-p" when i + 1 < args.Length:
                    policyPath = args[++i];
                    break;
                case "--mode" or "-m" when i + 1 < args.Length:
                    mode = args[++i];
                    break;
                case "--level" when i + 1 < args.Length:
                    level = args[++i];
                    break;
                case "--format" or "-f" when i + 1 < args.Length:
                    format = args[++i];
                    break;
                case "--condition-set" when i + 1 < args.Length:
                    conditionSetName = args[++i];
                    break;
                case "--contract" when i + 1 < args.Length:
                    contractIds.Add(args[++i]);
                    break;
                default:
                    Console.Error.WriteLine($"Unknown option: {args[i]}");
                    Console.Error.WriteLine("Run 'arch-linter-net graph --help' for usage information.");
                    return 2;
            }
        }

        if (mode is not ("strict" or "audit" or "all"))
        {
            Console.Error.WriteLine($"Invalid mode: {mode}. Use 'strict', 'audit', or 'all'.");
            return 2;
        }

        if (!TryParseLevel(level, out ArchitectureGraphLevel graphLevel))
        {
            Console.Error.WriteLine($"Invalid level: {level}. Use 'namespace', 'type', or 'assembly'.");
            return 2;
        }

        if (format is not ("json" or "dot" or "mermaid"))
        {
            Console.Error.WriteLine($"Invalid format: {format}. Use 'json', 'dot', or 'mermaid'.");
            return 2;
        }

        if (!File.Exists(policyPath))
        {
            Console.Error.WriteLine($"Policy file not found: {policyPath}");
            return 2;
        }

        try
        {
            ArchitectureGraphRequest request = new()
            {
                PolicyPath = policyPath,
                Mode = mode,
                Level = graphLevel,
                ConditionSetName = conditionSetName,
                ContractIds = contractIds,
            };

            ArchitectureGraphOutcome outcome = _engine.Value.BuildGraph(request);

            Console.WriteLine(format switch
            {
                "dot" => _engine.Value.GraphFormatter.FormatAsDot(outcome.Graph),
                "mermaid" => _engine.Value.GraphFormatter.FormatAsMermaid(outcome.Graph),
                _ => _engine.Value.GraphFormatter.FormatAsJson(outcome.Graph),
            });

            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Graph export error: {ex.Message}");
            return 2;
        }
    }

    private static int RunExplainCommand(string[] args)
    {
        string policyPath = "architecture/dependencies.arch.yml";
        string mode = "all";
        string level = "namespace";
        string format = "human";
        string? conditionSetName = null;
        string? source = null;
        string? target = null;

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--help" or "-h":
                    PrintExplainHelp();
                    return 0;
                case "--policy" or "-p" when i + 1 < args.Length:
                    policyPath = args[++i];
                    break;
                case "--mode" or "-m" when i + 1 < args.Length:
                    mode = args[++i];
                    break;
                case "--level" when i + 1 < args.Length:
                    level = args[++i];
                    break;
                case "--format" or "-f" when i + 1 < args.Length:
                    format = args[++i];
                    break;
                case "--condition-set" when i + 1 < args.Length:
                    conditionSetName = args[++i];
                    break;
                case "--source" when i + 1 < args.Length:
                    source = args[++i];
                    break;
                case "--target" when i + 1 < args.Length:
                    target = args[++i];
                    break;
                default:
                    Console.Error.WriteLine($"Unknown option: {args[i]}");
                    Console.Error.WriteLine("Run 'arch-linter-net explain --help' for usage information.");
                    return 2;
            }
        }

        if (mode is not ("strict" or "audit" or "all"))
        {
            Console.Error.WriteLine($"Invalid mode: {mode}. Use 'strict', 'audit', or 'all'.");
            return 2;
        }

        if (!TryParseLevel(level, out ArchitectureGraphLevel graphLevel))
        {
            Console.Error.WriteLine($"Invalid level: {level}. Use 'namespace' or 'type'.");
            return 2;
        }

        if (format is not ("human" or "json"))
        {
            Console.Error.WriteLine($"Invalid format: {format}. Use 'human' or 'json'.");
            return 2;
        }

        if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(target))
        {
            Console.Error.WriteLine("--source and --target are required.");
            return 2;
        }

        if (!File.Exists(policyPath))
        {
            Console.Error.WriteLine($"Policy file not found: {policyPath}");
            return 2;
        }

        try
        {
            ArchitectureExplainRequest request = new()
            {
                PolicyPath = policyPath,
                Source = source,
                Target = target,
                Mode = mode,
                Level = graphLevel,
                ConditionSetName = conditionSetName,
            };

            ArchitectureExplainOutcome outcome = _engine.Value.Explain(request);

            if (format == "json")
            {
                Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new
                {
                    source = outcome.Source,
                    target = outcome.Target,
                    path = outcome.Path,
                    contractIds = outcome.ContractIds,
                }));
            }
            else if (outcome.Path == null)
            {
                Console.WriteLine($"No dependency path found from '{outcome.Source}' to '{outcome.Target}'.");
            }
            else
            {
                Console.WriteLine(string.Join(" -> ", outcome.Path));
                if (outcome.ContractIds.Count > 0)
                {
                    Console.WriteLine($"Contract IDs: {string.Join(", ", outcome.ContractIds)}");
                }
            }

            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Explain error: {ex.Message}");
            return 2;
        }
    }

    private static bool TryParseLevel(string level, out ArchitectureGraphLevel graphLevel)
    {
        switch (level)
        {
            case "namespace":
                graphLevel = ArchitectureGraphLevel.Namespace;
                return true;
            case "type":
                graphLevel = ArchitectureGraphLevel.Type;
                return true;
            case "assembly":
                graphLevel = ArchitectureGraphLevel.Assembly;
                return true;
            default:
                graphLevel = default;
                return false;
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

    private static void PrintGraphHelp()
    {
        Console.WriteLine("""
            arch-linter-net graph — export the dependency graph for a policy

            Usage:
              arch-linter-net graph --policy <path> [options]

            Options:
              -p, --policy <path>   Path to YAML contract file
                                    (default: architecture/dependencies.arch.yml)
              -m, --mode <mode>     Contract mode: strict, audit, or all (default: all)
                  --level <level>   Graph granularity: namespace, type, or assembly
                                    (default: namespace)
              -f, --format <fmt>    Output format: json, dot, or mermaid (default: json)
                  --contract <id>   Restrict contract execution to this ID (may be repeated)
                  --condition-set <name>
                                    Use a named condition set from analysis.condition_sets
                                    to control conditional compilation symbols during
                                    Roslyn source analysis (default: policy default_condition_set,
                                    otherwise empty symbol set)
              -h, --help            Show this help message

            Exit codes:
              0   Graph exported successfully (regardless of contract violations)
              2   Runtime error (invalid arguments, file not found, etc.)
            """);
    }

    private static void PrintExplainHelp()
    {
        Console.WriteLine("""
            arch-linter-net explain — report the dependency path between two nodes

            Usage:
              arch-linter-net explain --source <id> --target <id> [options]

            Options:
              --source <id>         Source node id (type full name or namespace, per --level)
              --target <id>         Target node id (type full name, namespace, or an
                                    external dependency group name)
              -p, --policy <path>   Path to YAML contract file
                                    (default: architecture/dependencies.arch.yml)
              -m, --mode <mode>     Contract mode: strict, audit, or all (default: all)
                  --level <level>   Graph granularity: namespace or type (default: namespace)
                                    (assembly is not supported for explain; use 'graph --level assembly')
              -f, --format <fmt>    Output format: human or json (default: human)
                  --condition-set <name>
                                    Use a named condition set from analysis.condition_sets
                                    to control conditional compilation symbols during
                                    Roslyn source analysis (default: policy default_condition_set,
                                    otherwise empty symbol set)
              -h, --help            Show this help message

            Exit codes:
              0   Explanation produced, including a "no dependency path found" result
              2   Runtime error (invalid arguments, unsupported level, file not found, etc.)
            """);
    }
}
