using System.Reflection;
using ArchLinterNet.Cli.Commands.Topology.Abstractions;

namespace ArchLinterNet.Cli.Commands.Topology.Application;

internal static class TopologySubcommandCatalog
{
    public static IReadOnlyList<ITopologySubcommandModule> CreateModules()
    {
        return Assembly.GetExecutingAssembly()
            .GetTypes()
            .Where(type => typeof(ITopologySubcommandModule).IsAssignableFrom(type)
                && type is { IsAbstract: false, IsClass: true })
            .OrderBy(static type => type.FullName, StringComparer.Ordinal)
            .Select(CreateModule)
            .ToArray();
    }

    private static ITopologySubcommandModule CreateModule(Type type)
    {
        object? instance = Activator.CreateInstance(type);
        if (instance is ITopologySubcommandModule module)
        {
            return module;
        }

        throw new InvalidOperationException($"Failed to create topology subcommand module '{type.FullName}'.");
    }
}
