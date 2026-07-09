using System.CommandLine;
using System.CommandLine.Parsing;

namespace ArchLinterNet.Cli.Commands.Validate;

internal sealed class ValidateCommandDefinition(ValidateCommandHandler handler)
{
    public const string HelpText =
        """
        arch-linter-net — architecture contract linter for .NET

        Usage:
          arch-linter-net [options]
          arch-linter-net baseline generate --config <path> --output <path> [options]
          arch-linter-net graph [options]
          arch-linter-net explain --source <id> --target <id> [options]

        Validate Options:
          -p, --policy <path>   Path to YAML contract file
                                (default: architecture/dependencies.arch.yml)
          -m, --mode <mode>     Validation mode: strict or audit (default: strict)
              --strict          Shortcut for --mode strict
              --audit           Shortcut for --mode audit
              --contract <id>   Run only the contract with the given ID (may be repeated)
              --condition-set <name>
                                Use a named condition set from analysis.condition_sets
                                to control conditional compilation symbols during
                                Roslyn source analysis (default: policy default_condition_set,
                                otherwise empty symbol set)
              --baseline <path> Path to baseline file to merge with policy ignores
              --timings         Print phase-level timing report to stderr
          -f, --format <fmt>    Output format: human, json, or sarif (default: human)
                                sarif covers violations and cycles only; coverage,
                                unmatched-ignore, and policy-consistency findings can
                                still fail the run (exit code 1) without appearing in
                                SARIF results — use --format json to see those
              --json            Shortcut for --format json
          -h, --help            Show this help message
          -v, --version         Show version

        Exit codes:
          0   All contracts passed
          1   One or more contracts failed
          2   Runtime error (invalid arguments, file not found, etc.)
        """;

    public RootCommand CreateRootCommand()
    {
        RootCommand command = new("arch-linter-net");
        RemoveBuiltInRootOptions(command);

        Option<string> policyOption = CreateOption("--policy", "architecture/dependencies.arch.yml");
        policyOption.Aliases.Add("-p");

        Option<string> modeOption = CreateOption("--mode", "strict");
        modeOption.Aliases.Add("-m");

        Option<string> formatOption = CreateOption("--format", "human");
        formatOption.Aliases.Add("-f");

        Option<string[]> contractOption = new("--contract");
        Option<string> conditionSetOption = new("--condition-set");
        Option<string> baselineOption = new("--baseline");
        Option<bool> strictOption = new("--strict");
        Option<bool> auditOption = new("--audit");
        Option<bool> jsonOption = new("--json");
        Option<bool> timingsOption = new("--timings");
        Option<bool> helpOption = new("--help");
        helpOption.Aliases.Add("-h");
        Option<bool> versionOption = new("--version");
        versionOption.Aliases.Add("-v");

        command.Options.Add(policyOption);
        command.Options.Add(modeOption);
        command.Options.Add(formatOption);
        command.Options.Add(contractOption);
        command.Options.Add(conditionSetOption);
        command.Options.Add(baselineOption);
        command.Options.Add(strictOption);
        command.Options.Add(auditOption);
        command.Options.Add(jsonOption);
        command.Options.Add(timingsOption);
        command.Options.Add(helpOption);
        command.Options.Add(versionOption);

        command.SetAction(parseResult => handler.Execute(MapOptions(
            parseResult,
            policyOption,
            modeOption,
            formatOption,
            contractOption,
            conditionSetOption,
            baselineOption,
            strictOption,
            auditOption,
            jsonOption,
            timingsOption,
            helpOption,
            versionOption)));

        return command;
    }

    private static void RemoveBuiltInRootOptions(RootCommand command)
    {
        Option? helpOption = command.Options.SingleOrDefault(static option => option.Name == "help");
        if (helpOption is not null)
        {
            command.Options.Remove(helpOption);
        }

        Option? versionOption = command.Options.SingleOrDefault(static option => option.Name == "version");
        if (versionOption is not null)
        {
            command.Options.Remove(versionOption);
        }
    }

    private static ValidateCommandOptions MapOptions(
        ParseResult parseResult,
        Option<string> policyOption,
        Option<string> modeOption,
        Option<string> formatOption,
        Option<string[]> contractOption,
        Option<string> conditionSetOption,
        Option<string> baselineOption,
        Option<bool> strictOption,
        Option<bool> auditOption,
        Option<bool> jsonOption,
        Option<bool> timingsOption,
        Option<bool> helpOption,
        Option<bool> versionOption)
    {
        string mode = parseResult.GetValue(modeOption) ?? "strict";
        if (parseResult.GetValue(strictOption))
        {
            mode = "strict";
        }
        else if (parseResult.GetValue(auditOption))
        {
            mode = "audit";
        }

        string format = parseResult.GetValue(formatOption) ?? "human";
        if (parseResult.GetValue(jsonOption))
        {
            format = "json";
        }

        return new ValidateCommandOptions(
            parseResult.GetValue(policyOption) ?? "architecture/dependencies.arch.yml",
            mode,
            format,
            parseResult.GetValue(contractOption) ?? Array.Empty<string>(),
            parseResult.GetValue(conditionSetOption),
            parseResult.GetValue(timingsOption),
            parseResult.GetValue(baselineOption),
            parseResult.GetValue(helpOption),
            parseResult.GetValue(versionOption));
    }

    private static Option<string> CreateOption(string name, string defaultValue)
    {
        Option<string> option = new(name);
        option.DefaultValueFactory = _ => defaultValue;
        return option;
    }
}
