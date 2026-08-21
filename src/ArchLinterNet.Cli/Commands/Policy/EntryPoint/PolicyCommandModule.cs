using ArchLinterNet.Cli.Abstractions;

namespace ArchLinterNet.Cli.Commands.Policy.EntryPoint;

internal sealed class PolicyCommandModule : ITopLevelCliSubcommandModule
{
    public string CommandName => "policy";

    public System.CommandLine.Command CreateCommand(ICliRuntime runtime, ICliConsole console, IFileSystem fileSystem, CancellationToken cancellationToken = default)
    {
        return new PolicyCommandDefinition(
            new PolicyCheckCommandHandler(console),
            new PolicyContextCommandHandler(runtime, console),
            new PolicyWeakeningCommandHandler(runtime, console, fileSystem)).Create();
    }
}
