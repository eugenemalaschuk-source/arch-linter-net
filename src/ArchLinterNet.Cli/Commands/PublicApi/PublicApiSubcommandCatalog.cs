using System.Reflection;
using ArchLinterNet.Cli.Commands.PublicApi.Abstractions;

namespace ArchLinterNet.Cli.Commands.PublicApi;

internal static class PublicApiSubcommandCatalog
{
    public static IReadOnlyList<IPublicApiSubcommandModule> CreateModules()
    {
        return Assembly.GetExecutingAssembly()
            .GetTypes()
            .Where(type => typeof(IPublicApiSubcommandModule).IsAssignableFrom(type) && type is { IsAbstract: false, IsClass: true })
            .OrderBy(static type => type.FullName, StringComparer.Ordinal)
            .Select(CreateModule)
            .ToArray();
    }

    private static IPublicApiSubcommandModule CreateModule(Type type)
    {
        object? instance = Activator.CreateInstance(type);
        if (instance is IPublicApiSubcommandModule module)
        {
            return module;
        }

        throw new InvalidOperationException($"Failed to create public-api subcommand module '{type.FullName}'.");
    }
}
