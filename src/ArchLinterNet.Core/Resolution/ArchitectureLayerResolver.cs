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
        ArchitectureNamespaceMatch match = MatchNamespaceIncludeOnly(layer, namespaceName);

        if (!match.Matched || layer.Exclude.Count == 0)
        {
            return match;
        }

        return IsExcluded(layer, namespaceName)
            ? new ArchitectureNamespaceMatch(false, layer.Namespace, null)
            : match;
    }

    // The include-side match only, ignoring Exclude entirely. Used by MatchNamespace above and by
    // unmatched-exclusion detection, which needs to know whether a namespace falls within a layer's
    // included scope independent of whether any exclude entry also matches it.
    internal static ArchitectureNamespaceMatch MatchNamespaceIncludeOnly(ArchitectureLayer layer, string namespaceName)
    {
        if (string.IsNullOrWhiteSpace(layer.Namespace))
        {
            return new ArchitectureNamespaceMatch(false, string.Empty, null);
        }

        NamespaceGlobPattern pattern = layer.GlobPattern;

        return pattern.IsGlob
            ? MatchGlob(layer.Namespace, layer.NamespaceSuffix, namespaceName, pattern)
            : MatchLiteral(layer.Namespace, layer.NamespaceSuffix, namespaceName);
    }

    // result = union(includes) - union(excludes): an entry excludes namespaceName when it matches
    // the exclusion's own namespace/namespace_suffix glob, using exactly the same matching logic
    // (MatchLiteral/MatchGlob) the layer itself uses for inclusion. Excluding on the full
    // namespaceName (not the layer's matched prefix) mirrors how a sibling declared layer would
    // match the same namespace.
    private static bool IsExcluded(ArchitectureLayer layer, string namespaceName)
    {
        foreach (ArchitectureLayerExclusion exclusion in layer.Exclude)
        {
            if (ExclusionMatches(exclusion, namespaceName))
            {
                return true;
            }
        }

        return false;
    }

    // Whether a single exclude entry matches namespaceName, independent of any other entry on the
    // same layer. Exposed so callers that need to know EVERY matching entry (not just the first,
    // as FindMatchingExclusion below returns) - e.g. unmatched-exclusion detection with
    // overlapping exclude patterns - can test each entry on its own rather than relying on a
    // single "first wins" scan that would leave later-but-still-matching entries looking unused.
    internal static bool ExclusionMatches(ArchitectureLayerExclusion exclusion, string namespaceName)
    {
        if (string.IsNullOrWhiteSpace(exclusion.Namespace))
        {
            return false;
        }

        NamespaceGlobPattern pattern = exclusion.GlobPattern;
        ArchitectureNamespaceMatch match = pattern.IsGlob
            ? MatchGlob(exclusion.Namespace, exclusion.NamespaceSuffix, namespaceName, pattern)
            : MatchLiteral(exclusion.Namespace, exclusion.NamespaceSuffix, namespaceName);

        return match.Matched;
    }

    // Namespace matched by at least one exclude entry, or null when none matches. Used for
    // layer-description provenance, where only the fact that some entry decided the exclusion
    // matters. Do NOT use this for "which entries were used" accounting - it stops at the first
    // match, so overlapping entries after it would look unmatched even though they also match.
    internal static ArchitectureLayerExclusion? FindMatchingExclusion(ArchitectureLayer layer, string namespaceName)
    {
        foreach (ArchitectureLayerExclusion exclusion in layer.Exclude)
        {
            if (string.IsNullOrWhiteSpace(exclusion.Namespace))
            {
                continue;
            }

            NamespaceGlobPattern pattern = exclusion.GlobPattern;
            ArchitectureNamespaceMatch match = pattern.IsGlob
                ? MatchGlob(exclusion.Namespace, exclusion.NamespaceSuffix, namespaceName, pattern)
                : MatchLiteral(exclusion.Namespace, exclusion.NamespaceSuffix, namespaceName);

            if (match.Matched)
            {
                return exclusion;
            }
        }

        return null;
    }

    public static string DescribeLayer(ArchitectureLayer layer)
    {
        if (string.IsNullOrWhiteSpace(layer.Namespace))
        {
            return DescribeSelector(layer);
        }

        string namespaceDescription = DescribeNamespacePart(layer);

        if (layer.Exclude.Count > 0)
        {
            string excludeDescription = string.Join(", ", layer.Exclude
                .Where(e => !string.IsNullOrWhiteSpace(e.Namespace))
                .Select(DescribeExclusion));
            if (excludeDescription.Length > 0)
            {
                namespaceDescription = $"{namespaceDescription} (excluding {excludeDescription})";
            }
        }

        if (layer.Selector == null)
        {
            return namespaceDescription;
        }

        return $"{namespaceDescription} + {DescribeSelector(layer)}";
    }

    private static string DescribeExclusion(ArchitectureLayerExclusion exclusion)
    {
        return string.IsNullOrEmpty(exclusion.NamespaceSuffix)
            ? exclusion.Namespace
            : $"{exclusion.Namespace}.*.{exclusion.NamespaceSuffix}";
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
        string namespacePattern, string namespaceSuffix, string namespaceName)
    {
        bool prefixMatch = MatchesPrefix(namespaceName, namespacePattern);

        if (!prefixMatch)
        {
            return new ArchitectureNamespaceMatch(false, namespacePattern, null);
        }

        if (!string.IsNullOrEmpty(namespaceSuffix))
        {
            if (!namespaceName.EndsWith("." + namespaceSuffix, StringComparison.Ordinal))
            {
                return new ArchitectureNamespaceMatch(false, namespacePattern, null);
            }

            return new ArchitectureNamespaceMatch(true, namespacePattern, null);
        }

        return new ArchitectureNamespaceMatch(true, namespacePattern, null);
    }

    private static ArchitectureNamespaceMatch MatchGlob(
        string namespacePattern, string namespaceSuffix, string namespaceName, NamespaceGlobPattern pattern)
    {
        ArchitectureNamespaceMatch baseMatch = pattern.Match(namespaceName);

        if (!baseMatch.Matched)
        {
            return baseMatch;
        }

        if (string.IsNullOrEmpty(namespaceSuffix))
        {
            return baseMatch;
        }

        string[] nsSegments = namespaceName.Split('.');
        string[] patternSegments = namespacePattern.Split('.');
        string[] suffixSegments = namespaceSuffix.Split('.');

        int suffixIndex = patternSegments.Length;
        if (nsSegments.Length < suffixIndex + suffixSegments.Length)
        {
            return new ArchitectureNamespaceMatch(false, namespacePattern, null);
        }

        for (int i = 0; i < suffixSegments.Length; i++)
        {
            if (nsSegments[suffixIndex + i] != suffixSegments[i])
            {
                return new ArchitectureNamespaceMatch(false, namespacePattern, null);
            }
        }

        if (string.IsNullOrEmpty(baseMatch.MatchedNamespacePrefix))
        {
            return new ArchitectureNamespaceMatch(false, namespacePattern, null);
        }

        string resolvedPrefix = $"{baseMatch.MatchedNamespacePrefix}.{namespaceSuffix}";

        return new ArchitectureNamespaceMatch(true, baseMatch.Pattern, resolvedPrefix);
    }

}
