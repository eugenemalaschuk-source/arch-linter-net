using ArchLinterNet.Cli.Abstractions;
using ArchLinterNet.Cli.Commands.Measure.Application;

namespace ArchLinterNet.Cli.Commands.Measure.EntryPoint;

internal sealed class MeasureCommandModule : ITopLevelCliSubcommandModule
{
    public string CommandName => "measure";

    public System.CommandLine.Command CreateCommand(
        ICliRuntime runtime,
        ICliConsole console,
        IFileSystem fileSystem,
        CancellationToken cancellationToken = default) =>
        new MeasureCommandDefinition(
            new MeasureCommandHandler(runtime, console, cancellationToken)).Create();
}
