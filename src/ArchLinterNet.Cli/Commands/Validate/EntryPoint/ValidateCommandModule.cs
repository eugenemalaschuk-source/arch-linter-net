using ArchLinterNet.Cli.Abstractions;
using ArchLinterNet.Cli.Commands.Validate.Application;

namespace ArchLinterNet.Cli.Commands.Validate.EntryPoint;

internal sealed class ValidateCommandModule : IRootCliCommandModule
{
    public System.CommandLine.RootCommand CreateRootCommand(
        ICliRuntime runtime, ICliConsole console, IFileSystem fileSystem, CancellationToken cancellationToken = default)
    {
        return new ValidateCommandDefinition(
            new ValidateCommandHandler(runtime, console, fileSystem, cancellationToken)).CreateRootCommand();
    }
}
