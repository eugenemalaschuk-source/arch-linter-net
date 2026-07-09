using System.CommandLine;

namespace ArchLinterNet.Cli.Commands.Baseline;

internal interface IBaselineSubcommandModule
{
    string CommandName { get; }

    Command CreateCommand();
}

internal interface IDefaultBaselineSubcommandModule : IBaselineSubcommandModule
{
    Command CreateDefaultCommand(string commandName);
}
