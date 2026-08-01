using System.Reflection;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Resolution;

namespace ArchLinterNet.Core.Scanning;

internal static class ArchitectureTypeScanner
{
    public static Type[] FindTypesInNamespace(
        IEnumerable<Assembly> targetAssemblies, string namespacePrefix, CancellationToken cancellationToken = default)
    {
        return FindTypes(
            targetAssemblies,
            type => ArchitectureLayerResolver.MatchesPrefix(
                ArchitectureTypeNames.SafeNamespace(type), namespacePrefix),
            cancellationToken);
    }

    public static Type[] FindTypesInNamespaceWithSuffix(
        IEnumerable<Assembly> targetAssemblies,
        string namespacePrefix,
        string namespaceSuffix)
    {
        var layer = new ArchitectureLayer { Namespace = namespacePrefix, NamespaceSuffix = namespaceSuffix };
        return FindTypesInLayer(targetAssemblies, layer);
    }

    public static Type[] FindTypesInLayer(
        IEnumerable<Assembly> targetAssemblies, ArchitectureLayer layer, CancellationToken cancellationToken = default)
    {
        return FindTypes(
            targetAssemblies,
            type => ArchitectureLayerResolver.MatchesNamespace(layer, ArchitectureTypeNames.SafeNamespace(type)),
            cancellationToken);
    }

    // Checked per assembly — the same reflection-pass boundary
    // ArchitectureSourceFileFactIndex.RunReflectionPass/ArchitectureTypeIndex already check at, so
    // discovering the candidate type set itself is interruptible, not just the per-type loop a
    // caller (e.g. ArchitectureIlMethodBodyScanner) runs over the result afterward.
    private static Type[] FindTypes(
        IEnumerable<Assembly> targetAssemblies, Func<Type, bool> predicate, CancellationToken cancellationToken = default)
    {
        List<Type> matches = new();
        foreach (Assembly assembly in targetAssemblies.Distinct())
        {
            cancellationToken.ThrowIfCancellationRequested();
            foreach (Type type in GetLoadableTypes(assembly))
            {
                if (predicate(type))
                {
                    matches.Add(type);
                }
            }
        }

        return matches.ToArray();
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
