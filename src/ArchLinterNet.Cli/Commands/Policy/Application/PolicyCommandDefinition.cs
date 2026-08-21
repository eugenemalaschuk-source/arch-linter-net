using System.CommandLine;

namespace ArchLinterNet.Cli.Commands.Policy.Application;

internal sealed class PolicyCommandDefinition(PolicyCheckCommandHandler checkHandler, PolicyContextCommandHandler contextHandler)
{
    public const string HelpText =
        """
        arch-linter-net policy — inspect or validate policy configuration

        Usage:
          arch-linter-net policy check --policy <path> [options]
          arch-linter-net policy context --policy <path> [options]

        `check` validates policy and static configuration without architecture analysis.
        `context` summarizes effective policy facts for coding agents without project or assembly analysis.

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

        policy.Subcommands.Add(check);
        policy.Subcommands.Add(context);
        return policy;
    }
}
