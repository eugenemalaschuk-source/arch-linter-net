using ArchLinterNet.Cli.Abstractions;

namespace ArchLinterNet.Cli.Commands.Coverage.EntryPoint;

internal sealed class CoverageCommandModule : ITopLevelCliSubcommandModule
{
    public string CommandName => "coverage";

    public System.CommandLine.Command CreateCommand(ICliRuntime runtime, ICliConsole console, IFileSystem fileSystem, CancellationToken cancellationToken = default) =>
        new Application.CoverageCommandDefinition(new Application.CoverageCommandHandler(console, fileSystem)).Create();
}
