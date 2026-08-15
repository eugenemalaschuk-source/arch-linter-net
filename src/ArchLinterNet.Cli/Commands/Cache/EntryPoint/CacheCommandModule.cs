using ArchLinterNet.Cli.Abstractions;

namespace ArchLinterNet.Cli.Commands.Cache.EntryPoint;

internal sealed class CacheCommandModule : ITopLevelCliSubcommandModule
{
    public string CommandName => "cache";

    public System.CommandLine.Command CreateCommand(
        ICliRuntime runtime, ICliConsole console, IFileSystem fileSystem, CancellationToken cancellationToken = default)
    {
        return new CacheCommandDefinition(new CacheCommandHandler(console)).Create();
    }
}
