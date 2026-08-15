using System.CommandLine;

namespace ArchLinterNet.Cli.Commands.Badge.Application;

internal sealed class BadgeCommandDefinition(BadgeCommandHandler handler)
{
    public Command Create()
    {
        Command badge = new("badge", "Generate architecture validation badge payloads.");
        Command policy = new("architecture-policy", "Write Shields endpoint JSON for complete strict policy validation.");
        Option<string> policyPath = new("--policy");
        policyPath.DefaultValueFactory = _ => "architecture/dependencies.arch.yml";
        Option<bool> ensureBuilt = new("--ensure-built");
        Option<bool> noRestore = new("--no-restore");
        Option<string> configuration = new("--configuration");
        Option<bool> help = new("--help");
        help.Aliases.Add("-h");
        policy.Options.Add(policyPath);
        policy.Options.Add(ensureBuilt);
        policy.Options.Add(noRestore);
        policy.Options.Add(configuration);
        policy.Options.Add(help);
        policy.SetAction(result => handler.Execute(new BadgeCommandOptions(
            result.GetValue(policyPath) ?? "architecture/dependencies.arch.yml",
            result.GetValue(ensureBuilt), result.GetValue(noRestore), result.GetValue(configuration), result.GetValue(help))));
        badge.Subcommands.Add(policy);
        return badge;
    }
}
