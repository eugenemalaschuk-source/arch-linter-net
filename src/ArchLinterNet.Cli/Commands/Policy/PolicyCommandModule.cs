using ArchLinterNet.Cli.Abstractions;

namespace ArchLinterNet.Cli.Commands.Policy;

internal sealed class PolicyCommandModule : ITopLevelCliSubcommandModule
{
    public string CommandName => "policy";

    public System.CommandLine.Command CreateCommand(ICliRuntime runtime, ICliConsole console, IFileSystem fileSystem)
    {
        return new PolicyCommandDefinition(new PolicyCheckCommandHandler(console)).Create();
    }
}
