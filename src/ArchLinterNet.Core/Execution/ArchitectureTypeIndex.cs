using System.Reflection;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Resolution;
using ArchLinterNet.Core.Scanning;

namespace ArchLinterNet.Core.Execution;

internal sealed class ArchitectureTypeIndex
{
    private readonly IReadOnlyCollection<Assembly> _targetAssemblies;
    private readonly Lazy<Type[]> _allTypes;

    public ArchitectureTypeIndex(IReadOnlyCollection<Assembly> targetAssemblies)
    {
        _targetAssemblies = targetAssemblies ?? throw new ArgumentNullException(nameof(targetAssemblies));
        _allTypes = new Lazy<Type[]>(LoadAllTypes);
    }

    public Type[] FindTypesInLayer(ArchitectureLayer layer)
    {
        return _allTypes.Value
            .Where(type => ArchitectureLayerResolver.MatchesNamespace(layer, ArchitectureTypeNames.SafeNamespace(type)))
            .ToArray();
    }

    public Type[] FindTypesInNamespace(string namespacePrefix)
    {
        return _allTypes.Value
            .Where(type => ArchitectureLayerResolver.MatchesPrefix(
                ArchitectureTypeNames.SafeNamespace(type), namespacePrefix))
            .ToArray();
    }

    public HashSet<string> FindDirectChildNamespaces(string containerNamespace)
    {
        string prefix = containerNamespace + ".";
        HashSet<string> children = new(StringComparer.Ordinal);

        foreach (Type type in _allTypes.Value)
        {
            string ns = ArchitectureTypeNames.SafeNamespace(type);
            if (!ns.StartsWith(prefix, StringComparison.Ordinal))
            {
                continue;
            }

            string remainder = ns[prefix.Length..];
            int dotIndex = remainder.IndexOf('.');
            string child = dotIndex < 0 ? remainder : remainder[..dotIndex];
            children.Add($"{prefix}{child}");
        }

        return children;
    }

    private Type[] LoadAllTypes()
    {
        return _targetAssemblies
            .Distinct()
            .SelectMany(ArchitectureTypeScanner.GetLoadableTypes)
            .ToArray();
    }
}
