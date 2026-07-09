using System.CommandLine;
using System.CommandLine.Parsing;

namespace ArchLinterNet.Cli;

internal sealed class CliHost(ICliRootCommandFactory rootCommandFactory, ICliConsole console)
{
    public int Run(string[] args)
    {
        Command rootCommand = rootCommandFactory.Create();
        ParseResult parseResult = rootCommand.Parse(args);
        if (parseResult.Errors.Count > 0)
        {
            WriteParseErrors(parseResult);
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }

        return parseResult.Invoke();
    }

    private void WriteParseErrors(ParseResult parseResult)
    {
        foreach (ParseError error in parseResult.Errors)
        {
            console.Error.WriteLine(NormalizeErrorMessage(error.Message));
        }

        string? usageHint = GetUsageHint(parseResult.CommandResult.Command.Name);
        if (!string.IsNullOrEmpty(usageHint))
        {
            console.Error.WriteLine(usageHint);
        }
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
            _ => "Run with --help for usage information.",
        };
    }
}
