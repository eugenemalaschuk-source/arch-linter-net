using ArchLinterNet.Cli.Abstractions;

namespace ArchLinterNet.Cli.Infrastructure;

internal static class CliCompositionRoot
{
    public static CliComposition Compose()
    {
        ICliConsole console = new SystemCliConsole();
        IFileSystem fileSystem = new FileSystem();
        ICliRuntime runtime = new CliRuntime();

        IRootCliCommandModule rootCommandModule = CliCommandModuleCatalog.CreateRootModule();
        IReadOnlyList<ITopLevelCliSubcommandModule> subcommandModules =
            CliCommandModuleCatalog.CreateSubcommandModules();

        CliProcessInterruptionSource interruptionSource = new();

        ICliRootCommandFactory rootCommandFactory = new CliRootCommandFactory(
            rootCommandModule,
            subcommandModules,
            runtime,
            console,
            fileSystem,
            interruptionSource.Token);
        CliHost host = new(rootCommandFactory, console, runtime);

        return new CliComposition(
            host,
            rootCommandFactory,
            runtime,
            rootCommandModule,
            subcommandModules,
            interruptionSource);
    }
}

// IDisposable so the one process-scoped CliProcessInterruptionSource this composition owns
// (Console.CancelKeyPress subscription, PosixSignalRegistration, CancellationTokenSource) is
// deterministically released instead of left to finalization — see Program.Main's `using`.
internal sealed record CliComposition(
    CliHost Host,
    ICliRootCommandFactory RootCommandFactory,
    ICliRuntime Runtime,
    IRootCliCommandModule RootCommandModule,
    IReadOnlyList<ITopLevelCliSubcommandModule> SubcommandModules,
    CliProcessInterruptionSource InterruptionSource) : IDisposable
{
    public void Dispose()
    {
        InterruptionSource.Dispose();
    }
}
