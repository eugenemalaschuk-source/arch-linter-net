using System.CommandLine;
using ArchLinterNet.Cli.Commands.Badge.Application.Setup;

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
        Command setup = new("setup", "Preview or generate a versioned consumer badge setup.");
        Option<string> setupInput = new("--input");
        Option<string> setupOutput = new("--output");
        Option<string> setupRepository = new("--repository");
        Option<string> setupVisibility = new("--visibility");
        Option<string> setupMode = new("--mode");
        Option<string> setupProfile = new("--disclosure-profile");
        Option<string> setupAccount = new("--account");
        Option<string> setupAlias = new("--alias");
        Option<string> setupEndpoint = new("--endpoint");
        Option<string> setupPlan = new("--provider-plan");
        Option<int?> cadence = new("--cadence-minutes");
        Option<int?> lease = new("--max-lease-minutes");
        Option<bool> renewal = new("--renewal");
        Option<bool> dryRun = new("--dry-run");
        Option<string> setupFormat = new("--format") { DefaultValueFactory = _ => "json" };
        Option<bool> setupHelp = new("--help");
        setupHelp.Aliases.Add("-h");
        foreach (Option option in new Option[] { setupInput, setupOutput, setupRepository, setupVisibility, setupMode, setupProfile, setupAccount, setupAlias, setupEndpoint, setupPlan, cadence, lease, renewal, dryRun, setupFormat, setupHelp }) setup.Options.Add(option);
        setup.SetAction(result => handler.ExecuteSetup(new BadgeSetupCommandOptions(
            result.GetValue(setupInput), result.GetValue(setupOutput), result.GetValue(setupRepository), result.GetValue(setupVisibility),
            result.GetValue(setupMode), result.GetValue(setupProfile), result.GetValue(setupAccount), result.GetValue(setupAlias), result.GetValue(setupEndpoint), result.GetValue(setupPlan),
            result.GetValue(cadence), result.GetValue(lease), result.GetValue(renewal), result.GetValue(dryRun), false, result.GetValue(setupFormat) ?? "json", result.GetValue(setupHelp))));
        Command doctor = new("doctor", "Diagnose a versioned badge setup without writing.");
        Option<string> doctorInput = new("--input");
        Option<bool> publicDiagnostics = new("--public");
        Option<string> doctorFormat = new("--format") { DefaultValueFactory = _ => "json" };
        Option<bool> doctorHelp = new("--help");
        doctorHelp.Aliases.Add("-h");
        foreach (Option option in new Option[] { doctorInput, publicDiagnostics, doctorFormat, doctorHelp }) doctor.Options.Add(option);
        doctor.SetAction(result => handler.ExecuteDoctor(new BadgeSetupCommandOptions(
            result.GetValue(doctorInput), null, null, null, null, null, null, null, null, null, null, null, false, true,
            result.GetValue(publicDiagnostics), result.GetValue(doctorFormat) ?? "json", result.GetValue(doctorHelp))));
        health.Subcommands.Add(setup);
        health.Subcommands.Add(doctor);
        badge.Subcommands.Add(policy);
        badge.Subcommands.Add(health);
        return badge;
    }
}
