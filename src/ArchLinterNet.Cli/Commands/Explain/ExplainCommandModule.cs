using ArchLinterNet.Cli.Abstractions;

namespace ArchLinterNet.Cli.Commands.Explain;

internal sealed class ExplainCommandModule : ICliSubcommandModule
{
    private readonly ExplainCommandDefinition _definition;

    public ExplainCommandModule(ICliRuntime runtime, ICliConsole console, IFileSystem fileSystem)
    {
        _definition = new ExplainCommandDefinition(new ExplainCommandHandler(runtime, console, fileSystem));
    }

    public string CommandName => "explain";

    public System.CommandLine.Command CreateCommand()
    {
        return _definition.Create();
    }
}
