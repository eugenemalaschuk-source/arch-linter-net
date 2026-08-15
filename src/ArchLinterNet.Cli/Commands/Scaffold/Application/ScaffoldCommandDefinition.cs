using System.CommandLine;
using ArchLinterNet.Cli.Abstractions;

namespace ArchLinterNet.Cli.Commands.Scaffold.Application;

internal sealed class ScaffoldCommandDefinition(ICliConsole console, IFileSystem fileSystem)
{
    public Command Create()
    {
        var command = new Command("scaffold");
        var cliCommand = new Command("cli-command");
        Option<string> moduleOption = new("--module");
        Option<string> commandOption = new("--command");
        Option<bool> dryRunOption = new("--dry-run");
        Option<bool> forceOption = new("--force");
        Option<string> modelOption = new("--model");
        Option<string> abstractionOption = new("--abstraction");
        Option<string> exceptionOption = new("--exception");

        cliCommand.Options.Add(moduleOption);
        cliCommand.Options.Add(commandOption);
        cliCommand.Options.Add(dryRunOption);
        cliCommand.Options.Add(forceOption);
        cliCommand.Options.Add(modelOption);
        cliCommand.Options.Add(abstractionOption);
        cliCommand.Options.Add(exceptionOption);
        cliCommand.SetAction(parseResult => new ScaffoldCliCommandHandler(console, fileSystem).Execute(
            new ScaffoldCliCommandOptions(
                "cli-command",
                parseResult.GetValue(moduleOption),
                parseResult.GetValue(commandOption),
                parseResult.GetValue(dryRunOption),
                parseResult.GetValue(forceOption),
                parseResult.GetValue(modelOption),
                parseResult.GetValue(abstractionOption),
                parseResult.GetValue(exceptionOption))));
        command.Subcommands.Add(cliCommand);
        return command;
    }
}
