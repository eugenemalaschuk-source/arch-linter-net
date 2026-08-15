using System.CommandLine;

namespace ArchLinterNet.Cli.Commands.Schema.Application;

internal sealed class SchemaCommandDefinition(SchemaCommandHandler handler)
{
    public Command Create()
    {
        Command command = new("schema", "List or print release-matched packaged schemas without network access.");
        Command list = new("list", "List logical ids, versions, immutable ids, and packaged paths.");
        list.SetAction(_ => handler.List());

        Command print = new("print", "Print one exact packaged schema to standard output.");
        Argument<string> logicalId = new("logical-id")
        {
            Description = "Logical id reported by 'schema list'.",
        };
        print.Arguments.Add(logicalId);
        print.SetAction(parseResult => handler.Print(parseResult.GetValue(logicalId) ?? string.Empty));

        command.Subcommands.Add(list);
        command.Subcommands.Add(print);
        return command;
    }
}
