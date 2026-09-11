using System.CommandLine;

namespace ArchLinterNet.Cli.Commands.Badge.Application;

internal sealed class BadgeCommandDefinition(BadgeCommandHandler handler)
{
    public Command Create()
    {
        Command badge = new("badge", "Generate architecture validation badge payloads.");
        Command policy = new("architecture-policy", "Write Shields endpoint JSON from strict validation JSON.");
        Command health = new("architecture-health", "Write Shields endpoint JSON from canonical Architecture Health JSON.");
        Option<string> input = new("--input");
        Option<bool> help = new("--help");
        help.Aliases.Add("-h");
        policy.Options.Add(input);
        policy.Options.Add(help);
        policy.SetAction(result => handler.Execute(new BadgeCommandOptions(result.GetValue(input) ?? string.Empty, result.GetValue(help))));
        Option<string> healthInput = new("--input");
        Option<string> output = new("--output");
        Option<string> disclosureProfile = new("--disclosure-profile");
        Option<string> verifiedAt = new("--verified-at");
        Option<bool> verifyDisclosureProfile = new("--verify-disclosure-profile");
        Option<bool> healthHelp = new("--help");
        healthHelp.Aliases.Add("-h");
        health.Options.Add(healthInput);
        health.Options.Add(output);
        health.Options.Add(disclosureProfile);
        health.Options.Add(verifiedAt);
        health.Options.Add(verifyDisclosureProfile);
        health.Options.Add(healthHelp);
        health.SetAction(result => handler.ExecuteArchitectureHealth(new ArchitectureHealthBadgeCommandOptions(
            result.GetValue(healthInput) ?? string.Empty,
            result.GetValue(output),
            result.GetValue(healthHelp),
            result.GetValue(disclosureProfile),
            result.GetValue(verifiedAt),
            result.GetValue(verifyDisclosureProfile))));
        badge.Subcommands.Add(policy);
        badge.Subcommands.Add(health);
        return badge;
    }
}
