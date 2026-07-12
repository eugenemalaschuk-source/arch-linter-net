using System.CommandLine;
using ArchLinterNet.Cli.Abstractions;

namespace ArchLinterNet.Cli.Commands.Baseline.Abstractions;

internal interface IBaselineSubcommandModule : ICliSubcommandModule
{
}

internal interface IDefaultBaselineSubcommandModule : IBaselineSubcommandModule
{
    Command CreateDefaultCommand(string commandName, ICliRuntime runtime, ICliConsole console, IFileSystem fileSystem);
}
