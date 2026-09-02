using System.CommandLine;
using ArchLinterNet.Cli.Abstractions;
using ArchLinterNet.Cli.Commands.Topology.Abstractions;

namespace ArchLinterNet.Cli.Commands.Topology.EntryPoint;

internal sealed class CaptureTopologySubcommandModule : ITopologySubcommandModule
{
    public string CommandName => "capture";

    public Command CreateCommand(
        ICliRuntime runtime,
        ICliConsole console,
        IFileSystem fileSystem,
        CancellationToken cancellationToken = default)
    {
        TopologyCommandHandler handler = new(runtime, console, fileSystem, cancellationToken);
        Command command = new(CommandName, "Capture canonical topology observations for review.");

        Option<string> policy = OptionWithDefault("--policy", "architecture/dependencies.arch.yml");
        policy.Aliases.Add("-p");
        Option<string> subjectKind = new("--subject-kind");
        Option<string> format = new("--format");
        format.Aliases.Add("-f");
        Option<bool> json = new("--json");
        Option<string> output = new("--output");
        Option<string> conditionSet = new("--condition-set");
        Option<bool> ensureBuilt = new("--ensure-built");
        Option<bool> noRestore = new("--no-restore");
        Option<string> configuration = new("--configuration");
        Option<string> framework = new("--framework");
        Option<string> platform = new("--platform");
        Option<string> runtimeOption = new("--runtime");
        Option<int?> maxParallelism = new("--max-parallelism");
        Option<bool> help = new("--help");
        help.Aliases.Add("-h");

        command.Options.Add(policy);
        command.Options.Add(subjectKind);
        command.Options.Add(format);
        command.Options.Add(json);
        command.Options.Add(output);
        command.Options.Add(conditionSet);
        command.Options.Add(ensureBuilt);
        command.Options.Add(noRestore);
        command.Options.Add(configuration);
        command.Options.Add(framework);
        command.Options.Add(platform);
        command.Options.Add(runtimeOption);
        command.Options.Add(maxParallelism);
        command.Options.Add(help);
        command.SetAction(result => handler.Capture(new TopologyCaptureCommandOptions(
            result.GetValue(policy) ?? "architecture/dependencies.arch.yml",
            result.GetValue(subjectKind) ?? string.Empty,
            result.GetValue(json) ? "json" : result.GetValue(format) ?? "human",
            result.GetValue(output),
            result.GetValue(conditionSet),
            result.GetValue(help),
            result.GetValue(ensureBuilt),
            result.GetValue(noRestore),
            result.GetValue(configuration),
            result.GetValue(framework),
            result.GetValue(platform),
            result.GetValue(runtimeOption),
            result.GetValue(maxParallelism))
        {
            HasFormatConflict = result.GetValue(json) && result.GetValue(format) is not null,
        }));

        return command;
    }

    private static Option<string> OptionWithDefault(string name, string value)
    {
        Option<string> option = new(name);
        option.DefaultValueFactory = _ => value;
        return option;
    }
}
