using System.CommandLine;
using ArchLinterNet.Cli.Abstractions;
using ArchLinterNet.Cli.Commands.Topology.Abstractions;

namespace ArchLinterNet.Cli.Commands.Topology.EntryPoint;

internal sealed class VerifyTopologySubcommandModule : ITopologySubcommandModule
{
    public string CommandName => "verify";

    public Command CreateCommand(
        ICliRuntime runtime,
        ICliConsole console,
        IFileSystem fileSystem,
        CancellationToken cancellationToken = default)
    {
        TopologyCommandHandler handler = new(runtime, console, fileSystem, cancellationToken);
        Command command = new(CommandName, "Verify declared topology using ordinary validation.");

        Option<string> policy = OptionWithDefault("--policy", "architecture/dependencies.arch.yml");
        policy.Aliases.Add("-p");
        Option<string> mode = OptionWithDefault("--mode", "strict");
        mode.Aliases.Add("-m");
        Option<bool> strict = new("--strict");
        Option<bool> audit = new("--audit");
        Option<string> format = new("--format");
        format.Aliases.Add("-f");
        Option<bool> json = new("--json");
        Option<string> output = new("--output");
        Option<string> conditionSet = new("--condition-set");
        Option<string> baseline = new("--baseline");
        Option<string[]> contract = new("--contract");
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
        command.Options.Add(mode);
        command.Options.Add(strict);
        command.Options.Add(audit);
        command.Options.Add(format);
        command.Options.Add(json);
        command.Options.Add(output);
        command.Options.Add(conditionSet);
        command.Options.Add(baseline);
        command.Options.Add(contract);
        command.Options.Add(ensureBuilt);
        command.Options.Add(noRestore);
        command.Options.Add(configuration);
        command.Options.Add(framework);
        command.Options.Add(platform);
        command.Options.Add(runtimeOption);
        command.Options.Add(maxParallelism);
        command.Options.Add(help);
        command.SetAction(result => handler.Verify(new TopologyVerifyCommandOptions(
            result.GetValue(policy) ?? "architecture/dependencies.arch.yml",
            ResolveMode(result, mode, strict, audit),
            result.GetValue(json) ? "json" : result.GetValue(format) ?? "human",
            result.GetValue(output),
            result.GetValue(conditionSet),
            result.GetValue(baseline),
            result.GetValue(contract) ?? Array.Empty<string>(),
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

    private static string ResolveMode(ParseResult result, Option<string> mode, Option<bool> strict, Option<bool> audit)
    {
        string selected = "strict";
        bool expectModeValue = false;
        foreach (string token in result.Tokens.Select(token => token.Value))
        {
            if (expectModeValue)
            {
                selected = token;
                expectModeValue = false;
            }
            else if (token is "--mode" or "-m")
            {
                expectModeValue = true;
            }
            else if (token is "--strict")
            {
                selected = "strict";
            }
            else if (token is "--audit")
            {
                selected = "audit";
            }
        }

        return selected;
    }

    private static Option<string> OptionWithDefault(string name, string value)
    {
        Option<string> option = new(name);
        option.DefaultValueFactory = _ => value;
        return option;
    }
}
