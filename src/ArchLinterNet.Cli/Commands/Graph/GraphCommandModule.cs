using ArchLinterNet.Cli.Abstractions;

namespace ArchLinterNet.Cli.Commands.Graph;

internal sealed class GraphCommandModule : ICliSubcommandModule
{
    private readonly GraphCommandDefinition _definition;

    public GraphCommandModule(ICliRuntime runtime, ICliConsole console, IFileSystem fileSystem)
    {
        _definition = new GraphCommandDefinition(new GraphCommandHandler(runtime, console, fileSystem));
    }

    public string CommandName => "graph";

    public System.CommandLine.Command CreateCommand()
    {
        return _definition.Create();
    }
}
