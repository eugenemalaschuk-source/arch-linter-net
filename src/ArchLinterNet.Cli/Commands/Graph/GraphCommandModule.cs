using ArchLinterNet.Cli.Abstractions;

namespace ArchLinterNet.Cli.Commands.Graph;

internal sealed class GraphCommandModule : ITopLevelCliSubcommandModule
{
    public string CommandName => "graph";

    public System.CommandLine.Command CreateCommand(ICliRuntime runtime, ICliConsole console, IFileSystem fileSystem)
    {
        return new GraphCommandDefinition(new GraphCommandHandler(runtime, console)).Create();
    }
}
