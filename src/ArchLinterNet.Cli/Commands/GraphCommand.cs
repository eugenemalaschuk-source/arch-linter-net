using ArchLinterNet.Core.Graph;
using ArchLinterNet.Core.Model;

namespace ArchLinterNet.Cli.Commands;

internal static class GraphCommand
{
    public static int Run(string[] args)
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

        if (!CliEngine.TryParseLevel(level, out ArchitectureGraphLevel graphLevel))
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

            ArchitectureGraphOutcome outcome = CliEngine.Engine.Value.BuildGraph(request);

            Console.WriteLine(format switch
            {
                "dot" => CliEngine.Engine.Value.GraphFormatter.FormatAsDot(outcome.Graph),
                "mermaid" => CliEngine.Engine.Value.GraphFormatter.FormatAsMermaid(outcome.Graph),
                _ => CliEngine.Engine.Value.GraphFormatter.FormatAsJson(outcome.Graph),
            });

            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Graph export error: {ex.Message}");
            return 2;
        }
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
}
