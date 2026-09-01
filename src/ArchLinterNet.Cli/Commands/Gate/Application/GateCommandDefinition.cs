using System.CommandLine;
using ArchLinterNet.Cli;

namespace ArchLinterNet.Cli.Commands.Gate.Application;

internal sealed class GateCommandDefinition(GateCommandHandler handler)
{
    public Command Create()
    {
        Command command = new("gate", "Fail CI on new architecture debt and error-severity policy weakening.");
        ArchitectureAnalysisCommandOptionSet options = new();
        options.AddTo(command);
        command.SetAction(result => handler.Execute(options.Read(result)));
        return command;
    }
}
