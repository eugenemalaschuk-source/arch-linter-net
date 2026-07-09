using ArchLinterNet.Cli.Abstractions;

namespace ArchLinterNet.Cli.Commands.Explain;

internal sealed class ExplainCommandModule : ICliSubcommandModule
{
    public string CommandName => "explain";

    public System.CommandLine.Command CreateCommand(ICliRuntime runtime, ICliConsole console, IFileSystem fileSystem)
    {
        return new ExplainCommandDefinition(new ExplainCommandHandler(runtime, console, fileSystem)).Create();
    }
}
