using System.CommandLine;

namespace ArchLinterNet.Cli.Commands.Explain;

internal sealed class ExplainCommandDefinition(ExplainCommandHandler handler)
{
    public const string HelpText =
        """
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
        """;

    public Command Create()
    {
        Command command = new("explain");

        Option<string> sourceOption = new("--source");
        Option<string> targetOption = new("--target");
        Option<string> policyOption = CreateOption("--policy", "architecture/dependencies.arch.yml");
        policyOption.Aliases.Add("-p");
        Option<string> modeOption = CreateOption("--mode", "all");
        modeOption.Aliases.Add("-m");
        Option<string> levelOption = CreateOption("--level", "namespace");
        Option<string> formatOption = CreateOption("--format", "human");
        formatOption.Aliases.Add("-f");
        Option<string> conditionSetOption = new("--condition-set");
        Option<bool> helpOption = new("--help");
        helpOption.Aliases.Add("-h");

        command.Options.Add(sourceOption);
        command.Options.Add(targetOption);
        command.Options.Add(policyOption);
        command.Options.Add(modeOption);
        command.Options.Add(levelOption);
        command.Options.Add(formatOption);
        command.Options.Add(conditionSetOption);
        command.Options.Add(helpOption);

        command.SetAction(parseResult => handler.Execute(new ExplainCommandOptions(
            parseResult.GetValue(policyOption) ?? "architecture/dependencies.arch.yml",
            parseResult.GetValue(modeOption) ?? "all",
            parseResult.GetValue(levelOption) ?? "namespace",
            parseResult.GetValue(formatOption) ?? "human",
            parseResult.GetValue(conditionSetOption),
            parseResult.GetValue(sourceOption),
            parseResult.GetValue(targetOption),
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
