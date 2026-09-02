using System.CommandLine;
using ArchLinterNet.Cli.Abstractions;

namespace ArchLinterNet.Cli.Commands.Topology.Abstractions;

internal interface ITopologySubcommandModule
{
    string CommandName { get; }

    Command CreateCommand(
        ICliRuntime runtime,
        ICliConsole console,
        IFileSystem fileSystem,
        CancellationToken cancellationToken = default);
}
