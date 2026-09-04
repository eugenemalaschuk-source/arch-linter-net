using System.CommandLine;

namespace ArchLinterNet.Cli.Commands.Measure.Application;

internal sealed class MeasureCommandDefinition(MeasureCommandHandler handler)
{
    public const string HelpText =
        """
        arch-linter-net measure — report declared architecture metrics

        Usage:
          arch-linter-net measure [options]

        Options:
          -p, --policy <path>   Path to YAML policy file
                                (default: architecture/dependencies.arch.yml)
          -f, --format <fmt>    Output format: human or json (default: human)
              --metric <id>     Restrict measurement to this metric ID (may be repeated)
              --condition-set <name>
                                Use a named condition set from analysis.condition_sets
              --max-contributors <n>
                                Maximum contributors shown per metric (default: 20)
              --all-contributors
                                Show every ordered contributor; overrides the default bound
              --ensure-built    Build and receipt-verify before measuring
              --no-restore      Do not restore while ensuring build state
              --configuration <name>
                                Requested build configuration
              --framework <tfm> Requested target framework
              --platform <platform>
                                Requested platform
              --runtime <rid>   Requested runtime identifier
          -h, --help            Show this help message

        Exit codes:
          0   Every selected metric is complete/evaluable (including value zero)
          2   Invalid arguments, policy/runtime error, or an unassessable metric
        """;

    public Command Create()
    {
        Command command = new("measure");

        Option<string> policyOption = CreateOption("--policy", "architecture/dependencies.arch.yml");
        policyOption.Aliases.Add("-p");
        Option<string> formatOption = CreateOption("--format", "human");
        formatOption.Aliases.Add("-f");
        Option<string[]> metricOption = new("--metric");
        Option<string> conditionSetOption = new("--condition-set");
        Option<int?> maxContributorsOption = new("--max-contributors");
        Option<bool> allContributorsOption = new("--all-contributors");
        Option<bool> ensureBuiltOption = new("--ensure-built");
        Option<bool> noRestoreOption = new("--no-restore");
        Option<string> configurationOption = new("--configuration");
        Option<string> targetFrameworkOption = new("--framework");
        Option<string> platformOption = new("--platform");
        Option<string> runtimeIdentifierOption = new("--runtime");
        Option<bool> helpOption = new("--help");
        helpOption.Aliases.Add("-h");

        command.Options.Add(policyOption);
        command.Options.Add(formatOption);
        command.Options.Add(metricOption);
        command.Options.Add(conditionSetOption);
        command.Options.Add(maxContributorsOption);
        command.Options.Add(allContributorsOption);
        command.Options.Add(ensureBuiltOption);
        command.Options.Add(noRestoreOption);
        command.Options.Add(configurationOption);
        command.Options.Add(targetFrameworkOption);
        command.Options.Add(platformOption);
        command.Options.Add(runtimeIdentifierOption);
        command.Options.Add(helpOption);

        command.SetAction(parseResult => handler.Execute(new MeasureCommandOptions(
            parseResult.GetValue(policyOption) ?? "architecture/dependencies.arch.yml",
            parseResult.GetValue(formatOption) ?? "human",
            parseResult.GetValue(metricOption) ?? Array.Empty<string>(),
            parseResult.GetValue(conditionSetOption),
            parseResult.GetValue(maxContributorsOption),
            parseResult.GetValue(allContributorsOption),
            parseResult.GetValue(helpOption),
            parseResult.GetValue(ensureBuiltOption),
            parseResult.GetValue(noRestoreOption),
            parseResult.GetValue(configurationOption),
            parseResult.GetValue(targetFrameworkOption),
            parseResult.GetValue(platformOption),
            parseResult.GetValue(runtimeIdentifierOption))));

        return command;
    }

    private static Option<string> CreateOption(string name, string defaultValue)
    {
        Option<string> option = new(name);
        option.DefaultValueFactory = _ => defaultValue;
        return option;
    }
}
