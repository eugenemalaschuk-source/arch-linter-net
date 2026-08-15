using System.Reflection;
using ArchLinterNet.Core.Model;

namespace ArchLinterNet.Core.Scanning;

internal static class ArchitectureModuleContainerDiscovery
{
    internal sealed record Inventory(
        IReadOnlyDictionary<Type, ArchitectureModuleNamespaceMembership> Locations,
        IReadOnlyList<string> ModuleNames);

    public static Inventory Discover(IEnumerable<Assembly> targetAssemblies, string containerNamespace)
    {
        var locations = new Dictionary<Type, ArchitectureModuleNamespaceMembership>();
        var moduleNames = new SortedSet<string>(StringComparer.Ordinal);

        foreach (Assembly assembly in targetAssemblies.Distinct())
        {
            foreach (Type type in ArchitectureTypeScanner.GetLoadableTypes(assembly))
            {
                string typeNamespace = ArchitectureTypeNames.SafeNamespace(type);
                if (!ArchitectureModuleNamespaceMembershipResolver.TryResolve(
                        containerNamespace, typeNamespace, out ArchitectureModuleNamespaceMembership? membership)
                    || membership == null)
                {
                    continue;
                }

                if (membership is { ModuleName: not null })
                {
                    moduleNames.Add(membership.ModuleName);
                }

                locations[type] = membership;
            }
        }

        return new Inventory(locations, moduleNames.ToArray());
    }
}
