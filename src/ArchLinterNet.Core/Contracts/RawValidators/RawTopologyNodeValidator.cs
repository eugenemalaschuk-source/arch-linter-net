using ArchLinterNet.Core.Contracts.PolicyImports;
using YamlDotNet.RepresentationModel;

namespace ArchLinterNet.Core.Contracts.RawValidators;

// `IgnoreUnmatchedProperties()` intentionally keeps ordinary policy loading resilient, but topology
// is a completeness boundary: a misspelled selector or an inert scope key must not silently change
// the declared universe. Validate its closed object shapes before deserialization erases unknown keys.
internal sealed class RawTopologyNodeValidator : IArchitecturePolicyRawDocumentValidator
{
    private static readonly string[] _topologyKeys =
        ["mode", "subject_kind", "scope", "nodes", "allowed_edges", "out_of_scope", "stale_declarations"];
    private static readonly string[] _scopeKeys = ["allow_empty", "selectors"];
    private static readonly string[] _nodeKeys = ["id", "mappings"];
    private static readonly string[] _edgeKeys = ["from", "to"];
    private static readonly string[] _outOfScopeKeys = ["id", "selector", "reason"];
    private static readonly string[] _selectorKeys = ["layer", "namespace", "namespace_suffix", "project", "assembly", "context"];
    private static readonly string[] _contextKeys = ["role", "metadata", "when"];

    public void Validate(ArchitecturePolicyRawDocument document)
    {
        if (!document.TryGetSection("topology", out YamlMappingNode? topology))
        {
            return;
        }

        document.Provenance.SetValidationSubject(ArchitecturePolicyProvenancePath.Property("topology"));
        ValidateKnownKeys(topology, "topology", _topologyKeys);
        ValidateScope(document, topology);
        ValidateNodes(document, topology);
        ValidateEdges(document, topology);
        ValidateOutOfScope(document, topology);
    }

    private static void ValidateScope(ArchitecturePolicyRawDocument document, YamlMappingNode topology)
    {
        if (!RawYamlNodes.TryGetChild(topology, "scope", out YamlNode? scopeNode))
        {
            return;
        }

        if (scopeNode is not YamlMappingNode scope)
        {
            throw new InvalidOperationException("Topology 'scope' must be an object.");
        }

        ValidateKnownKeys(scope, "topology scope", _scopeKeys);
        ValidateSelectorList(document, scope, "selectors", "topology.scope.selectors");
    }

    private static void ValidateNodes(ArchitecturePolicyRawDocument document, YamlMappingNode topology)
    {
        if (!RawYamlNodes.TryGetChild(topology, "nodes", out YamlNode? nodesNode))
        {
            return;
        }

        if (nodesNode is not YamlSequenceNode nodes)
        {
            throw new InvalidOperationException("Topology 'nodes' must be a list of objects.");
        }

        for (int index = 0; index < nodes.Children.Count; index++)
        {
            document.Provenance.SetValidationSubject(TopologyPath("nodes", index));
            if (nodes.Children[index] is not YamlMappingNode node)
            {
                throw new InvalidOperationException($"Topology node {index} must be an object.");
            }

            ValidateKnownKeys(node, $"Topology node {index}", _nodeKeys);
            ValidateSelectorList(document, node, "mappings", $"topology.nodes[{index}].mappings");
        }
    }

    private static void ValidateEdges(ArchitecturePolicyRawDocument document, YamlMappingNode topology)
    {
        if (!RawYamlNodes.TryGetChild(topology, "allowed_edges", out YamlNode? edgesNode))
        {
            return;
        }

        if (edgesNode is not YamlSequenceNode edges)
        {
            throw new InvalidOperationException("Topology 'allowed_edges' must be a list of objects.");
        }

        for (int index = 0; index < edges.Children.Count; index++)
        {
            document.Provenance.SetValidationSubject(TopologyPath("allowed_edges", index));
            if (edges.Children[index] is not YamlMappingNode edge)
            {
                throw new InvalidOperationException($"Topology allowed edge {index} must be an object.");
            }

            ValidateKnownKeys(edge, $"Topology allowed edge {index}", _edgeKeys);
        }
    }

    private static void ValidateOutOfScope(ArchitecturePolicyRawDocument document, YamlMappingNode topology)
    {
        if (!RawYamlNodes.TryGetChild(topology, "out_of_scope", out YamlNode? entriesNode))
        {
            return;
        }

        if (entriesNode is not YamlSequenceNode entries)
        {
            throw new InvalidOperationException("Topology 'out_of_scope' must be a list of objects.");
        }

        for (int index = 0; index < entries.Children.Count; index++)
        {
            document.Provenance.SetValidationSubject(TopologyPath("out_of_scope", index));
            if (entries.Children[index] is not YamlMappingNode entry)
            {
                throw new InvalidOperationException($"Topology out_of_scope entry {index} must be an object.");
            }

            ValidateKnownKeys(entry, $"Topology out_of_scope entry {index}", _outOfScopeKeys);
            ValidateSelector(document, entry, "selector", $"topology.out_of_scope[{index}].selector");
        }
    }

    private static void ValidateSelectorList(
        ArchitecturePolicyRawDocument document, YamlMappingNode parent, string key, string location)
    {
        if (!RawYamlNodes.TryGetChild(parent, key, out YamlNode? selectorsNode))
        {
            return;
        }

        if (selectorsNode is not YamlSequenceNode selectors)
        {
            throw new InvalidOperationException($"Topology '{location}' must be a list of selector objects.");
        }

        for (int index = 0; index < selectors.Children.Count; index++)
        {
            document.Provenance.SetValidationSubject(ArchitecturePolicyProvenancePath.AppendIndex(location, index));
            if (selectors.Children[index] is not YamlMappingNode selector)
            {
                throw new InvalidOperationException($"Topology '{location}[{index}]' must be a selector object.");
            }

            ValidateSelectorNode(selector, $"Topology '{location}[{index}]'");
        }
    }

    private static void ValidateSelector(
        ArchitecturePolicyRawDocument document, YamlMappingNode parent, string key, string location)
    {
        if (!RawYamlNodes.TryGetChild(parent, key, out YamlNode? selectorNode))
        {
            return;
        }

        document.Provenance.SetValidationSubject(location);
        if (selectorNode is not YamlMappingNode selector)
        {
            throw new InvalidOperationException($"Topology '{location}' must be a selector object.");
        }

        ValidateSelectorNode(selector, $"Topology '{location}'");
    }

    private static void ValidateSelectorNode(YamlMappingNode selector, string location)
    {
        ValidateKnownKeys(selector, location, _selectorKeys);
        if (RawYamlNodes.TryGetChild(selector, "context", out YamlNode? contextNode))
        {
            if (contextNode is not YamlMappingNode context)
            {
                throw new InvalidOperationException($"{location} context must be an object.");
            }

            ValidateKnownKeys(context, $"{location} context", _contextKeys);
            if (RawYamlNodes.TryGetChild(context, "metadata", out YamlNode? metadata)
                && (RawYamlNodes.IsExplicitNull(metadata) || metadata is not YamlMappingNode))
            {
                throw new InvalidOperationException($"{location} context metadata must be an object when declared.");
            }
        }
    }

    private static void ValidateKnownKeys(YamlMappingNode node, string location, IEnumerable<string> allowed)
    {
        foreach ((YamlNode key, _) in node.Children)
        {
            if (key is YamlScalarNode scalar && !allowed.Contains(scalar.Value, StringComparer.Ordinal))
            {
                throw new InvalidOperationException($"{location} contains unknown property '{scalar.Value}'.");
            }
        }
    }

    private static string TopologyPath(string property, int index) => ArchitecturePolicyProvenancePath.AppendIndex(
        ArchitecturePolicyProvenancePath.AppendProperty(ArchitecturePolicyProvenancePath.Property("topology"), property), index);
}
