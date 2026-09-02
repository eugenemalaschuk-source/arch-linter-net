using System.CommandLine;
using ArchLinterNet.Cli.Abstractions;
using ArchLinterNet.Cli.Commands.Topology.Abstractions;

namespace ArchLinterNet.Cli.Commands.Topology.EntryPoint;

internal sealed class TopologyCommandModule : ITopLevelCliSubcommandModule
{
    public string CommandName => "topology";

    public Command CreateCommand(
        ICliRuntime runtime,
        ICliConsole console,
        IFileSystem fileSystem,
        CancellationToken cancellationToken = default)
    {
        Command command = new(CommandName, "Capture, diff, and verify declared architecture topology.");
        foreach (ITopologySubcommandModule module in TopologySubcommandCatalog.CreateModules()
            .OrderBy(static module => module.CommandName, StringComparer.Ordinal))
        {
            command.Subcommands.Add(module.CreateCommand(runtime, console, fileSystem, cancellationToken));
        }

        return command;
    }
}
