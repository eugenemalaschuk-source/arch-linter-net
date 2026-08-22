using ArchLinterNet.Cli.Abstractions;
using ArchLinterNet.Cli.Commands.Gate.Application;

namespace ArchLinterNet.Cli.Commands.Gate.EntryPoint;

internal sealed class GateCommandModule : ITopLevelCliSubcommandModule
{
    public string CommandName => "gate";

    public System.CommandLine.Command CreateCommand(
        ICliRuntime runtime, ICliConsole console, IFileSystem fileSystem, CancellationToken cancellationToken = default) =>
        new GateCommandDefinition(new GateCommandHandler(runtime, console, fileSystem, cancellationToken)).Create();
}
