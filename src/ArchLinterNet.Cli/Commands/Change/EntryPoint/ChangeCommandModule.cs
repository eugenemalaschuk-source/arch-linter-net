using ArchLinterNet.Cli.Abstractions;
using ArchLinterNet.Cli.Commands.Change.Application;

namespace ArchLinterNet.Cli.Commands.Change.EntryPoint;

internal sealed class ChangeCommandModule : ITopLevelCliSubcommandModule
{
    public string CommandName => "change";

    public System.CommandLine.Command CreateCommand(
        ICliRuntime runtime, ICliConsole console, IFileSystem fileSystem, CancellationToken cancellationToken = default) =>
        new ChangeCommandDefinition(new ChangeCommandHandler(runtime, console, fileSystem)).Create();
}
