using System.CommandLine;

namespace ArchLinterNet.Cli.Commands.Policy;

internal sealed class PolicyCommandDefinition(PolicyCheckCommandHandler handler)
{
    public const string HelpText =
        """
        arch-linter-net policy check — validate policy and static configuration

        Usage:
          arch-linter-net policy check --policy <path> [options]

        This command does not invoke MSBuild, evaluate projects, load target assemblies,
        or claim that the architecture is clean.

        Options:
          -p, --policy <path>   Path to YAML contract file
                                (default: architecture/dependencies.arch.yml)
          -f, --format <fmt>    Output format: human, json, or sarif (default: human)
          -h, --help            Show this help message

        Exit codes:
          0   Policy and static configuration are valid; fact-dependent checks may be deferred
          2   Invalid arguments or policy/configuration error
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
        check.SetAction(parseResult => handler.Execute(new PolicyCheckCommandOptions(
            parseResult.GetValue(policyOption) ?? "architecture/dependencies.arch.yml",
            parseResult.GetValue(formatOption) ?? "human",
            parseResult.GetValue(helpOption))));
        policy.Subcommands.Add(check);
        return policy;
    }
}
