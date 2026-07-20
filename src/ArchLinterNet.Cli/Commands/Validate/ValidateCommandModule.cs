using ArchLinterNet.Cli.Abstractions;

namespace ArchLinterNet.Cli.Commands.Validate;

internal sealed class ValidateCommandModule : IRootCliCommandModule
{
    public System.CommandLine.RootCommand CreateRootCommand(ICliRuntime runtime, ICliConsole console, IFileSystem fileSystem)
    {
        return new ValidateCommandDefinition(new ValidateCommandHandler(runtime, console)).CreateRootCommand();
    }
}
