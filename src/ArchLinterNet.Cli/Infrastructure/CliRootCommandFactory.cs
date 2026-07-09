using System.CommandLine;
using ArchLinterNet.Cli.Abstractions;

namespace ArchLinterNet.Cli.Infrastructure;

internal sealed class CliRootCommandFactory(
    IRootCliCommandModule rootCommandModule,
    IReadOnlyList<ICliSubcommandModule> subcommandModules) : ICliRootCommandFactory
{
    public Command Create()
    {
        RootCommand rootCommand = rootCommandModule.CreateRootCommand();
        foreach (ICliSubcommandModule module in subcommandModules.OrderBy(static module => module.CommandName, StringComparer.Ordinal))
        {
            rootCommand.Subcommands.Add(module.CreateCommand());
        }

        return rootCommand;
    }
}
