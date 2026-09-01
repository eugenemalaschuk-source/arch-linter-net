using ArchLinterNet.Cli.Abstractions;
using ArchLinterNet.Cli.Commands.Health.Application;

namespace ArchLinterNet.Cli.Commands.Health.EntryPoint;

internal sealed class HealthCommandModule : ITopLevelCliSubcommandModule
{
    public string CommandName => "health";

    public System.CommandLine.Command CreateCommand(
        ICliRuntime runtime,
        ICliConsole console,
        IFileSystem fileSystem,
        CancellationToken cancellationToken = default) =>
        new HealthCommandDefinition(new HealthCommandHandler(runtime, console, fileSystem, cancellationToken)).Create();
}
