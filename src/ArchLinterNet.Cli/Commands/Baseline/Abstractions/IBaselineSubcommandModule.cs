using System.CommandLine;
using ArchLinterNet.Cli.Abstractions;

namespace ArchLinterNet.Cli.Commands.Baseline;

internal interface IBaselineSubcommandModule
{
    string CommandName { get; }

    Command CreateCommand(ICliRuntime runtime, ICliConsole console, IFileSystem fileSystem);
}

internal interface IDefaultBaselineSubcommandModule : IBaselineSubcommandModule
{
    Command CreateDefaultCommand(string commandName, ICliRuntime runtime, ICliConsole console, IFileSystem fileSystem);
}
