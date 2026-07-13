using ArchLinterNet.Core.Model;
using YamlDotNet.RepresentationModel;

namespace ArchLinterNet.Core.Contracts.PolicyImports;

internal sealed class ArchitecturePolicyDocumentComposer
{
    private static readonly HashSet<string> _keyedSections = new(StringComparer.Ordinal)
    {
        "layers", "external_dependencies", "packages"
    };

    private readonly Dictionary<string, Declaration> _declarations = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Declaration> _contractIds = new(StringComparer.OrdinalIgnoreCase);
    private ArchitecturePolicyProvenanceMapBuilder _provenance = new();

    public ArchitecturePolicyCompositionResult Compose(IReadOnlyList<ArchitecturePolicySource> sources)
    {
        _declarations.Clear();
        _contractIds.Clear();
        _provenance = new ArchitecturePolicyProvenanceMapBuilder();
        var effective = new YamlMappingNode();

        foreach (ArchitecturePolicySource source in sources)
        {
            ComposeSource(effective, source);
        }

        foreach (string required in new[] { "version", "name", "layers", "analysis", "contracts" })
        {
            if (!ArchitecturePolicySourceParser.TryGetChild(effective, required, out _))
            {
                ArchitecturePolicySource rootSource = sources[0];
                throw Shape(
                    $"Composed policy is missing required section '{required}'.",
                    rootSource,
                    "$",
                    rootSource.Root);
            }
        }

        var stream = new YamlStream(new YamlDocument(effective));
        using var writer = new StringWriter();
        stream.Save(writer, assignAnchors: false);
        return new ArchitecturePolicyCompositionResult(writer.ToString(), _provenance.Build(sources));
    }

    private void ComposeSource(YamlMappingNode effective, ArchitecturePolicySource source)
    {
        foreach ((YamlNode keyNode, YamlNode value) in source.Root.Children)
        {
            string key = ScalarKey(keyNode, source, "$", "top-level field");
            if (key == "imports")
            {
                continue;
            }

            if (key is "version" or "name")
            {
                AddSingleton(effective, key, value, source, key);
            }
            else if (_keyedSections.Contains(key))
            {
                MergeKeyedSection(effective, key, value, source);
            }
            else if (key == "legacy_runtime_layers")
            {
                AppendSequence(effective, key, value, source, key);
            }
            else if (key == "analysis")
            {
                MergeAnalysis(effective, value, source);
            }
            else if (key == "contracts")
            {
                MergeContracts(effective, value, source);
            }
            else if (key == "classification")
            {
                MergeClassification(effective, value, source);
            }
        }
    }

    private void MergeKeyedSection(
        YamlMappingNode effective,
        string section,
        YamlNode sourceValue,
        ArchitecturePolicySource source)
    {
        YamlMappingNode sourceMap = RequireMapping(sourceValue, source, section);
        YamlMappingNode target = GetOrAddMapping(effective, section, source, section);
        foreach ((YamlNode keyNode, YamlNode value) in sourceMap.Children)
        {
            string key = ScalarKey(keyNode, source, section, "definition key");
            string yamlPath = $"{section}.{key}";
            Register($"map:{yamlPath}", source, yamlPath, value);
            _provenance.AddTree(value, source, yamlPath, yamlPath);
            target.Add(new YamlScalarNode(key), value);
        }
    }

    private void MergeAnalysis(YamlMappingNode effective, YamlNode value, ArchitecturePolicySource source)
    {
        YamlMappingNode sourceMap = RequireMapping(value, source, "analysis");
        YamlMappingNode target = GetOrAddMapping(effective, "analysis", source, "analysis");
        foreach ((YamlNode keyNode, YamlNode child) in sourceMap.Children)
        {
            string key = ScalarKey(keyNode, source, "analysis", "analysis field");
            string path = $"analysis.{key}";
            if (key == "condition_sets")
            {
                MergeNestedMap(target, key, child, source, path);
            }
            else if (child is YamlSequenceNode)
            {
                AppendSequence(target, key, child, source, path);
            }
            else
            {
                AddSingleton(target, key, child, source, path);
            }
        }
    }

    private void MergeContracts(YamlMappingNode effective, YamlNode value, ArchitecturePolicySource source)
    {
        YamlMappingNode sourceMap = RequireMapping(value, source, "contracts");
        YamlMappingNode target = GetOrAddMapping(effective, "contracts", source, "contracts");
        foreach ((YamlNode keyNode, YamlNode child) in sourceMap.Children)
        {
            string group = ScalarKey(keyNode, source, "contracts", "contract group");
            string path = $"contracts.{group}";
            YamlSequenceNode sourceSequence = RequireSequence(child, source, path);
            YamlSequenceNode targetSequence = GetOrAddSequence(target, group, source, path);
            int firstIndex = targetSequence.Children.Count;
            _provenance.AddSequenceItems(
                sourceSequence,
                source,
                path,
                path,
                firstIndex,
                group);
            for (int sourceIndex = 0; sourceIndex < sourceSequence.Children.Count; sourceIndex++)
            {
                YamlNode contract = sourceSequence.Children[sourceIndex];
                RegisterContractId(group, contract, source, sourceIndex);
                firstIndex++;
                targetSequence.Add(contract);
            }
        }
    }

    private void MergeClassification(YamlMappingNode effective, YamlNode value, ArchitecturePolicySource source)
    {
        YamlMappingNode sourceMap = RequireMapping(value, source, "classification");
        YamlMappingNode target = GetOrAddMapping(effective, "classification", source, "classification");
        foreach ((YamlNode keyNode, YamlNode child) in sourceMap.Children)
        {
            string key = ScalarKey(keyNode, source, "classification", "classification field");
            string path = $"classification.{key}";
            if (key == "precedence")
            {
                AddSingleton(target, key, child, source, path);
            }
            else
            {
                AppendSequence(target, key, child, source, path);
            }
        }
    }

    private void MergeNestedMap(
        YamlMappingNode parent,
        string key,
        YamlNode value,
        ArchitecturePolicySource source,
        string path)
    {
        YamlMappingNode sourceMap = RequireMapping(value, source, path);
        YamlMappingNode target = GetOrAddMapping(parent, key, source, path);
        foreach ((YamlNode childKeyNode, YamlNode childValue) in sourceMap.Children)
        {
            string childKey = ScalarKey(childKeyNode, source, path, "definition key");
            string childPath = $"{path}.{childKey}";
            Register($"map:{childPath}", source, childPath, childValue);
            _provenance.AddTree(childValue, source, childPath, childPath);
            target.Add(new YamlScalarNode(childKey), childValue);
        }
    }

    private void AppendSequence(
        YamlMappingNode parent,
        string key,
        YamlNode value,
        ArchitecturePolicySource source,
        string path)
    {
        YamlSequenceNode sourceSequence = RequireSequence(value, source, path);
        YamlSequenceNode target = GetOrAddSequence(parent, key, source, path);
        _provenance.AddSequenceItems(
            sourceSequence,
            source,
            path,
            path,
            target.Children.Count);
        foreach (YamlNode child in sourceSequence.Children)
        {
            target.Add(child);
        }
    }

    private void AddSingleton(
        YamlMappingNode parent,
        string key,
        YamlNode value,
        ArchitecturePolicySource source,
        string path)
    {
        Register($"singleton:{path}", source, path, value);
        _provenance.AddTree(value, source, path, path);
        parent.Add(new YamlScalarNode(key), value);
    }

    private void RegisterContractId(
        string group,
        YamlNode node,
        ArchitecturePolicySource source,
        int sourceIndex)
    {
        if (node is not YamlMappingNode contract)
        {
            return;
        }

        string? id = ScalarValue(contract, "id");
        if (string.IsNullOrEmpty(id))
        {
            string? name = ScalarValue(contract, "name");
            if (string.IsNullOrEmpty(name))
            {
                return;
            }

            id = ArchitecturePolicyDocumentLoader.NormalizeToContractId(name);
        }

        string path = $"contracts.{group}[{sourceIndex}]";
        string declarationKey = $"{group}\0{id}";
        ArchitecturePolicySourceLocation location = ArchitecturePolicyDiagnosticFactory.Location(
            source,
            path,
            node,
            group,
            id);
        var declaration = new Declaration(location);
        if (_contractIds.TryGetValue(declarationKey, out Declaration? first))
        {
            throw Conflict(
                $"Duplicate contract id '{id}' in '{group}' at " +
                $"{first.Location.SourcePath}:{first.Location.YamlPath} and " +
                $"{declaration.Location.SourcePath}:{declaration.Location.YamlPath}.",
                first.Location,
                declaration.Location);
        }

        _contractIds.Add(declarationKey, declaration);
    }

    private void Register(string key, ArchitecturePolicySource source, string path, YamlNode node)
    {
        var declaration = new Declaration(ArchitecturePolicyDiagnosticFactory.Location(source, path, node));
        if (_declarations.TryGetValue(key, out Declaration? first))
        {
            throw Conflict(
                $"Policy composition conflict at '{path}' between " +
                $"{first.Location.SourcePath}:{first.Location.YamlPath} and " +
                $"{declaration.Location.SourcePath}:{declaration.Location.YamlPath}.",
                first.Location,
                declaration.Location);
        }

        _declarations.Add(key, declaration);
    }

    private static YamlMappingNode GetOrAddMapping(
        YamlMappingNode parent,
        string key,
        ArchitecturePolicySource source,
        string path)
    {
        if (ArchitecturePolicySourceParser.TryGetChild(parent, key, out YamlNode? value))
        {
            return RequireMapping(value!, source, path);
        }

        var mapping = new YamlMappingNode();
        parent.Add(new YamlScalarNode(key), mapping);
        return mapping;
    }

    private static YamlSequenceNode GetOrAddSequence(
        YamlMappingNode parent,
        string key,
        ArchitecturePolicySource source,
        string path)
    {
        if (ArchitecturePolicySourceParser.TryGetChild(parent, key, out YamlNode? value))
        {
            return RequireSequence(value!, source, path);
        }

        var sequence = new YamlSequenceNode();
        parent.Add(new YamlScalarNode(key), sequence);
        return sequence;
    }

    private static YamlMappingNode RequireMapping(YamlNode node, ArchitecturePolicySource source, string path)
    {
        return node as YamlMappingNode
            ?? throw Shape(
                $"Policy source '{source.PortableIdentity}' field '{path}' must be a mapping.",
                source,
                path,
                node);
    }

    private static YamlSequenceNode RequireSequence(YamlNode node, ArchitecturePolicySource source, string path)
    {
        return node as YamlSequenceNode
            ?? throw Shape(
                $"Policy source '{source.PortableIdentity}' field '{path}' must be a sequence.",
                source,
                path,
                node);
    }

    private static string ScalarKey(
        YamlNode node,
        ArchitecturePolicySource source,
        string path,
        string description)
    {
        return node is YamlScalarNode { Value: { } value }
            ? value
            : throw Shape(
                $"Policy source '{source.PortableIdentity}' has a non-scalar {description} at '{path}'.",
                source,
                path,
                node);
    }

    private static string? ScalarValue(YamlMappingNode mapping, string key)
    {
        return ArchitecturePolicySourceParser.TryGetChild(mapping, key, out YamlNode? node)
            && node is YamlScalarNode scalar
                ? scalar.Value
                : null;
    }

    private static ArchitecturePolicyImportException Shape(
        string message,
        ArchitecturePolicySource source,
        string path,
        YamlNode node)
    {
        return ArchitecturePolicyDiagnosticFactory.Exception(
            ArchitecturePolicyImportErrorCategory.SourceShape,
            message,
            ArchitecturePolicyDiagnosticFactory.Location(source, path, node));
    }

    private static ArchitecturePolicyImportException Conflict(
        string message,
        ArchitecturePolicySourceLocation original,
        ArchitecturePolicySourceLocation conflicting)
    {
        return ArchitecturePolicyDiagnosticFactory.Exception(
            ArchitecturePolicyImportErrorCategory.CompositionConflict,
            message,
            original,
            new[] { conflicting },
            original.Source.ImportChain);
    }

    private sealed record Declaration(ArchitecturePolicySourceLocation Location);
}
