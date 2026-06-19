using System.Reflection;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Resolution;

namespace ArchLinterNet.Core.Scanning;

internal static class ArchitectureTypeScanner
{
    public static Type[] FindTypesInNamespace(IEnumerable<Assembly> targetAssemblies, string namespacePrefix)
    {
        return FindTypes(
            targetAssemblies,
            type => ArchitectureLayerResolver.MatchesPrefix(
                ArchitectureTypeNames.SafeNamespace(type), namespacePrefix));
    }

    public static Type[] FindTypesInNamespaceWithSuffix(
        IEnumerable<Assembly> targetAssemblies,
        string namespacePrefix,
        string namespaceSuffix)
    {
        var layer = new ArchitectureLayer { Namespace = namespacePrefix, NamespaceSuffix = namespaceSuffix };
        return FindTypesInLayer(targetAssemblies, layer);
    }

    public static Type[] FindTypesInLayer(IEnumerable<Assembly> targetAssemblies, ArchitectureLayer layer)
    {
        return FindTypes(
            targetAssemblies,
            type => ArchitectureLayerResolver.MatchesNamespace(layer, ArchitectureTypeNames.SafeNamespace(type)));
    }

    private static Type[] FindTypes(IEnumerable<Assembly> targetAssemblies, Func<Type, bool> predicate)
    {
        return targetAssemblies
            .Distinct()
            .SelectMany(GetLoadableTypes)
            .Where(predicate)
            .ToArray();
    }

    internal static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException exception)
        {
            return exception.Types.Where(type => type != null)!;
        }
    }
}
