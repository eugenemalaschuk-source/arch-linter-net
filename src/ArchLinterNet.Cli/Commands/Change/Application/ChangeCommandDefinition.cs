using System.CommandLine;

namespace ArchLinterNet.Cli.Commands.Change.Application;

internal sealed class ChangeCommandDefinition(ChangeCommandHandler handler)
{
    public Command Create()
    {
        Command command = new("change", "Create and compare complete architecture analysis snapshots.");
        command.Subcommands.Add(CreateSnapshot());
        command.Subcommands.Add(CreateReport());
        return command;
    }

    private Command CreateSnapshot()
    {
        Command command = new("snapshot", "Write a complete architecture change snapshot.");
        Option<string> policy = OptionWithDefault("--policy", "architecture/dependencies.arch.yml");
        policy.Aliases.Add("-p");
        Option<string> mode = OptionWithDefault("--mode", "strict");
        Option<string> conditionSet = new("--condition-set");
        Option<string> baseline = new("--baseline");
        Option<string> output = new("--output");
        Option<bool> ensureBuilt = new("--ensure-built");
        Option<bool> noRestore = new("--no-restore");
        Option<string> configuration = new("--configuration");
        Option<string> framework = new("--framework");
        Option<string> platform = new("--platform");
        Option<string> runtime = new("--runtime");
        Option<bool> help = new("--help");
        help.Aliases.Add("-h");
        command.Options.Add(policy);
        command.Options.Add(mode);
        command.Options.Add(conditionSet);
        command.Options.Add(baseline);
        command.Options.Add(output);
        command.Options.Add(ensureBuilt);
        command.Options.Add(noRestore);
        command.Options.Add(configuration);
        command.Options.Add(framework);
        command.Options.Add(platform);
        command.Options.Add(runtime);
        command.Options.Add(help);
        command.SetAction(result => handler.CreateSnapshot(new ChangeSnapshotCommandOptions(
            result.GetValue(policy) ?? "architecture/dependencies.arch.yml",
            result.GetValue(mode) ?? "strict",
            result.GetValue(conditionSet),
            result.GetValue(baseline),
            result.GetValue(output) ?? string.Empty,
            result.GetValue(help),
            result.GetValue(ensureBuilt),
            result.GetValue(noRestore),
            result.GetValue(configuration),
            result.GetValue(framework),
            result.GetValue(platform),
            result.GetValue(runtime))));
        return command;
    }

    private Command CreateReport()
    {
        Command command = new("report", "Compare two complete architecture change snapshots.");
        Option<string> baseline = new("--base");
        Option<string> current = new("--current");
        Option<string> format = OptionWithDefault("--format", "human");
        format.Aliases.Add("-f");
        Option<string> output = new("--output");
        Option<bool> help = new("--help");
        help.Aliases.Add("-h");
        command.Options.Add(baseline);
        command.Options.Add(current);
        command.Options.Add(format);
        command.Options.Add(output);
        command.Options.Add(help);
        command.SetAction(result => handler.CreateReport(new ChangeReportCommandOptions(
            result.GetValue(baseline) ?? string.Empty,
            result.GetValue(current) ?? string.Empty,
            result.GetValue(format) ?? "human",
            result.GetValue(output),
            result.GetValue(help))));
        return command;
    }

    private static Option<string> OptionWithDefault(string name, string value)
    {
        Option<string> option = new(name);
        option.DefaultValueFactory = _ => value;
        return option;
    }
}
