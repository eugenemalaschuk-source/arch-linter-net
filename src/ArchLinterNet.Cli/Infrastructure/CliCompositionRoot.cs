using System.Runtime.InteropServices;
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

        CancellationTokenSource cancellationTokenSource = RegisterProcessInterruptionSource();

        ICliRootCommandFactory rootCommandFactory = new CliRootCommandFactory(
            rootCommandModule,
            subcommandModules,
            runtime,
            console,
            fileSystem,
            cancellationTokenSource.Token);
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

    // One process-scoped cancellation source per CLI invocation: Ctrl+C/Ctrl+Break (all
    // platforms, via Console.CancelKeyPress) and SIGTERM (Unix, e.g. `timeout 30s dotnet ...`)
    // both trigger it. Windows has no SIGTERM equivalent to register. `e.Cancel = true` on the
    // CancelKeyPress handler lets cooperative shutdown (dispose owned resources, report a
    // cancelled completion status) run instead of the runtime hard-killing the process.
    private static CancellationTokenSource RegisterProcessInterruptionSource()
    {
        CancellationTokenSource cts = new();

        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return cts;
        }

        PosixSignalRegistration.Create(PosixSignal.SIGTERM, context =>
        {
            context.Cancel = true;
            cts.Cancel();
        });

        return cts;
    }
}

internal sealed record CliComposition(
    CliHost Host,
    ICliRootCommandFactory RootCommandFactory,
    ICliRuntime Runtime,
    IRootCliCommandModule RootCommandModule,
    IReadOnlyList<ITopLevelCliSubcommandModule> SubcommandModules);
