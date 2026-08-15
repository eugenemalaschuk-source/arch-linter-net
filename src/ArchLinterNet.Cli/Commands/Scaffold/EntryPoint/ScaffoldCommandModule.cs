using ArchLinterNet.Cli.Abstractions;

namespace ArchLinterNet.Cli.Commands.Scaffold.EntryPoint;

internal sealed class ScaffoldCommandModule : ITopLevelCliSubcommandModule
{
    public string CommandName => "scaffold";

    public System.CommandLine.Command CreateCommand(
        ICliRuntime runtime, ICliConsole console, IFileSystem fileSystem, CancellationToken cancellationToken = default)
    {
        return new ScaffoldCommandDefinition(console, fileSystem).Create();
    }
}
