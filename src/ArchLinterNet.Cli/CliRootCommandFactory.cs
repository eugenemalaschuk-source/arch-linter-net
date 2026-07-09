using System.CommandLine;
using ArchLinterNet.Cli.Commands.Baseline;
using ArchLinterNet.Cli.Commands.Explain;
using ArchLinterNet.Cli.Commands.Graph;
using ArchLinterNet.Cli.Commands.Validate;

namespace ArchLinterNet.Cli;

internal sealed class CliRootCommandFactory(
    ValidateCommandDefinition validateCommandDefinition,
    BaselineCommandDefinition baselineCommandDefinition,
    GraphCommandDefinition graphCommandDefinition,
    ExplainCommandDefinition explainCommandDefinition) : ICliRootCommandFactory
{
    public Command Create()
    {
        Command rootCommand = validateCommandDefinition.CreateRootCommand();
        rootCommand.Subcommands.Add(baselineCommandDefinition.Create());
        rootCommand.Subcommands.Add(graphCommandDefinition.Create());
        rootCommand.Subcommands.Add(explainCommandDefinition.Create());
        return rootCommand;
    }
}
