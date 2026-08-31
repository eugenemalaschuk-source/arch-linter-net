using System.CommandLine;

namespace ArchLinterNet.Cli.Commands.Health.Application;

internal sealed class HealthCommandDefinition(HealthCommandHandler handler)
{
    public Command Create()
    {
        Command command = new("health", "Project the canonical architecture-health/v1 summary.");
        Option<string> policy = WithDefault("--policy", "architecture/dependencies.arch.yml");
        policy.Aliases.Add("-p");
        Option<string> baseline = new("--baseline");
        Option<string> mode = WithDefault("--mode", "all");
        mode.Aliases.Add("-m");
        Option<string> conditionSet = new("--condition-set");
        Option<string[]> contracts = new("--contract") { AllowMultipleArgumentsPerToken = true };
        Option<string> baseContext = new("--base-context");
        Option<string> currentContext = new("--current-context");
        Option<string> format = WithDefault("--format", "human");
        format.Aliases.Add("-f");
        Option<bool> ensureBuilt = new("--ensure-built");
        Option<bool> noRestore = new("--no-restore");
        Option<string> configuration = new("--configuration");
        Option<string> framework = new("--framework");
        Option<string> platform = new("--platform");
        Option<string> runtime = new("--runtime");
        Option<bool> help = new("--help");
        help.Aliases.Add("-h");
        command.Options.Add(policy);
        command.Options.Add(baseline);
        command.Options.Add(mode);
        command.Options.Add(conditionSet);
        command.Options.Add(contracts);
        command.Options.Add(baseContext);
        command.Options.Add(currentContext);
        command.Options.Add(format);
        command.Options.Add(ensureBuilt);
        command.Options.Add(noRestore);
        command.Options.Add(configuration);
        command.Options.Add(framework);
        command.Options.Add(platform);
        command.Options.Add(runtime);
        command.Options.Add(help);
        command.SetAction(result => handler.Execute(new HealthCommandOptions(
            result.GetValue(policy) ?? "architecture/dependencies.arch.yml",
            result.GetValue(baseline),
            result.GetValue(mode) ?? "all",
            result.GetValue(conditionSet),
            result.GetValue(format) ?? "human",
            result.GetValue(contracts) ?? Array.Empty<string>(),
            result.GetValue(baseContext),
            result.GetValue(currentContext),
            result.GetValue(help),
            result.GetValue(ensureBuilt),
            result.GetValue(noRestore),
            result.GetValue(configuration),
            result.GetValue(framework),
            result.GetValue(platform),
            result.GetValue(runtime))));
        return command;
    }

    private static Option<string> WithDefault(string name, string value)
    {
        Option<string> option = new(name);
        option.DefaultValueFactory = _ => value;
        return option;
    }
}
