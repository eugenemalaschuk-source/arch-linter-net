using ArchLinterNet.Core.Graph;
using ArchLinterNet.Core.Model;

namespace ArchLinterNet.Cli.Commands;

internal static class ExplainCommand
{
    public static int Run(string[] args)
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

        if (!CliEngine.TryParseLevel(level, out ArchitectureGraphLevel graphLevel))
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

            ArchitectureExplainOutcome outcome = CliEngine.Engine.Value.Explain(request);

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
