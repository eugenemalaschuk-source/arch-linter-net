using ArchLinterNet.Cli.Abstractions;
using ArchLinterNet.Core.Schema;

namespace ArchLinterNet.Cli.Commands.Schema;

internal sealed class SchemaCommandModule : ITopLevelCliSubcommandModule
{
    public string CommandName => "schema";

    public System.CommandLine.Command CreateCommand(ICliRuntime runtime, ICliConsole console, IFileSystem fileSystem, CancellationToken cancellationToken = default)
    {
        return new SchemaCommandDefinition(new SchemaCommandHandler(new PackagedSchemaRegistry(), console)).Create();
    }
}
