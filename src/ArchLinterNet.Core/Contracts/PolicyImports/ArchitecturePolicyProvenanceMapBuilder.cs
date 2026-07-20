using ArchLinterNet.Core.Model;
using YamlDotNet.RepresentationModel;

namespace ArchLinterNet.Core.Contracts.PolicyImports;

internal sealed record ArchitecturePolicyCompositionResult(
    string Yaml,
    ArchitecturePolicyProvenanceIndex Provenance);

internal sealed class ArchitecturePolicyProvenanceMapBuilder
{
    private readonly Dictionary<string, ArchitecturePolicySourceLocation> _nodes =
        new(StringComparer.Ordinal);
    private int _nextEncounterOrdinal;

    public void AddTree(
        YamlNode node,
        ArchitecturePolicySource source,
        string originalPath,
        string effectivePath,
        string? contractFamily = null,
        string? contractId = null)
    {
        (string? family, string? id) = ResolveContractContext(
            node, effectivePath, contractFamily, contractId);
        _nodes[effectivePath] = CreateLocation(source, originalPath, node, family, id);

        if (node is YamlMappingNode mapping)
        {
            foreach ((YamlNode keyNode, YamlNode valueNode) in mapping.Children)
            {
                if (keyNode is not YamlScalarNode { Value: { } key })
                {
                    continue;
                }

                AddTree(
                    valueNode,
                    source,
                    AppendYamlProperty(originalPath, key),
                    ArchitecturePolicyProvenancePath.AppendProperty(effectivePath, key),
                    family,
                    id);
            }
        }
        else if (node is YamlSequenceNode sequence)
        {
            for (int index = 0; index < sequence.Children.Count; index++)
            {
                AddTree(
                    sequence.Children[index],
                    source,
                    $"{originalPath}[{index}]",
                    ArchitecturePolicyProvenancePath.AppendIndex(effectivePath, index),
                    family,
                    id);
            }
        }
    }

    public void AddSequenceItems(
        YamlSequenceNode sequence,
        ArchitecturePolicySource source,
        string originalPath,
        string effectivePath,
        int effectiveStartIndex,
        string? contractFamily = null)
    {
        for (int index = 0; index < sequence.Children.Count; index++)
        {
            YamlNode child = sequence.Children[index];
            string? contractId = contractFamily is null ? null : ReadContractId(child);
            AddTree(
                child,
                source,
                $"{originalPath}[{index}]",
                ArchitecturePolicyProvenancePath.AppendIndex(effectivePath, effectiveStartIndex + index),
                contractFamily,
                contractId);
        }
    }

    public ArchitecturePolicyProvenanceIndex Build(IReadOnlyList<ArchitecturePolicySource> sources)
    {
        return new ArchitecturePolicyProvenanceIndex(
            sources.Select(source => source.Descriptor).ToArray(),
            _nodes);
    }

    private ArchitecturePolicySourceLocation CreateLocation(
        ArchitecturePolicySource source,
        string yamlPath,
        YamlNode node,
        string? contractFamily,
        string? contractId)
    {
        return new ArchitecturePolicySourceLocation(
            source.Descriptor,
            yamlPath,
            checked((int)Math.Max(1, node.Start.Line + 1)),
            checked((int)Math.Max(1, node.Start.Column + 1)),
            contractFamily,
            contractId,
            _nextEncounterOrdinal++);
    }

    private static (string? Family, string? Id) ResolveContractContext(
        YamlNode node,
        string effectivePath,
        string? currentFamily,
        string? currentId)
    {
        if (currentFamily is not null)
        {
            return (currentFamily, currentId);
        }

        if (!ArchitecturePolicyProvenancePath.TryGetContractGroup(effectivePath, out string family))
        {
            return (null, null);
        }

        return (family, ReadContractId(node));
    }

    private static string? ReadContractId(YamlNode node)
    {
        if (node is not YamlMappingNode mapping)
        {
            return null;
        }

        string? id = ScalarValue(mapping, "id");
        if (!string.IsNullOrWhiteSpace(id))
        {
            return id;
        }

        string? name = ScalarValue(mapping, "name");
        return string.IsNullOrWhiteSpace(name)
            ? null
            : ArchitecturePolicyDocumentLoader.NormalizeToContractId(name);
    }

    private static string? ScalarValue(YamlMappingNode mapping, string key)
    {
        return ArchitecturePolicySourceParser.TryGetChild(mapping, key, out YamlNode? node)
            && node is YamlScalarNode scalar
                ? scalar.Value
                : null;
    }

    private static string AppendYamlProperty(string parent, string property)
    {
        return parent == "$" ? property : $"{parent}.{property}";
    }
}

internal static class ArchitecturePolicyProvenanceFactory
{
    public static ArchitecturePolicyProvenanceIndex CreateMonolithic(
        IArchitecturePolicyPathResolver pathResolver,
        string policyPath,
        string yaml)
    {
        ArchitecturePolicySourceDescriptor descriptor = CreateRootDescriptor(pathResolver, policyPath);
        var stream = new YamlStream();
        stream.Load(new StringReader(yaml));
        if (stream.Documents.Count != 1 || stream.Documents[0].RootNode is not YamlMappingNode root)
        {
            return new ArchitecturePolicyProvenanceIndex(
                new[] { descriptor },
                new Dictionary<string, ArchitecturePolicySourceLocation>(StringComparer.Ordinal));
        }

        var source = new ArchitecturePolicySource(
            descriptor,
            policyPath,
            policyPath,
            policyPath,
            root,
            Array.Empty<string>());
        var builder = new ArchitecturePolicyProvenanceMapBuilder();
        builder.AddTree(root, source, "$", ArchitecturePolicyProvenancePath.Root);
        return builder.Build(new[] { source });
    }

    internal static ArchitecturePolicySourceDescriptor CreateRootDescriptor(
        IArchitecturePolicyPathResolver pathResolver,
        string policyPath)
    {
        string sourcePath;
        try
        {
            ArchitecturePolicyRootPath root = pathResolver.ResolveRoot(policyPath);
            sourcePath = Path.GetRelativePath(root.BoundaryPath, root.FullPath)
                .Replace(Path.DirectorySeparatorChar, '/');
        }
        catch (ArchitecturePolicyImportException)
        {
            return CreateUnresolvedRootDescriptor(policyPath);
        }

        return new ArchitecturePolicySourceDescriptor(
            sourcePath,
            sourcePath,
            ArchitecturePolicyDocumentRole.Root,
            0,
            null,
            null,
            new[] { sourcePath });
    }

    internal static ArchitecturePolicySourceDescriptor CreateUnresolvedRootDescriptor(string policyPath)
    {
        string normalizedPath = Path.GetFullPath(policyPath);
        string sourcePath = Path.GetFileName(normalizedPath);
        string? directory = Path.GetDirectoryName(normalizedPath);
        if (directory is not null && string.Equals(
                Path.GetFileName(directory),
                "architecture",
                StringComparison.OrdinalIgnoreCase))
        {
            sourcePath = $"architecture/{sourcePath}";
        }

        return new ArchitecturePolicySourceDescriptor(
            sourcePath,
            sourcePath,
            ArchitecturePolicyDocumentRole.Root,
            0,
            null,
            null,
            new[] { sourcePath });
    }
}
