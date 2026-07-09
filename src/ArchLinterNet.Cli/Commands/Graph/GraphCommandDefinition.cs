using System.CommandLine;
using System.CommandLine.Parsing;

namespace ArchLinterNet.Cli.Commands.Graph;

internal sealed class GraphCommandDefinition(GraphCommandHandler handler)
{
    public const string HelpText =
        """
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
        """;

    public Command Create()
    {
        Command command = new("graph");

        Option<string> policyOption = CreateOption("--policy", "architecture/dependencies.arch.yml");
        policyOption.Aliases.Add("-p");
        Option<string> modeOption = CreateOption("--mode", "all");
        modeOption.Aliases.Add("-m");
        Option<string> levelOption = CreateOption("--level", "namespace");
        Option<string> formatOption = CreateOption("--format", "json");
        formatOption.Aliases.Add("-f");
        Option<string> conditionSetOption = new("--condition-set");
        Option<string[]> contractOption = new("--contract");
        Option<bool> helpOption = new("--help");
        helpOption.Aliases.Add("-h");

        command.Options.Add(policyOption);
        command.Options.Add(modeOption);
        command.Options.Add(levelOption);
        command.Options.Add(formatOption);
        command.Options.Add(conditionSetOption);
        command.Options.Add(contractOption);
        command.Options.Add(helpOption);

        command.SetAction(parseResult => handler.Execute(new GraphCommandOptions(
            parseResult.GetValue(policyOption) ?? "architecture/dependencies.arch.yml",
            parseResult.GetValue(modeOption) ?? "all",
            parseResult.GetValue(levelOption) ?? "namespace",
            parseResult.GetValue(formatOption) ?? "json",
            parseResult.GetValue(conditionSetOption),
            parseResult.GetValue(contractOption) ?? Array.Empty<string>(),
            parseResult.GetValue(helpOption))));

        return command;
    }

    private static Option<string> CreateOption(string name, string defaultValue)
    {
        Option<string> option = new(name);
        option.DefaultValueFactory = _ => defaultValue;
        return option;
    }
}
