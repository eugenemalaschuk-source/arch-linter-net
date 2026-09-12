using System.CommandLine;
using ArchLinterNet.Cli.Commands.Badge.Application.Setup;

namespace ArchLinterNet.Cli.Commands.Badge.Application;

internal sealed class BadgeCommandDefinition(BadgeCommandHandler handler)
{
    private const string InputOptionName = "--input";
    private const string HelpOptionName = "--help";

    public Command Create()
    {
        Command badge = new("badge", "Generate architecture validation badge payloads.");
        Command policy = new("architecture-policy", "Write Shields endpoint JSON from strict validation JSON.");
        Command health = new("architecture-health", "Write Shields endpoint JSON from canonical Architecture Health JSON.");
        Option<string> input = new(InputOptionName);
        Option<bool> help = new(HelpOptionName);
        help.Aliases.Add("-h");
        policy.Options.Add(input);
        policy.Options.Add(help);
        policy.SetAction(result => handler.Execute(new BadgeCommandOptions(result.GetValue(input) ?? string.Empty, result.GetValue(help))));
        Option<string> healthInput = new(InputOptionName);
        Option<string> output = new("--output");
        Option<string> disclosureProfile = new("--disclosure-profile");
        Option<string> verifiedAt = new("--verified-at");
        Option<bool> verifyDisclosureProfile = new("--verify-disclosure-profile");
        Option<bool> healthHelp = new(HelpOptionName);
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
        Option<string> setupInput = new(InputOptionName);
        Option<string> setupOutput = new("--output");
        Option<string> setupRepository = new("--repository");
        Option<string> setupVisibility = new("--visibility");
        Option<string> setupMode = new("--mode");
        Option<string> setupProfile = new("--disclosure-profile");
        Option<string> setupAccount = new("--account");
        Option<string> setupAlias = new("--alias");
        Option<string> setupEndpoint = new("--endpoint");
        Option<string> setupAudience = new("--audience");
        Option<string> setupPlan = new("--provider-plan");
        Option<long?> setupRepositoryId = new("--repository-id");
        Option<long?> setupRepositoryOwnerId = new("--repository-owner-id");
        Option<string> setupBaseRef = new("--base-ref") { DefaultValueFactory = _ => "main" };
        Option<string> setupPolicy = new("--policy") { DefaultValueFactory = _ => "architecture/dependencies.arch.yml" };
        Option<string> setupSolution = new("--solution") { DefaultValueFactory = _ => "ArchLinterNet.slnx" };
        Option<string> setupCapabilities = new("--capability-evidence");
        Option<bool> approveDisclosure = new("--approve-disclosure");
        Option<string> producerWorkflow = new("--producer-workflow") { DefaultValueFactory = _ => BadgeSetupContract.DefaultProducerWorkflowPath };
        Option<string> producerJobName = new("--producer-job-name") { DefaultValueFactory = _ => BadgeSetupContract.DefaultCheckName };
        Option<string> checkName = new("--check-name") { DefaultValueFactory = _ => BadgeSetupContract.DefaultCheckName };
        Option<string> checkApp = new("--check-app") { DefaultValueFactory = _ => BadgeSetupContract.DefaultCheckApp };
        Option<string> artifactName = new("--artifact-name") { DefaultValueFactory = _ => BadgeSetupContract.DefaultArtifactName };
        Option<string> evidenceArtifactName = new("--evidence-artifact-name") { DefaultValueFactory = _ => BadgeSetupContract.DefaultEvidenceArtifactName };
        Option<int?> cadence = new("--cadence-minutes");
        Option<int?> lease = new("--max-lease-minutes");
        Option<bool> renewal = new("--renewal");
        Option<bool> dryRun = new("--dry-run");
        Option<string> setupFormat = new("--format") { DefaultValueFactory = _ => "json" };
        Option<bool> setupHelp = new(HelpOptionName);
        setupHelp.Aliases.Add("-h");
        foreach (Option option in new Option[] { setupInput, setupOutput, setupRepository, setupVisibility, setupMode, setupProfile, setupAccount, setupAlias, setupEndpoint, setupAudience, setupPlan, setupRepositoryId, setupRepositoryOwnerId, setupBaseRef, setupPolicy, setupSolution, setupCapabilities, approveDisclosure, producerWorkflow, producerJobName, checkName, checkApp, artifactName, evidenceArtifactName, cadence, lease, renewal, dryRun, setupFormat, setupHelp }) setup.Options.Add(option);
        setup.SetAction(result => handler.ExecuteSetup(new BadgeSetupCommandOptions(
            result.GetValue(setupInput), result.GetValue(setupOutput), result.GetValue(setupRepository), result.GetValue(setupVisibility),
            result.GetValue(setupMode), result.GetValue(setupProfile), result.GetValue(setupAccount), result.GetValue(setupAlias), result.GetValue(setupEndpoint), result.GetValue(setupPlan),
            result.GetValue(cadence), result.GetValue(lease), result.GetValue(renewal), result.GetValue(dryRun), false, result.GetValue(setupFormat) ?? "json", result.GetValue(setupHelp),
            result.GetValue(setupRepositoryId), result.GetValue(setupRepositoryOwnerId), result.GetValue(setupBaseRef), result.GetValue(setupPolicy), result.GetValue(setupSolution), result.GetValue(setupCapabilities), null, result.GetValue(approveDisclosure),
            result.GetValue(producerWorkflow), result.GetValue(producerJobName), result.GetValue(checkName), result.GetValue(checkApp), result.GetValue(artifactName), result.GetValue(evidenceArtifactName), result.GetValue(setupAudience))));
        Command doctor = new("doctor", "Diagnose a versioned badge setup without writing.");
        Option<string> doctorInput = new(InputOptionName);
        Option<bool> publicDiagnostics = new("--public");
        Option<string> doctorFormat = new("--format") { DefaultValueFactory = _ => "json" };
        Option<string> doctorCapabilities = new("--capability-evidence");
        Option<string> doctorObservation = new("--observation");
        Option<bool> doctorHelp = new(HelpOptionName);
        doctorHelp.Aliases.Add("-h");
        foreach (Option option in new Option[] { doctorInput, publicDiagnostics, doctorFormat, doctorCapabilities, doctorObservation, doctorHelp }) doctor.Options.Add(option);
        doctor.SetAction(result => handler.ExecuteDoctor(new BadgeSetupCommandOptions(
            InputPath: result.GetValue(doctorInput),
            OutputDirectory: null,
            Repository: null,
            Visibility: null,
            Mode: null,
            DisclosureProfile: null,
            Account: null,
            Alias: null,
            Endpoint: null,
            ProviderPlan: null,
            CadenceMinutes: null,
            MaxLeaseMinutes: null,
            RenewalEnabled: false,
            DryRun: true,
            PublicDiagnostics: result.GetValue(publicDiagnostics),
            Format: result.GetValue(doctorFormat) ?? "json",
            ShowHelp: result.GetValue(doctorHelp),
            CapabilityEvidencePath: result.GetValue(doctorCapabilities),
            ObservationPath: result.GetValue(doctorObservation))));
        health.Subcommands.Add(setup);
        health.Subcommands.Add(doctor);
        badge.Subcommands.Add(policy);
        badge.Subcommands.Add(health);
        return badge;
    }
}
