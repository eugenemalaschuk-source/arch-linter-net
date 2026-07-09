using System.Reflection;
using ArchLinterNet.Cli.Abstractions;

namespace ArchLinterNet.Cli.Infrastructure;

internal static class CliCommandModuleCatalog
{
    public static IRootCliCommandModule CreateRootModule(ICliRuntime runtime, ICliConsole console, IFileSystem fileSystem)
    {
        Type moduleType = GetModuleTypes<IRootCliCommandModule>().Single();
        return CreateModule<IRootCliCommandModule>(moduleType, runtime, console, fileSystem);
    }

    public static IReadOnlyList<ICliSubcommandModule> CreateSubcommandModules(
        ICliRuntime runtime,
        ICliConsole console,
        IFileSystem fileSystem)
    {
        return GetModuleTypes<ICliSubcommandModule>()
            .Select(type => CreateModule<ICliSubcommandModule>(type, runtime, console, fileSystem))
            .ToArray();
    }

    private static IReadOnlyList<Type> GetModuleTypes<TModule>()
    {
        return Assembly.GetExecutingAssembly()
            .GetTypes()
            .Where(type => typeof(TModule).IsAssignableFrom(type) && type is { IsAbstract: false, IsClass: true })
            .OrderBy(static type => type.FullName, StringComparer.Ordinal)
            .ToArray();
    }

    private static TModule CreateModule<TModule>(Type type, ICliRuntime runtime, ICliConsole console, IFileSystem fileSystem)
    {
        object? instance = Activator.CreateInstance(type, runtime, console, fileSystem);
        if (instance is TModule module)
        {
            return module;
        }

        throw new InvalidOperationException($"Failed to create CLI module '{type.FullName}'.");
    }
}
