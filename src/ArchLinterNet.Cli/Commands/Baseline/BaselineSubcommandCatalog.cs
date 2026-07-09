using System.Reflection;
using ArchLinterNet.Cli.Abstractions;

namespace ArchLinterNet.Cli.Commands.Baseline;

internal static class BaselineSubcommandCatalog
{
    public static IReadOnlyList<IBaselineSubcommandModule> CreateModules(
        ICliRuntime runtime,
        ICliConsole console,
        IFileSystem fileSystem)
    {
        return Assembly.GetExecutingAssembly()
            .GetTypes()
            .Where(type => typeof(IBaselineSubcommandModule).IsAssignableFrom(type) && type is { IsAbstract: false, IsClass: true })
            .OrderBy(static type => type.FullName, StringComparer.Ordinal)
            .Select(type => CreateModule(type, runtime, console, fileSystem))
            .ToArray();
    }

    private static IBaselineSubcommandModule CreateModule(Type type, ICliRuntime runtime, ICliConsole console, IFileSystem fileSystem)
    {
        object? instance = Activator.CreateInstance(type, runtime, console, fileSystem);
        if (instance is IBaselineSubcommandModule module)
        {
            return module;
        }

        throw new InvalidOperationException($"Failed to create baseline subcommand module '{type.FullName}'.");
    }
}
