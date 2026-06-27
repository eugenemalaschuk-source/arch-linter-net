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

    private Type[] LoadAllTypes()
    {
        return _targetAssemblies
            .Distinct()
            .SelectMany(ArchitectureTypeScanner.GetLoadableTypes)
            .ToArray();
    }
}
