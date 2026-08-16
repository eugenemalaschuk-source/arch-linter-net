using System.CommandLine;

namespace ArchLinterNet.Cli.Commands.Badge.Application;

internal sealed class BadgeCommandDefinition(BadgeCommandHandler handler)
{
    public Command Create()
    {
        Command badge = new("badge", "Generate architecture validation badge payloads.");
        Command policy = new("architecture-policy", "Write Shields endpoint JSON from strict validation JSON.");
        Option<string> input = new("--input");
        Option<bool> help = new("--help");
        help.Aliases.Add("-h");
        policy.Options.Add(input);
        policy.Options.Add(help);
        policy.SetAction(result => handler.Execute(new BadgeCommandOptions(result.GetValue(input) ?? string.Empty, result.GetValue(help))));
        badge.Subcommands.Add(policy);
        return badge;
    }
}
