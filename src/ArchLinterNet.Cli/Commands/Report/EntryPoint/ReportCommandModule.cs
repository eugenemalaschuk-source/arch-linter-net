using ArchLinterNet.Cli.Abstractions;
using ArchLinterNet.Cli.Commands.Report.Application;

namespace ArchLinterNet.Cli.Commands.Report.EntryPoint;

/// <summary>Discovers the native report command without a central command registry.</summary>
internal sealed class ReportCommandModule : ITopLevelCliSubcommandModule
{
    public string CommandName => "report";

    public System.CommandLine.Command CreateCommand(
        ICliRuntime runtime,
        ICliConsole console,
        IFileSystem fileSystem,
        CancellationToken cancellationToken = default) =>
        new ReportCommandDefinition(new ReportCommandHandler(console, fileSystem)).Create();
}
