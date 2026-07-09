using System.CommandLine;
using ArchLinterNet.Cli.Abstractions;

namespace ArchLinterNet.Cli.Infrastructure;

internal sealed class CliRootCommandFactory(
    IRootCliCommandModule rootCommandModule,
    IReadOnlyList<ITopLevelCliSubcommandModule> subcommandModules,
    ICliRuntime runtime,
    ICliConsole console,
    IFileSystem fileSystem) : ICliRootCommandFactory
{
    public Command Create()
    {
        RootCommand rootCommand = rootCommandModule.CreateRootCommand(runtime, console, fileSystem);
        foreach (ITopLevelCliSubcommandModule module in subcommandModules.OrderBy(static module => module.CommandName, StringComparer.Ordinal))
        {
            rootCommand.Subcommands.Add(module.CreateCommand(runtime, console, fileSystem));
        }

        return rootCommand;
    }
}
