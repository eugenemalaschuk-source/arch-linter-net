using System.CommandLine;
using ArchLinterNet.Cli.Abstractions;

namespace ArchLinterNet.Cli.Commands.Baseline;

internal interface IBaselineSubcommandModule : ICliSubcommandModule
{
}

internal interface IDefaultBaselineSubcommandModule : IBaselineSubcommandModule
{
    Command CreateDefaultCommand(string commandName, ICliRuntime runtime, ICliConsole console, IFileSystem fileSystem);
}
