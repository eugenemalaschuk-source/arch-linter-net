using ArchLinterNet.Cli.Abstractions;

namespace ArchLinterNet.Cli.Infrastructure;

internal sealed class CliCompositionRoot
{
    public static CliComposition Compose()
    {
        ICliConsole console = new SystemCliConsole();
        IFileSystem fileSystem = new FileSystem();
        ICliRuntime runtime = new CliRuntime();

        IRootCliCommandModule rootCommandModule = CliCommandModuleCatalog.CreateRootModule();
        IReadOnlyList<ITopLevelCliSubcommandModule> subcommandModules =
            CliCommandModuleCatalog.CreateSubcommandModules();

        ICliRootCommandFactory rootCommandFactory = new CliRootCommandFactory(
            rootCommandModule,
            subcommandModules,
            runtime,
            console,
            fileSystem);
        CliHost host = new(rootCommandFactory, console, runtime);

        return new CliComposition(
            host,
            rootCommandFactory,
            runtime,
            rootCommandModule,
            subcommandModules);
    }

    public static CliHost CreateHost()
    {
        return Compose().Host;
    }
}

internal sealed record CliComposition(
    CliHost Host,
    ICliRootCommandFactory RootCommandFactory,
    ICliRuntime Runtime,
    IRootCliCommandModule RootCommandModule,
    IReadOnlyList<ITopLevelCliSubcommandModule> SubcommandModules);
