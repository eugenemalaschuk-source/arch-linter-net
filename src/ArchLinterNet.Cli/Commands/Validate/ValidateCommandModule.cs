using ArchLinterNet.Cli.Abstractions;

namespace ArchLinterNet.Cli.Commands.Validate;

internal sealed class ValidateCommandModule : IRootCliCommandModule
{
    private readonly ValidateCommandDefinition _definition;

    public ValidateCommandModule(ICliRuntime runtime, ICliConsole console, IFileSystem fileSystem)
    {
        _definition = new ValidateCommandDefinition(new ValidateCommandHandler(runtime, console, fileSystem));
    }

    public System.CommandLine.RootCommand CreateRootCommand() => _definition.CreateRootCommand();
}
