using System.Reflection;
using ArchLinterNet.Cli.Abstractions;

namespace ArchLinterNet.Cli.Infrastructure;

internal static class CliCommandModuleCatalog
{
    public static IRootCliCommandModule CreateRootModule()
    {
        Type moduleType = GetModuleTypes<IRootCliCommandModule>().Single();
        return CreateModule<IRootCliCommandModule>(moduleType);
    }

    public static IReadOnlyList<ITopLevelCliSubcommandModule> CreateSubcommandModules()
    {
        return GetModuleTypes<ITopLevelCliSubcommandModule>()
            .Select(CreateModule<ITopLevelCliSubcommandModule>)
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

    private static TModule CreateModule<TModule>(Type type)
    {
        object? instance = Activator.CreateInstance(type);
        if (instance is TModule module)
        {
            return module;
        }

        throw new InvalidOperationException($"Failed to create CLI module '{type.FullName}'.");
    }
}
