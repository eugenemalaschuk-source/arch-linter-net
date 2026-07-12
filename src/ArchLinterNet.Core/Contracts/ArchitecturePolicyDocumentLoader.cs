using System.Text.RegularExpressions;
using ArchLinterNet.Core.Contracts.Abstractions;
using ArchLinterNet.Core.Contracts.Validators;
using ArchLinterNet.Core.IO;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace ArchLinterNet.Core.Contracts;

public sealed partial class ArchitecturePolicyDocumentLoader : IArchitecturePolicyDocumentLoader
{
    private readonly IArchitectureFileSystem _fileSystem;

    public ArchitecturePolicyDocumentLoader()
        : this(ArchitectureFileSystem.Real)
    {
    }

    public ArchitecturePolicyDocumentLoader(IArchitectureFileSystem fileSystem)
    {
        _fileSystem = fileSystem;
    }

    public ArchitectureContractDocument Load(string policyPath)
    {
        if (!_fileSystem.FileExists(policyPath))
        {
            throw new FileNotFoundException($"Architecture contract file not found: {policyPath}");
        }

        IDeserializer deserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .WithNodeDeserializer(
                new ArchitectureClassificationMetadataScalarNodeDeserializer(),
                syntax => syntax.Before<YamlDotNet.Serialization.NodeDeserializers.ScalarNodeDeserializer>())
            .Build();

        string yaml = _fileSystem.ReadAllText(policyPath);
        ValidateRawLayerYaml(yaml);
        ValidateRawContextualContractYaml(yaml);
        ArchitectureContractDocument? document = deserializer.Deserialize<ArchitectureContractDocument>(yaml);

        if (document == null)
        {
            throw new InvalidOperationException("Failed to deserialize architecture contract YAML.");
        }

        AssignFallbackIds(document);

        foreach (IArchitecturePolicyDocumentValidator validator in ArchitecturePolicyDocumentValidatorPipeline.All)
        {
            validator.Validate(document);
        }

        return document;
    }

    private static void ValidateRawLayerYaml(string yaml)
    {
        var stream = new YamlStream();
        stream.Load(new StringReader(yaml));

        if (stream.Documents.Count == 0
            || stream.Documents[0].RootNode is not YamlMappingNode root
            || !TryGetMappingChild(root, "layers", out YamlMappingNode? layers))
        {
            return;
        }

        foreach ((YamlNode keyNode, YamlNode valueNode) in layers!.Children)
        {
            string layerName = ((YamlScalarNode)keyNode).Value ?? string.Empty;
            if (valueNode is not YamlMappingNode layerNode)
            {
                continue;
            }

            ValidateLayerNodeKeys(layerNode, layerName);
            ValidateNamespaceValue(layerNode, layerName);

            bool hasNamespace = TryGetNonNullChild(layerNode, "namespace", out _);
            bool hasNamespaceSuffix = TryGetNonNullChild(layerNode, "namespace_suffix", out _);
            YamlNode? selectorNode = null;
            bool hasSelectorKey = TryGetChild(layerNode, "selector", out selectorNode);

            if (hasSelectorKey && IsExplicitNull(selectorNode))
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

            if (TryGetChild(selectorMapping, "metadata", out YamlNode? metadataNode) && IsExplicitNull(metadataNode))
            {
                throw new InvalidOperationException(
                    $"Layer '{layerName}' selector metadata must be an object when declared.");
            }

            foreach ((YamlNode selKeyNode, _) in selectorMapping.Children)
            {
                if (selKeyNode is YamlScalarNode selKeyScalar
                    && !string.Equals(selKeyScalar.Value, "role", StringComparison.Ordinal)
                    && !string.Equals(selKeyScalar.Value, "metadata", StringComparison.Ordinal))
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
                && !string.Equals(scalar.Value, "namespace", StringComparison.Ordinal)
                && !string.Equals(scalar.Value, "namespace_suffix", StringComparison.Ordinal)
                && !string.Equals(scalar.Value, "external", StringComparison.Ordinal)
                && !string.Equals(scalar.Value, "selector", StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Layer '{layerName}' contains unknown property '{scalar.Value}'.");
            }
        }
    }

    private static void ValidateNamespaceValue(YamlMappingNode layerNode, string layerName)
    {
        if (TryGetChild(layerNode, "namespace", out YamlNode? nsNode)
            && nsNode is YamlScalarNode nsScalar
            && (IsExplicitNull(nsScalar) || string.IsNullOrWhiteSpace(nsScalar.Value)))
        {
            throw new InvalidOperationException(
                $"Layer '{layerName}' namespace must be a non-empty string.");
        }
    }

    // ContextualContractValidator (Validators/) runs after deserialization and can only see what
    // IgnoreUnmatchedProperties() left behind - an unknown selector property (e.g. "metdata" typo'd
    // for "metadata") is silently dropped by deserialization, leaving ArchitectureContextSelector's
    // Metadata at its empty-dictionary default. That default is structurally valid (a role-only
    // selector is a legitimate, intentional shape), so no post-deserialization check can distinguish
    // "author wrote role-only on purpose" from "author's metadata typo silently vanished" - the
    // dictionary looks identical either way. For context_allow_only in particular, an unintentionally
    // role-only `allowed` selector silently broadens to match every type of that role (any metadata),
    // turning a metadata-scoped allow-list into a false-negative that admits cross-context references.
    // This raw-YAML pass, mirroring ValidateRawLayerYaml's selector-key check below, is the only place
    // that can still see the rejected property name before deserialization discards it.
    private static void ValidateRawContextualContractYaml(string yaml)
    {
        var stream = new YamlStream();
        stream.Load(new StringReader(yaml));

        if (stream.Documents.Count == 0
            || stream.Documents[0].RootNode is not YamlMappingNode root
            || !TryGetMappingChild(root, "contracts", out YamlMappingNode? contracts))
        {
            return;
        }

        ValidateContextualContractGroup(contracts!, "strict_context_dependencies", "forbidden");
        ValidateContextualContractGroup(contracts!, "audit_context_dependencies", "forbidden");
        ValidateContextualContractGroup(contracts!, "strict_context_allow_only", "allowed");
        ValidateContextualContractGroup(contracts!, "audit_context_allow_only", "allowed");
    }

    private static void ValidateContextualContractGroup(YamlMappingNode contracts, string groupKey, string targetListKey)
    {
        if (!TryGetChild(contracts, groupKey, out YamlNode? groupNode) || groupNode is not YamlSequenceNode sequence)
        {
            return;
        }

        foreach (YamlNode entryNode in sequence.Children)
        {
            if (entryNode is not YamlMappingNode contractNode)
            {
                continue;
            }

            string contractName = TryGetChild(contractNode, "name", out YamlNode? nameNode)
                && nameNode is YamlScalarNode nameScalar
                    ? nameScalar.Value ?? "<unnamed>"
                    : "<unnamed>";

            if (TryGetChild(contractNode, "source", out YamlNode? sourceNode) && sourceNode is YamlMappingNode sourceMapping)
            {
                ValidateContextualSelectorNodeKeys(sourceMapping, contractName, "source");
            }

            ValidateContextualSelectorListKeys(contractNode, contractName, targetListKey);
            ValidateContextualSelectorListKeys(contractNode, contractName, "exclude");
        }
    }

    private static void ValidateContextualSelectorListKeys(YamlMappingNode contractNode, string contractName, string listKey)
    {
        if (!TryGetChild(contractNode, listKey, out YamlNode? listNode) || listNode is not YamlSequenceNode listSequence)
        {
            return;
        }

        foreach (YamlNode itemNode in listSequence.Children)
        {
            if (itemNode is YamlMappingNode itemMapping)
            {
                ValidateContextualSelectorNodeKeys(itemMapping, contractName, listKey);
            }
        }
    }

    private static void ValidateContextualSelectorNodeKeys(YamlMappingNode selectorNode, string contractName, string fieldName)
    {
        foreach ((YamlNode keyNode, _) in selectorNode.Children)
        {
            if (keyNode is YamlScalarNode scalar
                && !string.Equals(scalar.Value, "role", StringComparison.Ordinal)
                && !string.Equals(scalar.Value, "metadata", StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Contextual contract '{contractName}' declares an unknown property '{scalar.Value}' on its '{fieldName}' selector. " +
                    "A contextual selector supports only 'role' and 'metadata'.");
            }
        }
    }

    private static bool TryGetMappingChild(YamlMappingNode parent, string key, out YamlMappingNode? child)
    {
        child = null;
        if (!TryGetChild(parent, key, out YamlNode? node) || node is not YamlMappingNode mapping)
        {
            return false;
        }

        child = mapping;
        return true;
    }

    private static bool TryGetNonNullChild(YamlMappingNode parent, string key, out YamlNode? child)
    {
        child = null;
        return TryGetChild(parent, key, out child) && !IsExplicitNull(child);
    }

    private static bool TryGetChild(YamlMappingNode parent, string key, out YamlNode? child)
    {
        foreach ((YamlNode candidateKey, YamlNode candidateValue) in parent.Children)
        {
            if (candidateKey is YamlScalarNode scalar
                && string.Equals(scalar.Value, key, StringComparison.Ordinal))
            {
                child = candidateValue;
                return true;
            }
        }

        child = null;
        return false;
    }

    private static bool IsExplicitNull(YamlNode? node)
    {
        return node is YamlScalarNode scalar
            && (scalar.Value is null
                || (scalar.Style == ScalarStyle.Plain
                    && string.Equals(scalar.Value, "null", StringComparison.OrdinalIgnoreCase))
                || (scalar.Style == ScalarStyle.Plain
                    && string.Equals(scalar.Value, "~", StringComparison.Ordinal)));
    }

    public static string NormalizeToContractId(string name)
    {
        string normalized = name.ToLowerInvariant();
        normalized = normalized.Replace(" -> ", "-to-");
        normalized = NonAlphaNumDashPattern().Replace(normalized, "-");
        normalized = MultiDashPattern().Replace(normalized, "-");
        normalized = normalized.Trim('-');
        return normalized;
    }

    private static void AssignFallbackIds(ArchitectureContractDocument document)
    {
        foreach (IArchitectureContract contract in GetAllContracts(document).Where(c => string.IsNullOrEmpty(c.Id)))
        {
            contract.Id = NormalizeToContractId(contract.Name);
        }
    }

    [GeneratedRegex(@"[^a-z0-9-]", RegexOptions.Compiled)]
    private static partial Regex NonAlphaNumDashPattern();
    [GeneratedRegex("-{2,}", RegexOptions.Compiled)]
    private static partial Regex MultiDashPattern();

    private static IEnumerable<IArchitectureContract> GetAllContracts(ArchitectureContractDocument document)
    {
        return document.Contracts.AllStrict
            .Concat(document.Contracts.AllAudit)
            .Concat(document.Contracts.StrictLayerTemplates)
            .Concat(document.Contracts.AuditLayerTemplates);
    }
}
