using System.CommandLine;

namespace ArchLinterNet.Cli.Commands.Policy.Application;

internal sealed class PolicyCommandDefinition(
    PolicyCheckCommandHandler checkHandler,
    PolicyContextCommandHandler contextHandler,
    PolicyWeakeningCommandHandler weakeningHandler)
{
    public const string HelpText =
        """
        arch-linter-net policy — inspect or validate policy configuration

        Usage:
          arch-linter-net policy check --policy <path> [options]
          arch-linter-net policy context --policy <path> [options]
          arch-linter-net policy weakening --base-context <path> --current-context <path> [options]

        `check` validates policy and static configuration without architecture analysis.
        `context` summarizes effective policy facts for coding agents without project or assembly analysis.
        `weakening` compares separately exported base/current policy contexts without policy loading or architecture analysis.

        Options:
          -h, --help            Show this help message
        """;

    public Command Create()
    {
        Command policy = new("policy");
        Command check = new("check");
        Option<string> policyOption = new("--policy");
        policyOption.Aliases.Add("-p");
        policyOption.DefaultValueFactory = _ => "architecture/dependencies.arch.yml";
        Option<string> formatOption = new("--format");
        formatOption.Aliases.Add("-f");
        formatOption.DefaultValueFactory = _ => "human";
        Option<bool> helpOption = new("--help");
        helpOption.Aliases.Add("-h");

        check.Options.Add(policyOption);
        check.Options.Add(formatOption);
        check.Options.Add(helpOption);
        check.SetAction(parseResult => checkHandler.Execute(new PolicyCheckCommandOptions(
            parseResult.GetValue(policyOption) ?? "architecture/dependencies.arch.yml",
            parseResult.GetValue(formatOption) ?? "human",
            parseResult.GetValue(helpOption))));

        Command context = new("context");
        Option<string> contextPolicyOption = new("--policy");
        contextPolicyOption.Aliases.Add("-p");
        contextPolicyOption.DefaultValueFactory = _ => "architecture/dependencies.arch.yml";
        Option<string> contextFormatOption = new("--format");
        contextFormatOption.Aliases.Add("-f");
        contextFormatOption.DefaultValueFactory = _ => "markdown";
        Option<bool> contextHelpOption = new("--help");
        contextHelpOption.Aliases.Add("-h");

        context.Options.Add(contextPolicyOption);
        context.Options.Add(contextFormatOption);
        context.Options.Add(contextHelpOption);
        context.SetAction(parseResult => contextHandler.Execute(new PolicyContextCommandOptions(
            parseResult.GetValue(contextPolicyOption) ?? "architecture/dependencies.arch.yml",
            parseResult.GetValue(contextFormatOption) ?? "markdown",
            parseResult.GetValue(contextHelpOption))));

        Command weakening = new("weakening");
        Option<string> baseContextOption = new("--base-context");
        Option<string> currentContextOption = new("--current-context");
        Option<string> weakeningFormatOption = new("--format");
        weakeningFormatOption.Aliases.Add("-f");
        weakeningFormatOption.DefaultValueFactory = _ => "human";
        Option<bool> weakeningHelpOption = new("--help");
        weakeningHelpOption.Aliases.Add("-h");

        weakening.Options.Add(baseContextOption);
        weakening.Options.Add(currentContextOption);
        weakening.Options.Add(weakeningFormatOption);
        weakening.Options.Add(weakeningHelpOption);
        weakening.SetAction(parseResult => weakeningHandler.Execute(new PolicyWeakeningCommandOptions(
            parseResult.GetValue(baseContextOption) ?? string.Empty,
            parseResult.GetValue(currentContextOption) ?? string.Empty,
            parseResult.GetValue(weakeningFormatOption) ?? "human",
            parseResult.GetValue(weakeningHelpOption))));

        policy.Subcommands.Add(check);
        policy.Subcommands.Add(context);
        policy.Subcommands.Add(weakening);
        return policy;
    }
}
