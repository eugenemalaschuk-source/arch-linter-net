using ArchLinterNet.Cli.Abstractions;

namespace ArchLinterNet.Cli.Commands.Badge.EntryPoint;

internal sealed class BadgeCommandModule : ITopLevelCliSubcommandModule
{
    public string CommandName => "badge";

    public System.CommandLine.Command CreateCommand(ICliRuntime runtime, ICliConsole console, IFileSystem fileSystem, CancellationToken cancellationToken = default) =>
        new Application.BadgeCommandDefinition(new Application.BadgeCommandHandler(console, fileSystem)).Create();
}
