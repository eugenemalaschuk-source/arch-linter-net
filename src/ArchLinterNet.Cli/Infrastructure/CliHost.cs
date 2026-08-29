using System.CommandLine;
using System.CommandLine.Parsing;
using ArchLinterNet.Cli.Abstractions;
using ArchLinterNet.Cli.Commands.Validate.Application;

namespace ArchLinterNet.Cli.Infrastructure;

internal sealed class CliHost(ICliRootCommandFactory rootCommandFactory, ICliConsole console, ICliRuntime runtime)
{
    public int Run(string[] args)
    {
        if (TryHandleLegacyValidateShortCircuit(args))
        {
            return CliExitCodes.Success;
        }

        Command rootCommand = rootCommandFactory.Create();
        ParseResult parseResult = rootCommand.Parse(args);
        if (parseResult.Errors.Count > 0 || parseResult.UnmatchedTokens.Count > 0)
        {
            WriteParseErrors(parseResult);
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }

        return parseResult.Invoke();
    }

    private bool TryHandleLegacyValidateShortCircuit(string[] args)
    {
        if (args.Length == 0)
        {
            return false;
        }

        int i = 0;
        while (i < args.Length)
        {
            string arg = args[i];
            switch (arg)
            {
                case "--help" or "-h":
                    console.Out.WriteLine(ValidateCommandDefinition.HelpText);
                    return true;
                case "--version" or "-v":
                    console.Out.WriteLine($"arch-linter-net {runtime.Version}");
                    return true;
                case "--policy" or "-p" or "--mode" or "-m" or "--format" or "-f" or "--contract" or "--condition-set" or "--baseline":
                    if (i + 1 >= args.Length)
                    {
                        return false;
                    }

                    i += 2;
                    continue;
                case "--strict" or "--audit" or "--json" or "--timings":
                    break;
                default:
                    return false;
            }

            i++;
        }

        return false;
    }

    private void WriteParseErrors(ParseResult parseResult)
    {
        var handledUnmatchedTokens = new HashSet<string>(StringComparer.Ordinal);
        foreach (ParseError error in parseResult.Errors)
        {
            string? unmatchedToken = parseResult.UnmatchedTokens.FirstOrDefault(token =>
                error.Message.Contains($"'{token}'", StringComparison.Ordinal));
            if (unmatchedToken is not null)
            {
                WriteUnknownToken(unmatchedToken);
                handledUnmatchedTokens.Add(unmatchedToken);
                continue;
            }

            console.Error.WriteLine(NormalizeErrorMessage(error.Message));
        }

        foreach (string token in parseResult.UnmatchedTokens.Where(token => !handledUnmatchedTokens.Contains(token)))
        {
            WriteUnknownToken(token);
        }

        console.Error.WriteLine(GetUsageHint(parseResult.CommandResult.Command.Name));
    }

    private void WriteUnknownToken(string token)
    {
        string kind = token.StartsWith("-", StringComparison.Ordinal)
            ? "option"
            : "command or argument";
        console.Error.WriteLine($"Unknown {kind}: {token}");
    }

    private static string NormalizeErrorMessage(string message)
    {
        const string UnrecognizedOptionPrefix = "Unrecognized option '";
        if (message.StartsWith(UnrecognizedOptionPrefix, StringComparison.Ordinal))
        {
            int start = UnrecognizedOptionPrefix.Length;
            int end = message.IndexOf('\'', start);
            if (end > start)
            {
                return $"Unknown option: {message[start..end]}";
            }
        }

        const string UnrecognizedCommandOrArgumentPrefix = "Unrecognized command or argument '";
        if (message.StartsWith(UnrecognizedCommandOrArgumentPrefix, StringComparison.Ordinal))
        {
            int start = UnrecognizedCommandOrArgumentPrefix.Length;
            int end = message.IndexOf('\'', start);
            if (end > start)
            {
                return $"Unknown option: {message[start..end]}";
            }
        }

        return message;
    }

    private static string? GetUsageHint(string commandName)
    {
        return commandName switch
        {
            "graph" => "Run 'arch-linter-net graph --help' for usage information.",
            "explain" => "Run 'arch-linter-net explain --help' for usage information.",
            "update" => "Run 'arch-linter-net baseline update --help' for usage information.",
            "prune" => "Run 'arch-linter-net baseline prune --help' for usage information.",
            "diff" => "Run 'arch-linter-net baseline diff --help' for usage information.",
            "verify" => "Run 'arch-linter-net baseline verify --help' for usage information.",
            "baseline" or "generate" => "Run 'arch-linter-net baseline --help' for usage information.",
            "public-api" or "capture" => "Run 'arch-linter-net public-api --help' for usage information.",
            _ => "Run with --help for usage information.",
        };
    }
}
