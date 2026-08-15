using ArchLinterNet.Cli.Abstractions;
using ArchLinterNet.Cli.Commands;
using ArchLinterNet.Core.Schema;

namespace ArchLinterNet.Cli.Commands.Schema.Application;

internal sealed class SchemaCommandHandler(PackagedSchemaRegistry registry, ICliConsole console)
{
    public int List()
    {
        foreach (PackagedSchemaDescriptor schema in registry.List())
        {
            console.Out.WriteLine($"{schema.LogicalId}\t{schema.DocumentVersion}\t{schema.SchemaId}\t{schema.ResourcePath}");
        }

        return CliExitCodes.Success;
    }

    public int Print(string logicalId)
    {
        if (!registry.TryRead(logicalId, out string schema))
        {
            console.Error.WriteLine($"Unknown packaged schema '{logicalId}'. Run 'arch-linter-net schema list'.");
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }

        console.Out.Write(schema);
        return CliExitCodes.Success;
    }
}
