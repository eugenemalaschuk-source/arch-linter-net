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

    public string Compose(IReadOnlyList<ArchitecturePolicySource> sources)
    {
        _declarations.Clear();
        _contractIds.Clear();
        var effective = new YamlMappingNode();

        foreach (ArchitecturePolicySource source in sources)
        {
            ComposeSource(effective, source);
        }

        foreach (string required in new[] { "version", "name", "layers", "analysis", "contracts" })
        {
            if (!ArchitecturePolicySourceParser.TryGetChild(effective, required, out _))
            {
                throw Shape($"Composed policy is missing required section '{required}'.");
            }
        }

        var stream = new YamlStream(new YamlDocument(effective));
        using var writer = new StringWriter();
        stream.Save(writer, assignAnchors: false);
        return writer.ToString();
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
        YamlMappingNode target = GetOrAddMapping(effective, section);
        foreach ((YamlNode keyNode, YamlNode value) in sourceMap.Children)
        {
            string key = ScalarKey(keyNode, source, section, "definition key");
            string yamlPath = $"{section}.{key}";
            Register($"map:{yamlPath}", source, yamlPath);
            target.Add(new YamlScalarNode(key), value);
        }
    }

    private void MergeAnalysis(YamlMappingNode effective, YamlNode value, ArchitecturePolicySource source)
    {
        YamlMappingNode sourceMap = RequireMapping(value, source, "analysis");
        YamlMappingNode target = GetOrAddMapping(effective, "analysis");
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
        YamlMappingNode target = GetOrAddMapping(effective, "contracts");
        foreach ((YamlNode keyNode, YamlNode child) in sourceMap.Children)
        {
            string group = ScalarKey(keyNode, source, "contracts", "contract group");
            string path = $"contracts.{group}";
            YamlSequenceNode sourceSequence = RequireSequence(child, source, path);
            YamlSequenceNode targetSequence = GetOrAddSequence(target, group);
            int firstIndex = targetSequence.Children.Count;
            foreach (YamlNode contract in sourceSequence.Children)
            {
                RegisterContractId(group, contract, source, firstIndex++);
                targetSequence.Add(contract);
            }
        }
    }

    private void MergeClassification(YamlMappingNode effective, YamlNode value, ArchitecturePolicySource source)
    {
        YamlMappingNode sourceMap = RequireMapping(value, source, "classification");
        YamlMappingNode target = GetOrAddMapping(effective, "classification");
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
        YamlMappingNode target = GetOrAddMapping(parent, key);
        foreach ((YamlNode childKeyNode, YamlNode childValue) in sourceMap.Children)
        {
            string childKey = ScalarKey(childKeyNode, source, path, "definition key");
            string childPath = $"{path}.{childKey}";
            Register($"map:{childPath}", source, childPath);
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
        YamlSequenceNode target = GetOrAddSequence(parent, key);
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
        Register($"singleton:{path}", source, path);
        parent.Add(new YamlScalarNode(key), value);
    }

    private void RegisterContractId(
        string group,
        YamlNode node,
        ArchitecturePolicySource source,
        int index)
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

        string path = $"contracts.{group}[{index}]";
        string declarationKey = $"{group}\0{id}";
        var declaration = new Declaration(source.PortableIdentity, path);
        if (_contractIds.TryGetValue(declarationKey, out Declaration? first))
        {
            throw Conflict(
                $"Duplicate contract id '{id}' in '{group}' at {first.Source}:{first.Path} and {declaration.Source}:{declaration.Path}.");
        }

        _contractIds.Add(declarationKey, declaration);
    }

    private void Register(string key, ArchitecturePolicySource source, string path)
    {
        var declaration = new Declaration(source.PortableIdentity, path);
        if (_declarations.TryGetValue(key, out Declaration? first))
        {
            throw Conflict(
                $"Policy composition conflict at '{path}' between {first.Source}:{first.Path} and {declaration.Source}:{declaration.Path}.");
        }

        _declarations.Add(key, declaration);
    }

    private static YamlMappingNode GetOrAddMapping(YamlMappingNode parent, string key)
    {
        if (ArchitecturePolicySourceParser.TryGetChild(parent, key, out YamlNode? value))
        {
            return (YamlMappingNode)value!;
        }

        var mapping = new YamlMappingNode();
        parent.Add(new YamlScalarNode(key), mapping);
        return mapping;
    }

    private static YamlSequenceNode GetOrAddSequence(YamlMappingNode parent, string key)
    {
        if (ArchitecturePolicySourceParser.TryGetChild(parent, key, out YamlNode? value))
        {
            return (YamlSequenceNode)value!;
        }

        var sequence = new YamlSequenceNode();
        parent.Add(new YamlScalarNode(key), sequence);
        return sequence;
    }

    private static YamlMappingNode RequireMapping(YamlNode node, ArchitecturePolicySource source, string path)
    {
        return node as YamlMappingNode
            ?? throw Shape($"Policy source '{source.PortableIdentity}' field '{path}' must be a mapping.");
    }

    private static YamlSequenceNode RequireSequence(YamlNode node, ArchitecturePolicySource source, string path)
    {
        return node as YamlSequenceNode
            ?? throw Shape($"Policy source '{source.PortableIdentity}' field '{path}' must be a sequence.");
    }

    private static string ScalarKey(
        YamlNode node,
        ArchitecturePolicySource source,
        string path,
        string description)
    {
        return node is YamlScalarNode { Value: { } value }
            ? value
            : throw Shape($"Policy source '{source.PortableIdentity}' has a non-scalar {description} at '{path}'.");
    }

    private static string? ScalarValue(YamlMappingNode mapping, string key)
    {
        return ArchitecturePolicySourceParser.TryGetChild(mapping, key, out YamlNode? node)
            && node is YamlScalarNode scalar
                ? scalar.Value
                : null;
    }

    private static ArchitecturePolicyImportException Shape(string message)
    {
        return new ArchitecturePolicyImportException(ArchitecturePolicyImportErrorCategory.SourceShape, message);
    }

    private static ArchitecturePolicyImportException Conflict(string message)
    {
        return new ArchitecturePolicyImportException(
            ArchitecturePolicyImportErrorCategory.CompositionConflict,
            message);
    }

    private sealed record Declaration(string Source, string Path);
}
