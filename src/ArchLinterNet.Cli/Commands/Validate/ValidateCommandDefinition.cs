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
        string mode = ResolveMode(parseResult);
        string format = ResolveFormat(parseResult);

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

    private static string ResolveMode(ParseResult parseResult)
    {
        string mode = "strict";
        bool expectModeValue = false;

        foreach (string token in EnumerateTokenValues(parseResult))
        {
            if (expectModeValue)
            {
                expectModeValue = false;
                mode = NormalizeModeOrPreserve(token);
                continue;
            }

            if (IsOption(token, "--mode", "-m"))
            {
                expectModeValue = true;
                continue;
            }

            if (IsOption(token, "--strict"))
            {
                mode = "strict";
                continue;
            }

            if (IsOption(token, "--audit"))
            {
                mode = "audit";
            }
        }

        return mode;
    }

    private static string ResolveFormat(ParseResult parseResult)
    {
        string format = "human";
        bool expectFormatValue = false;

        foreach (string token in EnumerateTokenValues(parseResult))
        {
            if (expectFormatValue)
            {
                expectFormatValue = false;
                format = NormalizeFormatOrPreserve(token);
                continue;
            }

            if (IsOption(token, "--format", "-f"))
            {
                expectFormatValue = true;
                continue;
            }

            if (IsOption(token, "--json"))
            {
                format = "json";
            }
        }

        return format;
    }

    private static IEnumerable<string> EnumerateTokenValues(ParseResult parseResult)
    {
        return parseResult.Tokens.Select(static token => token.Value);
    }

    private static bool IsModeValue(string token)
    {
        return string.Equals(token, "strict", StringComparison.OrdinalIgnoreCase)
            || string.Equals(token, "audit", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeModeOrPreserve(string token)
    {
        if (string.Equals(token, "audit", StringComparison.OrdinalIgnoreCase))
        {
            return "audit";
        }

        if (string.Equals(token, "strict", StringComparison.OrdinalIgnoreCase))
        {
            return "strict";
        }

        return token;
    }

    private static bool IsFormatValue(string token)
    {
        return string.Equals(token, "human", StringComparison.OrdinalIgnoreCase)
            || string.Equals(token, "json", StringComparison.OrdinalIgnoreCase)
            || string.Equals(token, "sarif", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeFormatOrPreserve(string token)
    {
        if (string.Equals(token, "json", StringComparison.OrdinalIgnoreCase))
        {
            return "json";
        }

        if (string.Equals(token, "sarif", StringComparison.OrdinalIgnoreCase))
        {
            return "sarif";
        }

        if (string.Equals(token, "human", StringComparison.OrdinalIgnoreCase))
        {
            return "human";
        }

        return token;
    }

    private static bool IsOption(string token, params string[] names)
    {
        foreach (string name in names)
        {
            if (string.Equals(token, name, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static Option<string> CreateOption(string name, string defaultValue)
    {
        Option<string> option = new(name);
        option.DefaultValueFactory = _ => defaultValue;
        return option;
    }
}
