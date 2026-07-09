using ArchLinterNet.Cli.Abstractions;

namespace ArchLinterNet.Cli.Commands.Graph;

internal sealed class GraphCommandModule : ICliSubcommandModule
{
    public string CommandName => "graph";

    public System.CommandLine.Command CreateCommand(ICliRuntime runtime, ICliConsole console, IFileSystem fileSystem)
    {
        return new GraphCommandDefinition(new GraphCommandHandler(runtime, console, fileSystem)).Create();
    }
}
