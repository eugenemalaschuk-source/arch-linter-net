using System.CommandLine;

namespace ArchLinterNet.Cli.Commands.Cache.Application;

internal sealed class CacheCommandDefinition(CacheCommandHandler handler)
{
    public const string HelpText =
        """
        arch-linter-net cache — inspect or clear the persistent analysis-cache/v1

        Usage:
          arch-linter-net cache inspect --cache <auto|path>
          arch-linter-net cache clear --cache <auto|path>

        Options:
              --cache <dest>   "auto" for the platform user-cache namespace, or an
                                explicit directory (validated for safe containment).
                                Required — cache is disabled unless a location is given.
          -h, --help            Show this help message

        Exit codes:
          0   Operation completed
          2   Runtime error (invalid arguments, unsafe/unwritable location, etc.)
        """;

    public Command Create()
    {
        Command command = new("cache");

        Option<string> cacheOption = new("--cache");
        Option<bool> helpOption = new("--help");
        helpOption.Aliases.Add("-h");

        Command inspectCommand = new("inspect") { cacheOption, helpOption };
        inspectCommand.SetAction(parseResult => handler.Inspect(
            parseResult.GetValue(cacheOption), parseResult.GetValue(helpOption)));

        Command clearCommand = new("clear") { cacheOption, helpOption };
        clearCommand.SetAction(parseResult => handler.Clear(
            parseResult.GetValue(cacheOption), parseResult.GetValue(helpOption)));

        command.Subcommands.Add(inspectCommand);
        command.Subcommands.Add(clearCommand);
        command.Options.Add(helpOption);
        command.SetAction(_ => handler.ShowHelp());

        return command;
    }
}
