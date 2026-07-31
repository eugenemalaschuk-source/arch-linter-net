using System.CommandLine;

namespace ArchLinterNet.Cli.Abstractions;

internal interface IRootCliCommandModule
{
    RootCommand CreateRootCommand(
        ICliRuntime runtime, ICliConsole console, IFileSystem fileSystem, CancellationToken cancellationToken = default);
}

internal interface ICliSubcommandModule
{
    string CommandName { get; }

    Command CreateCommand(ICliRuntime runtime, ICliConsole console, IFileSystem fileSystem);
}

internal interface ITopLevelCliSubcommandModule : ICliSubcommandModule
{
}
