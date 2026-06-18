using ArchLinterNet.Core.Contracts;

namespace ArchLinterNet.Core.Resolution;

internal static class ArchitectureLayerResolver
{
    public static ArchitectureLayer ResolveLayer(
        ArchitectureContractDocument document,
        string contractName,
        string layerName)
    {
        if (!document.Layers.TryGetValue(layerName, out ArchitectureLayer? layer))
        {
            throw new InvalidOperationException(
                $"Architecture contract '{contractName}' references unknown layer '{layerName}'.");
        }

        return layer;
    }

    public static string ResolveLayerNamespace(
        ArchitectureContractDocument document,
        string contractName,
        string layerName)
    {
        return ResolveLayer(document, contractName, layerName).Namespace;
    }

    public static bool MatchesNamespace(ArchitectureLayer layer, string namespaceName)
    {
        if (string.IsNullOrEmpty(layer.NamespaceSuffix))
        {
            return MatchesPrefix(namespaceName, layer.Namespace);
        }

        return MatchesPrefix(namespaceName, layer.Namespace)
               && namespaceName.EndsWith("." + layer.NamespaceSuffix, StringComparison.Ordinal);
    }

    public static string DescribeLayer(ArchitectureLayer layer)
    {
        return string.IsNullOrEmpty(layer.NamespaceSuffix)
            ? layer.Namespace
            : $"{layer.Namespace}.*.{layer.NamespaceSuffix}";
    }

    public static string? ResolveContainingLayer(
        ArchitectureContractDocument document,
        string typeName,
        IReadOnlySet<string> candidateLayerNames)
    {
        return candidateLayerNames
            .Select(layerName => new
            {
                LayerName = layerName,
                Namespace = ResolveLayerNamespace(document, "cycle-resolution", layerName)
            })
            .Where(layer => MatchesPrefix(typeName, layer.Namespace))
            .OrderByDescending(layer => layer.Namespace.Length)
            .Select(layer => layer.LayerName)
            .FirstOrDefault();
    }

    public static bool IsProjectType(ArchitectureContractDocument document, string typeName)
    {
        return document.Layers.Values.Any(layer =>
            MatchesPrefix(typeName, layer.Namespace));
    }

    public static bool IsInAnyNamespace(string typeName, IEnumerable<string> namespacePrefixes)
    {
        return namespacePrefixes.Any(prefix => MatchesPrefix(typeName, prefix));
    }

    public static bool MatchesPrefix(string name, string prefix)
    {
        return string.Equals(name, prefix, StringComparison.Ordinal)
               || name.StartsWith(prefix + ".", StringComparison.Ordinal);
    }
}
