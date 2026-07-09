using System.CommandLine;
using ParseResult = System.CommandLine.ParseResult;

namespace ArchLinterNet.Cli.Commands.Baseline;

internal sealed class BaselineCommandDefinition(
    BaselineGenerateCommandHandler generateHandler,
    BaselineUpdateCommandHandler updateHandler,
    BaselinePruneCommandHandler pruneHandler,
    BaselineDiffCommandHandler diffHandler,
    BaselineVerifyCommandHandler verifyHandler)
{
    public const string HelpText =
        """
        arch-linter-net baseline — manage baseline lifecycle commands

        Usage:
          arch-linter-net baseline generate --config <path> --output <path> [options]
          arch-linter-net baseline update --config <path> --baseline <path> --output <path> [options]
          arch-linter-net baseline prune --config <path> --baseline <path> --output <path> [options]
          arch-linter-net baseline diff --config <path> --baseline <path> [options]
          arch-linter-net baseline verify --config <path> --baseline <path> [options]

        Options:
          --policy, --config <path>
                              Path to YAML contract file
                              (default: architecture/dependencies.arch.yml)
          --output <path>     Path to write the generated baseline file (required)
          --mode <mode>       Contract mode: strict, audit, or all (default: all)
          --reason <text>     Reason text for baseline entries
                              (default: "generated baseline")
          --contract <id>     Restrict to this contract ID (may be repeated)
          --condition-set <name>
                              Use a named condition set from analysis.condition_sets
                              to control conditional compilation symbols during
                              Roslyn source analysis (default: policy
                              default_condition_set, otherwise empty symbol set)
          -h, --help          Show this help message

        Subcommands:
          generate   Generate a fresh baseline from current violations
          update     Update an existing baseline, preserving valid entries
          prune      Remove stale/resolved entries from an existing baseline
          diff       Report new/existing/resolved/configuration-error entries
          verify     Exit non-zero if the baseline is out of sync (CI gate)

        Run 'arch-linter-net baseline <subcommand> --help' for subcommand-specific options.

        Exit codes:
          0   Command completed successfully
          1   Baseline verify found resolved entries or configuration errors
          2   Runtime error (invalid arguments, file not found, config violations, etc.)
        """;

    public const string UpdateHelpText =
        """
        arch-linter-net baseline update — update a baseline while preserving valid entries

        Usage:
          arch-linter-net baseline update --config <path> --baseline <path> --output <path> [options]

        Options:
          --policy, --config <path>
                              Path to YAML contract file
                              (default: architecture/dependencies.arch.yml)
          --baseline <path>   Path to the existing baseline file to update (required)
          --output <path>     Path to write the updated baseline file (required)
          --mode <mode>       Contract mode: strict, audit, or all (default: all)
          --reason <text>     Reason text for newly added entries
                              (default: "generated baseline")
          --contract <id>     Restrict to this contract ID (may be repeated)
          --condition-set <name>
                              Use a named condition set from analysis.condition_sets
          -h, --help          Show this help message

        Exit codes:
          0   Baseline updated successfully
          2   Runtime error (invalid arguments, file not found, config violations, etc.)
        """;

    public const string PruneHelpText =
        """
        arch-linter-net baseline prune — remove stale entries from a baseline

        Usage:
          arch-linter-net baseline prune --config <path> --baseline <path> --output <path> [options]

        Options:
          --policy, --config <path>
                              Path to YAML contract file
                              (default: architecture/dependencies.arch.yml)
          --baseline <path>   Path to the existing baseline file to prune (required)
          --output <path>     Path to write the pruned baseline file (required)
          --mode <mode>       Contract mode: strict, audit, or all (default: all)
          --contract <id>     Restrict to this contract ID (may be repeated)
          --condition-set <name>
                              Use a named condition set from analysis.condition_sets
          --json              Report removed entries as JSON
          -h, --help          Show this help message

        Exit codes:
          0   Baseline pruned successfully
          2   Runtime error (invalid arguments, file not found, config violations, etc.)
        """;

    public const string DiffHelpText =
        """
        arch-linter-net baseline diff — compare a baseline against current violations

        Usage:
          arch-linter-net baseline diff --config <path> --baseline <path> [options]

        Options:
          --policy, --config <path>
                              Path to YAML contract file
                              (default: architecture/dependencies.arch.yml)
          --baseline <path>   Path to the baseline file to diff against (required)
          --mode <mode>       Contract mode: strict, audit, or all (default: all)
          --contract <id>     Restrict to this contract ID (may be repeated)
          --condition-set <name>
                              Use a named condition set from analysis.condition_sets
          --json               Output the diff as JSON
          -h, --help          Show this help message

        Exit codes:
          0   Diff produced successfully (regardless of drift found)
          2   Runtime error (invalid arguments, file not found, config violations, etc.)
        """;

    public const string VerifyHelpText =
        """
        arch-linter-net baseline verify — verify a baseline is in sync (CI gate)

        Usage:
          arch-linter-net baseline verify --config <path> --baseline <path> [options]

        Options:
          --policy, --config <path>
                              Path to YAML contract file
                              (default: architecture/dependencies.arch.yml)
          --baseline <path>   Path to the baseline file to verify (required)
          --mode <mode>       Contract mode: strict, audit, or all (default: all)
          --contract <id>     Restrict to this contract ID (may be repeated)
          --condition-set <name>
                              Use a named condition set from analysis.condition_sets
          --json               Output the verification report as JSON
          -h, --help          Show this help message

        Exit codes:
          0   Baseline is in sync
          1   Baseline is out of sync (resolved entries or configuration errors found)
          2   Runtime error (invalid arguments, file not found, config violations, etc.)
        """;

    public Command Create()
    {
        Command baselineCommand = CreateGenerateCommand("baseline", true);
        baselineCommand.Subcommands.Add(CreateGenerateCommand("generate", false));
        baselineCommand.Subcommands.Add(CreateUpdateCommand());
        baselineCommand.Subcommands.Add(CreatePruneCommand());
        baselineCommand.Subcommands.Add(CreateDiffCommand());
        baselineCommand.Subcommands.Add(CreateVerifyCommand());
        return baselineCommand;
    }

    private Command CreateGenerateCommand(string name, bool familyHelp)
    {
        Command command = new(name);
        Option<string> policyOption = CreatePolicyOption();
        Option<string> outputOption = new("--output");
        Option<string> reasonOption = new("--reason");
        reasonOption.DefaultValueFactory = _ => "generated baseline";
        Option<string> modeOption = CreateModeOption();
        Option<string> conditionSetOption = new("--condition-set");
        Option<string[]> contractOption = new("--contract");
        Option<bool> helpOption = new("--help");
        helpOption.Aliases.Add("-h");

        command.Options.Add(policyOption);
        command.Options.Add(outputOption);
        command.Options.Add(reasonOption);
        command.Options.Add(modeOption);
        command.Options.Add(conditionSetOption);
        command.Options.Add(contractOption);
        command.Options.Add(helpOption);

        command.SetAction(parseResult => generateHandler.Execute(new BaselineGenerateCommandOptions(
            GetPolicyPath(parseResult, policyOption),
            parseResult.GetValue(outputOption),
            parseResult.GetValue(reasonOption) ?? "generated baseline",
            parseResult.GetValue(modeOption) ?? "all",
            parseResult.GetValue(conditionSetOption),
            parseResult.GetValue(contractOption) ?? Array.Empty<string>(),
            parseResult.GetValue(helpOption))));

        return command;
    }

    private Command CreateUpdateCommand()
    {
        Command command = new("update");
        Option<string> policyOption = CreatePolicyOption();
        Option<string> baselineOption = new("--baseline");
        Option<string> outputOption = new("--output");
        Option<string> reasonOption = new("--reason");
        reasonOption.DefaultValueFactory = _ => "generated baseline";
        Option<string> modeOption = CreateModeOption();
        Option<string> conditionSetOption = new("--condition-set");
        Option<string[]> contractOption = new("--contract");
        Option<bool> helpOption = new("--help");
        helpOption.Aliases.Add("-h");

        command.Options.Add(policyOption);
        command.Options.Add(baselineOption);
        command.Options.Add(outputOption);
        command.Options.Add(reasonOption);
        command.Options.Add(modeOption);
        command.Options.Add(conditionSetOption);
        command.Options.Add(contractOption);
        command.Options.Add(helpOption);

        command.SetAction(parseResult => updateHandler.Execute(new BaselineUpdateCommandOptions(
            GetPolicyPath(parseResult, policyOption),
            parseResult.GetValue(baselineOption),
            parseResult.GetValue(outputOption),
            parseResult.GetValue(reasonOption) ?? "generated baseline",
            parseResult.GetValue(modeOption) ?? "all",
            parseResult.GetValue(conditionSetOption),
            parseResult.GetValue(contractOption) ?? Array.Empty<string>(),
            parseResult.GetValue(helpOption))));

        return command;
    }

    private Command CreatePruneCommand()
    {
        Command command = new("prune");
        Option<string> policyOption = CreatePolicyOption();
        Option<string> baselineOption = new("--baseline");
        Option<string> outputOption = new("--output");
        Option<string> modeOption = CreateModeOption();
        Option<string> conditionSetOption = new("--condition-set");
        Option<string[]> contractOption = new("--contract");
        Option<bool> jsonOption = new("--json");
        Option<bool> shortFormatOption = new("-f");
        Option<bool> helpOption = new("--help");
        helpOption.Aliases.Add("-h");

        command.Options.Add(policyOption);
        command.Options.Add(baselineOption);
        command.Options.Add(outputOption);
        command.Options.Add(modeOption);
        command.Options.Add(conditionSetOption);
        command.Options.Add(contractOption);
        command.Options.Add(jsonOption);
        command.Options.Add(shortFormatOption);
        command.Options.Add(helpOption);

        command.SetAction(parseResult => pruneHandler.Execute(new BaselinePruneCommandOptions(
            GetPolicyPath(parseResult, policyOption),
            parseResult.GetValue(baselineOption),
            parseResult.GetValue(outputOption),
            parseResult.GetValue(modeOption) ?? "all",
            parseResult.GetValue(conditionSetOption),
            parseResult.GetValue(jsonOption) || parseResult.GetValue(shortFormatOption) ? "json" : "human",
            parseResult.GetValue(contractOption) ?? Array.Empty<string>(),
            parseResult.GetValue(helpOption))));

        return command;
    }

    private Command CreateDiffCommand()
    {
        Command command = new("diff");
        Option<string> policyOption = CreatePolicyOption();
        Option<string> baselineOption = new("--baseline");
        Option<string> modeOption = CreateModeOption();
        Option<string> conditionSetOption = new("--condition-set");
        Option<string[]> contractOption = new("--contract");
        Option<bool> jsonOption = new("--json");
        Option<bool> helpOption = new("--help");
        helpOption.Aliases.Add("-h");

        command.Options.Add(policyOption);
        command.Options.Add(baselineOption);
        command.Options.Add(modeOption);
        command.Options.Add(conditionSetOption);
        command.Options.Add(contractOption);
        command.Options.Add(jsonOption);
        command.Options.Add(helpOption);

        command.SetAction(parseResult => diffHandler.Execute(new BaselineDiffCommandOptions(
            GetPolicyPath(parseResult, policyOption),
            parseResult.GetValue(baselineOption),
            parseResult.GetValue(modeOption) ?? "all",
            parseResult.GetValue(conditionSetOption),
            parseResult.GetValue(jsonOption) ? "json" : "human",
            parseResult.GetValue(contractOption) ?? Array.Empty<string>(),
            parseResult.GetValue(helpOption))));

        return command;
    }

    private Command CreateVerifyCommand()
    {
        Command command = new("verify");
        Option<string> policyOption = CreatePolicyOption();
        Option<string> baselineOption = new("--baseline");
        Option<string> modeOption = CreateModeOption();
        Option<string> conditionSetOption = new("--condition-set");
        Option<string[]> contractOption = new("--contract");
        Option<bool> jsonOption = new("--json");
        Option<bool> helpOption = new("--help");
        helpOption.Aliases.Add("-h");

        command.Options.Add(policyOption);
        command.Options.Add(baselineOption);
        command.Options.Add(modeOption);
        command.Options.Add(conditionSetOption);
        command.Options.Add(contractOption);
        command.Options.Add(jsonOption);
        command.Options.Add(helpOption);

        command.SetAction(parseResult => verifyHandler.Execute(new BaselineVerifyCommandOptions(
            GetPolicyPath(parseResult, policyOption),
            parseResult.GetValue(baselineOption),
            parseResult.GetValue(modeOption) ?? "all",
            parseResult.GetValue(conditionSetOption),
            parseResult.GetValue(jsonOption) ? "json" : "human",
            parseResult.GetValue(contractOption) ?? Array.Empty<string>(),
            parseResult.GetValue(helpOption))));

        return command;
    }

    private static Option<string> CreatePolicyOption()
    {
        Option<string> option = new("--policy");
        option.DefaultValueFactory = _ => "architecture/dependencies.arch.yml";
        option.Aliases.Add("--config");
        return option;
    }

    private static Option<string> CreateModeOption()
    {
        Option<string> option = new("--mode");
        option.DefaultValueFactory = _ => "all";
        option.Aliases.Add("-m");
        return option;
    }

    private static string GetPolicyPath(ParseResult parseResult, Option<string> policyOption)
    {
        return parseResult.GetValue(policyOption) ?? "architecture/dependencies.arch.yml";
    }
}
