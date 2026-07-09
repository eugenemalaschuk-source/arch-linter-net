using System.CommandLine;

namespace ArchLinterNet.Cli.Abstractions;

internal interface IRootCliCommandModule
{
    RootCommand CreateRootCommand();
}

internal interface ICliSubcommandModule
{
    string CommandName { get; }

    Command CreateCommand();
}
