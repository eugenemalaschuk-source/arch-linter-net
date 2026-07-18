using System.Globalization;
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
        return MatchNamespace(layer, namespaceName).Matched;
    }

    public static ArchitectureNamespaceMatch MatchNamespace(ArchitectureLayer layer, string namespaceName)
    {
        if (string.IsNullOrWhiteSpace(layer.Namespace))
        {
            return new ArchitectureNamespaceMatch(false, string.Empty, null);
        }

        NamespaceGlobPattern pattern = layer.GlobPattern;

        if (!pattern.IsGlob)
        {
            return MatchLiteral(layer, namespaceName);
        }

        return MatchGlob(layer, namespaceName, pattern);
    }

    public static string DescribeLayer(ArchitectureLayer layer)
    {
        if (string.IsNullOrWhiteSpace(layer.Namespace))
        {
            return DescribeSelector(layer);
        }

        string namespaceDescription = DescribeNamespacePart(layer);
        if (layer.Selector == null)
        {
            return namespaceDescription;
        }

        return $"{namespaceDescription} + {DescribeSelector(layer)}";
    }

    private static string DescribeNamespacePart(ArchitectureLayer layer)
    {
        NamespaceGlobPattern pattern = layer.GlobPattern;
        bool hasSuffix = !string.IsNullOrEmpty(layer.NamespaceSuffix);

        if (!pattern.IsGlob)
        {
            return hasSuffix
                ? $"{layer.Namespace}.*.{layer.NamespaceSuffix}"
                : layer.Namespace;
        }

        if (!hasSuffix)
        {
            return layer.Namespace;
        }

        return $"{layer.Namespace}.{layer.NamespaceSuffix}";
    }

    private static string DescribeSelector(ArchitectureLayer layer)
    {
        if (layer.Selector == null)
        {
            return "<empty layer>";
        }

        string metadata = layer.Selector.Metadata.Count == 0
            ? string.Empty
            : $", metadata: {string.Join(", ", layer.Selector.Metadata.OrderBy(e => e.Key, StringComparer.Ordinal).Select(e => $"{e.Key}={FormatScalar(e.Value)}"))}";
        string when = string.IsNullOrEmpty(layer.Selector.When) ? string.Empty : $", when: {layer.Selector.When}";
        return $"selector(role: {layer.Selector.Role}{metadata}{when})";
    }

    private static string FormatScalar(object value)
    {
        return value switch
        {
            null => string.Empty,
            string s => s,
            bool b => b ? "True" : "False",
            byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal =>
                Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty,
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString() ?? string.Empty
        };
    }

    public static string? ResolveContainingLayer(
        ArchitectureContractDocument document,
        string namespaceName,
        IReadOnlySet<string> candidateLayerNames)
    {
        return candidateLayerNames
            .Select(layerName => new
            {
                LayerName = layerName,
                Layer = ResolveLayer(document, "cycle-resolution", layerName)
            })
            .Where(layer => !string.IsNullOrWhiteSpace(layer.Layer.Namespace))
            .Select(layer => new
            {
                layer.LayerName,
                layer.Layer,
                Match = MatchNamespace(layer.Layer, namespaceName),
                Pattern = layer.Layer.GlobPattern
            })
            .Where(layer => layer.Match.Matched)
            .OrderByDescending(layer => !layer.Pattern.IsGlob)
            .ThenByDescending(layer => layer.Pattern.LiteralCount)
            .ThenByDescending(layer => !string.IsNullOrEmpty(layer.Layer.NamespaceSuffix))
            .ThenBy(layer => layer.Pattern.WildcardCount)
            .ThenBy(layer => layer.LayerName, StringComparer.Ordinal)
            .Select(layer => layer.LayerName)
            .FirstOrDefault();
    }

    public static bool IsProjectType(ArchitectureContractDocument document, string namespaceName)
    {
        return document.Layers.Values.Any(layer =>
            !string.IsNullOrWhiteSpace(layer.Namespace)
            && MatchesNamespace(layer, namespaceName));
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

    private static ArchitectureNamespaceMatch MatchLiteral(
        ArchitectureLayer layer, string namespaceName)
    {
        bool prefixMatch = MatchesPrefix(namespaceName, layer.Namespace);

        if (!prefixMatch)
        {
            return new ArchitectureNamespaceMatch(false, layer.Namespace, null);
        }

        if (!string.IsNullOrEmpty(layer.NamespaceSuffix))
        {
            if (!namespaceName.EndsWith("." + layer.NamespaceSuffix, StringComparison.Ordinal))
            {
                return new ArchitectureNamespaceMatch(false, layer.Namespace, null);
            }

            return new ArchitectureNamespaceMatch(true, layer.Namespace, null);
        }

        return new ArchitectureNamespaceMatch(true, layer.Namespace, null);
    }

    private static ArchitectureNamespaceMatch MatchGlob(
        ArchitectureLayer layer, string namespaceName, NamespaceGlobPattern pattern)
    {
        ArchitectureNamespaceMatch baseMatch = pattern.Match(namespaceName);

        if (!baseMatch.Matched)
        {
            return baseMatch;
        }

        if (string.IsNullOrEmpty(layer.NamespaceSuffix))
        {
            return baseMatch;
        }

        string[] nsSegments = namespaceName.Split('.');
        string[] patternSegments = layer.Namespace.Split('.');
        string[] suffixSegments = layer.NamespaceSuffix.Split('.');

        int suffixIndex = patternSegments.Length;
        if (nsSegments.Length < suffixIndex + suffixSegments.Length)
        {
            return new ArchitectureNamespaceMatch(false, layer.Namespace, null);
        }

        for (int i = 0; i < suffixSegments.Length; i++)
        {
            if (nsSegments[suffixIndex + i] != suffixSegments[i])
            {
                return new ArchitectureNamespaceMatch(false, layer.Namespace, null);
            }
        }

        if (string.IsNullOrEmpty(baseMatch.MatchedNamespacePrefix))
        {
            return new ArchitectureNamespaceMatch(false, layer.Namespace, null);
        }

        string resolvedPrefix = $"{baseMatch.MatchedNamespacePrefix}.{layer.NamespaceSuffix}";

        return new ArchitectureNamespaceMatch(true, baseMatch.Pattern, resolvedPrefix);
    }

}
