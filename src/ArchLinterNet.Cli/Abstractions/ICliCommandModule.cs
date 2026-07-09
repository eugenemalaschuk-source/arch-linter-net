using System.CommandLine;

namespace ArchLinterNet.Cli.Abstractions;

internal interface IRootCliCommandModule
{
    RootCommand CreateRootCommand(ICliRuntime runtime, ICliConsole console, IFileSystem fileSystem);
}

internal interface ICliSubcommandModule
{
    string CommandName { get; }

    Command CreateCommand(ICliRuntime runtime, ICliConsole console, IFileSystem fileSystem);
}
