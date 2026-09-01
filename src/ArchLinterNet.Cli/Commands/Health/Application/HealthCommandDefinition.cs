using System.CommandLine;
using ArchLinterNet.Cli;

namespace ArchLinterNet.Cli.Commands.Health.Application;

internal sealed class HealthCommandDefinition(HealthCommandHandler handler)
{
    public Command Create()
    {
        Command command = new("health", "Project the canonical architecture-health/v1 summary.");
        ArchitectureAnalysisCommandOptionSet options = new();
        Option<string> executionContext = new("--execution-context");
        options.AddTo(command);
        command.Options.Add(executionContext);
        command.SetAction(result => handler.Execute(options.Read(result), result.GetValue(executionContext)));
        return command;
    }
}
