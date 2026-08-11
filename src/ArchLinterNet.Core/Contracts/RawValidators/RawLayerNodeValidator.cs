using ArchLinterNet.Core.Contracts.PolicyImports;
using YamlDotNet.RepresentationModel;

namespace ArchLinterNet.Core.Contracts.RawValidators;

// Raw-YAML node validation for the `layers` block. IgnoreUnmatchedProperties() silently drops any
// key with no matching C# property, so a typo'd layer or selector property would otherwise leave the
// layer looking legitimate-but-inert instead of failing the load.
internal sealed class RawLayerNodeValidator : IArchitecturePolicyRawDocumentValidator
{
    public void Validate(ArchitecturePolicyRawDocument document)
    {
        if (!document.TryGetSection("layers", out YamlMappingNode? layers))
        {
            return;
        }

        foreach ((YamlNode keyNode, YamlNode valueNode) in layers.Children)
        {
            string layerName = ((YamlScalarNode)keyNode).Value ?? string.Empty;
            document.Provenance.SetValidationSubject(ArchitecturePolicyProvenancePath.AppendProperty(
                ArchitecturePolicyProvenancePath.Property("layers"), layerName));
            if (valueNode is not YamlMappingNode layerNode)
            {
                continue;
            }

            ValidateLayerNodeKeys(layerNode, layerName);
            ValidateNamespaceValue(layerNode, layerName);
            ValidateLayerExcludeEntries(layerNode, layerName);
            ValidateLayerOverlapsWithEntries(layerNode, layerName);

            bool hasNamespace = RawYamlNodes.TryGetNonNullChild(layerNode, RawYamlNodes.NamespaceKey, out _);
            bool hasNamespaceSuffix = RawYamlNodes.TryGetNonNullChild(layerNode, RawYamlNodes.NamespaceSuffixKey, out _);
            bool hasSelectorKey = RawYamlNodes.TryGetChild(layerNode, "selector", out YamlNode? selectorNode);

            if (hasSelectorKey && RawYamlNodes.IsExplicitNull(selectorNode))
            {
                throw new InvalidOperationException(
                    $"Layer '{layerName}' selector must be an object when declared.");
            }

            if (hasNamespaceSuffix && !hasNamespace)
            {
                throw new InvalidOperationException(
                    $"Layer '{layerName}' namespace_suffix requires a non-empty namespace.");
            }

            if (selectorNode is not YamlMappingNode selectorMapping)
            {
                continue;
            }

            if (RawYamlNodes.TryGetChild(selectorMapping, RawYamlNodes.MetadataKey, out YamlNode? metadataNode)
                && RawYamlNodes.IsExplicitNull(metadataNode))
            {
                throw new InvalidOperationException(
                    $"Layer '{layerName}' selector metadata must be an object when declared.");
            }

            foreach ((YamlNode selKeyNode, _) in selectorMapping.Children)
            {
                if (selKeyNode is YamlScalarNode selKeyScalar
                    && !string.Equals(selKeyScalar.Value, "role", StringComparison.Ordinal)
                    && !string.Equals(selKeyScalar.Value, RawYamlNodes.MetadataKey, StringComparison.Ordinal)
                    && !string.Equals(selKeyScalar.Value, RawYamlNodes.WhenKey, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"Layer '{layerName}' selector contains unknown property '{selKeyScalar.Value}'.");
                }
            }
        }
    }

    private static void ValidateLayerNodeKeys(YamlMappingNode layerNode, string layerName)
    {
        foreach ((YamlNode keyNode, _) in layerNode.Children)
        {
            if (keyNode is YamlScalarNode scalar
                && !string.Equals(scalar.Value, RawYamlNodes.NamespaceKey, StringComparison.Ordinal)
                && !string.Equals(scalar.Value, RawYamlNodes.NamespaceSuffixKey, StringComparison.Ordinal)
                && !string.Equals(scalar.Value, "external", StringComparison.Ordinal)
                && !string.Equals(scalar.Value, "selector", StringComparison.Ordinal)
                && !string.Equals(scalar.Value, RawYamlNodes.ExcludeKey, StringComparison.Ordinal)
                && !string.Equals(scalar.Value, RawYamlNodes.OverlapsWithKey, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Layer '{layerName}' contains unknown property '{scalar.Value}'.");
            }
        }
    }

    // Mirrors ValidateLayerExcludeEntries: overlaps_with is a flat list of layer-name strings, so
    // the only raw shape to guard is "sequence of non-empty scalars" - a nested mapping/sequence
    // entry would otherwise reach ArchitectureLayer.OverlapsWith as a deserialization type error
    // with no layer-name context instead of this actionable message.
    private static void ValidateLayerOverlapsWithEntries(YamlMappingNode layerNode, string layerName)
    {
        if (!RawYamlNodes.TryGetChild(layerNode, RawYamlNodes.OverlapsWithKey, out YamlNode? overlapsWithNode))
        {
            return;
        }

        if (overlapsWithNode is not YamlSequenceNode overlapsWithSequence)
        {
            throw new InvalidOperationException(
                $"Layer '{layerName}' overlaps_with must be a list of layer names.");
        }

        foreach (YamlNode entryNode in overlapsWithSequence.Children)
        {
            if (entryNode is not YamlScalarNode entryScalar || string.IsNullOrWhiteSpace(entryScalar.Value))
            {
                throw new InvalidOperationException(
                    $"Layer '{layerName}' overlaps_with entries must be non-empty layer name strings.");
            }
        }
    }

    // Mirrors the selector-key check above: `exclude` entries accept only namespace/namespace_suffix
    // (the same shape a layer itself uses for inclusion). An unrecognized key such as a typo'd
    // "namespace_sufix" or an accidental "role" would otherwise be silently dropped by
    // IgnoreUnmatchedProperties(), leaving the exclusion inert without any signal to the author.
    private static void ValidateLayerExcludeEntries(YamlMappingNode layerNode, string layerName)
    {
        if (!RawYamlNodes.TryGetChild(layerNode, RawYamlNodes.ExcludeKey, out YamlNode? excludeNode))
        {
            return;
        }

        if (excludeNode is not YamlSequenceNode excludeSequence)
        {
            throw new InvalidOperationException(
                $"Layer '{layerName}' exclude must be a list of entries.");
        }

        foreach (YamlNode entryNode in excludeSequence.Children)
        {
            if (entryNode is not YamlMappingNode entryMapping)
            {
                throw new InvalidOperationException(
                    $"Layer '{layerName}' exclude entries must be objects with a 'namespace' key.");
            }

            bool hasNamespace = false;

            foreach ((YamlNode entryKeyNode, _) in entryMapping.Children)
            {
                if (entryKeyNode is not YamlScalarNode entryKeyScalar)
                {
                    continue;
                }

                if (string.Equals(entryKeyScalar.Value, RawYamlNodes.NamespaceKey, StringComparison.Ordinal))
                {
                    hasNamespace = true;
                    continue;
                }

                if (!string.Equals(entryKeyScalar.Value, RawYamlNodes.NamespaceSuffixKey, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"Layer '{layerName}' exclude entry contains unknown property '{entryKeyScalar.Value}'.");
                }
            }

            if (!hasNamespace)
            {
                throw new InvalidOperationException(
                    $"Layer '{layerName}' exclude entry must declare 'namespace'.");
            }
        }
    }

    private static void ValidateNamespaceValue(YamlMappingNode layerNode, string layerName)
    {
        if (RawYamlNodes.TryGetChild(layerNode, RawYamlNodes.NamespaceKey, out YamlNode? nsNode)
            && nsNode is YamlScalarNode nsScalar
            && (RawYamlNodes.IsExplicitNull(nsScalar) || string.IsNullOrWhiteSpace(nsScalar.Value)))
        {
            throw new InvalidOperationException(
                $"Layer '{layerName}' namespace must be a non-empty string.");
        }
    }
}
