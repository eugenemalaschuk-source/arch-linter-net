using ArchLinterNet.Cli.Abstractions;

namespace ArchLinterNet.Cli.Commands.History.EntryPoint;

internal sealed class HistoryCommandModule : ITopLevelCliSubcommandModule
{
    public string CommandName => "history";

    public System.CommandLine.Command CreateCommand(ICliRuntime runtime, ICliConsole console, IFileSystem fileSystem, CancellationToken cancellationToken = default) =>
        new Application.HistoryCommandDefinition(new Application.HistoryIngestCommandHandler(console)).Create();
}
