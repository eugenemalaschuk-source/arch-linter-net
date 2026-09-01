using System.CommandLine;
using ArchLinterNet.Cli;

namespace ArchLinterNet.Cli.Commands.Health.Application;

internal sealed class HealthCommandDefinition(HealthCommandHandler handler)
{
    public Command Create()
    {
        Command command = new("health", "Project the canonical architecture-health/v1 summary.");
        ArchitectureAnalysisCommandOptionSet options = new();
        options.AddTo(command);
        command.SetAction(result => handler.Execute(options.Read(result)));
        return command;
    }
}
