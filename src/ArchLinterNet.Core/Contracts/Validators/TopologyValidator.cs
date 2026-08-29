using ArchLinterNet.Core.Contracts.PolicyImports;
using ArchLinterNet.Core.Resolution;

namespace ArchLinterNet.Core.Contracts.Validators;

// Validates the typed native topology declaration after provenance binding. Observed-subject
// matching is intentionally deferred to #509; this boundary only establishes a complete,
// deterministic, and reviewable policy model for that evaluator to consume.
internal sealed class TopologyValidator : IArchitecturePolicyDocumentValidator
{
    private static readonly HashSet<string> _modes = ["partial", "exhaustive"];
    private static readonly HashSet<string> _subjectKinds = ["type", "namespace", "project", "assembly"];
    private static readonly IReadOnlyDictionary<string, HashSet<string>> _selectorKindsBySubjectKind =
        new Dictionary<string, HashSet<string>>(StringComparer.Ordinal)
        {
            ["type"] = ["layer", "namespace", "project", "assembly", "context"],
            ["namespace"] = ["namespace", "project", "assembly"],
            ["project"] = ["project"],
            ["assembly"] = ["assembly"],
        };

    public void Validate(ArchitectureContractDocument document)
    {
        ArchitectureTopology? topology = document.Topology;
        if (topology is null)
        {
            return;
        }

        document.Provenance.SetValidationSubject(ArchitecturePolicyProvenancePath.Property("topology"));
        ValidateMode(topology);
        ValidateSubjectKind(topology);
        ValidateScope(document, topology);
        ValidateNodes(document, topology);
        ValidateEdges(document, topology);
        ValidateOutOfScope(document, topology);
    }

    private static void ValidateMode(ArchitectureTopology topology)
    {
        if (!_modes.Contains(topology.Mode))
        {
            throw new InvalidOperationException("Topology mode must be either 'partial' or 'exhaustive'.");
        }
    }

    private static void ValidateSubjectKind(ArchitectureTopology topology)
    {
        if (!_subjectKinds.Contains(topology.SubjectKind))
        {
            throw new InvalidOperationException(
                "Topology subject_kind must be one of 'type', 'namespace', 'project', or 'assembly'.");
        }
    }

    private static void ValidateScope(ArchitectureContractDocument document, ArchitectureTopology topology)
    {
        if (topology.Scope is null || topology.Scope.Selectors is null || topology.Scope.Selectors.Count == 0)
        {
            throw new InvalidOperationException("Topology scope must declare at least one bounded selector.");
        }

        string selectorPath = ArchitecturePolicyProvenancePath.AppendProperty(
            ArchitecturePolicyProvenancePath.AppendProperty(ArchitecturePolicyProvenancePath.Property("topology"), "scope"), "selectors");
        ValidateSelectorList(document, topology.Scope.Selectors, topology.SubjectKind, selectorPath, "Topology scope");
    }

    private static void ValidateNodes(ArchitectureContractDocument document, ArchitectureTopology topology)
    {
        if (topology.Nodes is null || topology.Nodes.Count == 0)
        {
            throw new InvalidOperationException("Topology must declare at least one node.");
        }

        var nodeIds = new HashSet<string>(StringComparer.Ordinal);
        var mappings = new Dictionary<TopologySelectorIdentity, string>();
        string nodesPath = ArchitecturePolicyProvenancePath.AppendProperty(ArchitecturePolicyProvenancePath.Property("topology"), "nodes");
        for (int index = 0; index < topology.Nodes.Count; index++)
        {
            ArchitectureTopologyNode? node = topology.Nodes[index];
            string nodePath = ArchitecturePolicyProvenancePath.AppendIndex(nodesPath, index);
            document.Provenance.SetValidationSubject(nodePath);
            if (node is null)
            {
                throw new InvalidOperationException($"Topology node {index} must not be null.");
            }

            if (string.IsNullOrWhiteSpace(node.Id))
            {
                throw new InvalidOperationException($"Topology node {index} must declare a non-empty id.");
            }

            if (!nodeIds.Add(node.Id))
            {
                throw new InvalidOperationException($"Topology declares duplicate node id '{node.Id}'.");
            }

            if (node.Mappings is null || node.Mappings.Count == 0)
            {
                throw new InvalidOperationException($"Topology node '{node.Id}' must declare at least one mapping selector.");
            }

            string mappingPath = ArchitecturePolicyProvenancePath.AppendProperty(nodePath, "mappings");
            for (int mappingIndex = 0; mappingIndex < node.Mappings.Count; mappingIndex++)
            {
                string path = ArchitecturePolicyProvenancePath.AppendIndex(mappingPath, mappingIndex);
                TopologySelectorIdentity identity = ValidateSelector(
                    document, node.Mappings[mappingIndex], topology.SubjectKind, path, $"Topology node '{node.Id}' mapping");
                if (mappings.TryGetValue(identity, out string? existingNode))
                {
                    string detail = string.Equals(existingNode, node.Id, StringComparison.Ordinal)
                        ? $"Topology node '{node.Id}' declares duplicate mapping selector."
                        : $"Topology nodes '{existingNode}' and '{node.Id}' declare the same mapping selector, which is unambiguously ambiguous.";
                    throw new InvalidOperationException(detail);
                }

                mappings.Add(identity, node.Id);
            }
        }
    }

    private static void ValidateEdges(ArchitectureContractDocument document, ArchitectureTopology topology)
    {
        if (topology.AllowedEdges is null)
        {
            throw new InvalidOperationException("Topology allowed_edges must be a list when declared.");
        }

        HashSet<string> nodes = topology.Nodes.Select(node => node.Id).ToHashSet(StringComparer.Ordinal);
        var edges = new HashSet<(string From, string To)>();
        string edgesPath = ArchitecturePolicyProvenancePath.AppendProperty(ArchitecturePolicyProvenancePath.Property("topology"), "allowed_edges");
        for (int index = 0; index < topology.AllowedEdges.Count; index++)
        {
            ArchitectureTopologyEdge? edge = topology.AllowedEdges[index];
            document.Provenance.SetValidationSubject(ArchitecturePolicyProvenancePath.AppendIndex(edgesPath, index));
            if (edge is null || string.IsNullOrWhiteSpace(edge.From) || string.IsNullOrWhiteSpace(edge.To))
            {
                throw new InvalidOperationException($"Topology allowed edge {index} must declare non-empty from and to node ids.");
            }

            if (!nodes.Contains(edge.From) || !nodes.Contains(edge.To))
            {
                throw new InvalidOperationException(
                    $"Topology allowed edge '{edge.From}' -> '{edge.To}' references an undeclared node.");
            }

            (string From, string To) identity = (edge.From, edge.To);
            if (!edges.Add(identity))
            {
                throw new InvalidOperationException($"Topology declares duplicate allowed edge '{edge.From}' -> '{edge.To}'.");
            }
        }
    }

    private static void ValidateOutOfScope(ArchitectureContractDocument document, ArchitectureTopology topology)
    {
        if (topology.OutOfScope is null)
        {
            throw new InvalidOperationException("Topology out_of_scope must be a list when declared.");
        }

        var ids = new HashSet<string>(StringComparer.Ordinal);
        string entriesPath = ArchitecturePolicyProvenancePath.AppendProperty(ArchitecturePolicyProvenancePath.Property("topology"), "out_of_scope");
        for (int index = 0; index < topology.OutOfScope.Count; index++)
        {
            ArchitectureTopologyOutOfScopeDeclaration? entry = topology.OutOfScope[index];
            string entryPath = ArchitecturePolicyProvenancePath.AppendIndex(entriesPath, index);
            document.Provenance.SetValidationSubject(entryPath);
            if (entry is null || string.IsNullOrWhiteSpace(entry.Id))
            {
                throw new InvalidOperationException($"Topology out_of_scope entry {index} must declare a non-empty id.");
            }

            if (!ids.Add(entry.Id))
            {
                throw new InvalidOperationException($"Topology declares duplicate out_of_scope id '{entry.Id}'.");
            }

            if (string.IsNullOrWhiteSpace(entry.Reason))
            {
                throw new InvalidOperationException($"Topology out_of_scope entry '{entry.Id}' must declare a non-empty reason.");
            }

            ValidateSelector(document, entry.Selector, topology.SubjectKind, ArchitecturePolicyProvenancePath.AppendProperty(entryPath, "selector"),
                $"Topology out_of_scope entry '{entry.Id}'");
        }
    }

    private static void ValidateSelectorList(
        ArchitectureContractDocument document,
        List<ArchitectureTopologySubjectSelector> selectors,
        string subjectKind,
        string path,
        string label)
    {
        for (int index = 0; index < selectors.Count; index++)
        {
            ValidateSelector(document, selectors[index], subjectKind, ArchitecturePolicyProvenancePath.AppendIndex(path, index), $"{label} selector");
        }
    }

    private static TopologySelectorIdentity ValidateSelector(
        ArchitectureContractDocument document,
        ArchitectureTopologySubjectSelector? selector,
        string subjectKind,
        string path,
        string label)
    {
        document.Provenance.SetValidationSubject(path);
        if (selector is null)
        {
            throw new InvalidOperationException($"{label} must not be null.");
        }

        bool layer = !string.IsNullOrWhiteSpace(selector.Layer);
        bool @namespace = !string.IsNullOrWhiteSpace(selector.Namespace);
        bool project = !string.IsNullOrWhiteSpace(selector.Project);
        bool assembly = !string.IsNullOrWhiteSpace(selector.Assembly);
        bool context = selector.Context is not null;
        int primaryCount = (layer ? 1 : 0) + (@namespace ? 1 : 0) + (project ? 1 : 0) + (assembly ? 1 : 0) + (context ? 1 : 0);
        if (primaryCount != 1)
        {
            throw new InvalidOperationException(
                $"{label} must declare exactly one of layer, namespace, project, assembly, or context.");
        }

        string selectorKind = layer ? "layer"
            : @namespace ? "namespace"
            : project ? "project"
            : assembly ? "assembly"
            : "context";
        if (!_selectorKindsBySubjectKind[subjectKind].Contains(selectorKind))
        {
            throw new InvalidOperationException(
                $"{label} selector kind '{selectorKind}' is not valid for topology subject_kind '{subjectKind}'.");
        }

        if (!@namespace && !string.IsNullOrWhiteSpace(selector.NamespaceSuffix))
        {
            throw new InvalidOperationException($"{label} namespace_suffix requires namespace.");
        }

        if (layer && !document.Layers.ContainsKey(selector.Layer))
        {
            throw new InvalidOperationException($"{label} references undeclared layer '{selector.Layer}'.");
        }

        if (@namespace)
        {
            try
            {
                _ = selector.NamespacePattern;
            }
            catch (InvalidNamespacePatternException exception)
            {
                throw new InvalidOperationException($"{label}: {exception.Message}", exception);
            }
        }

        if (context)
        {
            ValidateContext(label, selector.Context!);
        }

        return TopologySelectorIdentity.Create(selector, selectorKind);
    }

    private static void ValidateContext(string label, ArchitectureContextSelector context)
    {
        if (string.IsNullOrWhiteSpace(context.Role))
        {
            throw new InvalidOperationException($"{label} context must declare a non-empty role.");
        }

        if (context.Metadata is null || context.Metadata.Keys.Any(string.IsNullOrWhiteSpace))
        {
            throw new InvalidOperationException($"{label} context metadata must be an object with non-empty keys.");
        }

        foreach ((string key, object value) in context.Metadata)
        {
            if (value is string text && text.Length == 0)
            {
                throw new InvalidOperationException($"{label} context metadata key '{key}' must not be an empty string.");
            }

            if (!IsSupportedScalar(value))
            {
                throw new InvalidOperationException(
                    $"{label} context metadata key '{key}' must be a string, boolean, or finite numeric scalar.");
            }
        }
    }

    private static bool IsSupportedScalar(object? value) => value switch
    {
        null => false,
        string or bool or byte or sbyte or short or ushort or int or uint or long or ulong or decimal => true,
        float number => IsFinite(number),
        double number => IsFinite(number),
        _ => false,
    };

    private static bool IsFinite(double number) => !double.IsNaN(number) && !double.IsInfinity(number);

    private sealed record TopologySelectorIdentity(
        string Kind,
        string Value,
        string NamespaceSuffix,
        TopologyContextIdentity? Context)
    {
        public static TopologySelectorIdentity Create(ArchitectureTopologySubjectSelector selector, string kind) => kind switch
        {
            "layer" => new(kind, selector.Layer, string.Empty, null),
            "namespace" => new(kind, selector.Namespace, selector.NamespaceSuffix, null),
            "project" => new(kind, selector.Project, string.Empty, null),
            "assembly" => new(kind, selector.Assembly, string.Empty, null),
            _ => new(kind, string.Empty, string.Empty, TopologyContextIdentity.Create(selector.Context!)),
        };
    }

    private sealed class TopologyContextIdentity : IEquatable<TopologyContextIdentity>
    {
        private readonly TopologyMetadataEntry[] _metadata;

        private TopologyContextIdentity(string role, string? when, TopologyMetadataEntry[] metadata)
        {
            Role = role;
            When = when;
            _metadata = metadata;
        }

        private string Role { get; }

        private string? When { get; }

        public static TopologyContextIdentity Create(ArchitectureContextSelector context) => new(
            context.Role,
            context.When,
            context.Metadata
                .OrderBy(item => item.Key, StringComparer.Ordinal)
                .Select(item => new TopologyMetadataEntry(item.Key, item.Value.GetType(), item.Value))
                .ToArray());

        public bool Equals(TopologyContextIdentity? other) => other is not null
            && string.Equals(Role, other.Role, StringComparison.Ordinal)
            && string.Equals(When, other.When, StringComparison.Ordinal)
            && _metadata.SequenceEqual(other._metadata);

        public override bool Equals(object? obj) => Equals(obj as TopologyContextIdentity);

        public override int GetHashCode()
        {
            var hash = new HashCode();
            hash.Add(Role, StringComparer.Ordinal);
            hash.Add(When, StringComparer.Ordinal);
            foreach (TopologyMetadataEntry entry in _metadata)
            {
                hash.Add(entry);
            }

            return hash.ToHashCode();
        }
    }

    private readonly record struct TopologyMetadataEntry(string Key, Type ValueType, object Value);
}
