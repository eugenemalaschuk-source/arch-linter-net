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

    // Checked per assembly AND per type — the same reflection-pass boundary
    // ArchitectureSourceFileFactIndex.RunReflectionPass/ArchitectureTypeIndex already check at.
    // GetLoadableTypes itself is one eager reflection call per assembly (not individually
    // interruptible), but a single large target assembly can still contain thousands of types, so
    // the predicate loop below is checked per type too — not only at the assembly boundary — so
    // discovering the candidate type set is interruptible at the same granularity a caller (e.g.
    // ArchitectureIlMethodBodyScanner) would otherwise expect from the per-type loop it runs over
    // the result afterward.
    private static Type[] FindTypes(
        IEnumerable<Assembly> targetAssemblies, Func<Type, bool> predicate, CancellationToken cancellationToken = default)
    {
        List<Type> matches = new();
        foreach (Assembly assembly in targetAssemblies.Distinct())
        {
            cancellationToken.ThrowIfCancellationRequested();
            foreach (Type type in GetLoadableTypes(assembly, cancellationToken))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (predicate(type))
                {
                    matches.Add(type);
                }
            }
        }

        return matches.ToArray();
    }

    internal static IEnumerable<Type> GetLoadableTypes(Assembly assembly) =>
        GetLoadableTypes(assembly, CancellationToken.None);

    internal static IEnumerable<Type> GetLoadableTypes(Assembly assembly, CancellationToken cancellationToken)
    {
        Type[] types;
        try
        {
            types = assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException exception)
        {
            types = exception.Types.Where(type => type != null).Cast<Type>().ToArray();
        }

        foreach (Type type in types)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return type;
        }
    }

    // Type-counting metrics must distinguish a genuine empty assembly from a partial
    // ReflectionTypeLoadException result. Keep the existing iterator above for validation's
    // best-effort callers; this eager metric-only projection retains the completeness bit.
    internal static ArchitectureLoadableTypeScan GetLoadableTypesWithCompleteness(
        Assembly assembly,
        CancellationToken cancellationToken)
    {
        Type[] types;
        bool isComplete;
        try
        {
            types = assembly.GetTypes();
            isComplete = true;
        }
        catch (ReflectionTypeLoadException exception)
        {
            types = exception.Types.Where(type => type != null).Cast<Type>().ToArray();
            isComplete = false;
        }

        foreach (Type type in types)
        {
            cancellationToken.ThrowIfCancellationRequested();
        }

        return new ArchitectureLoadableTypeScan(types, isComplete);
    }
}

internal sealed record ArchitectureLoadableTypeScan(Type[] Types, bool IsComplete);
