using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Discovery;
using ArchLinterNet.Core.Scanning;

namespace ArchLinterNet.Core.Execution;

internal sealed record ArchitectureCoverageNamespaceEntry(string Namespace, string RepresentativeType);

internal sealed record ArchitectureCoverageDependencyEdge(string SourceNamespace, string TargetNamespace);

internal sealed class ArchitectureCoverageInventory
{
    private readonly Dictionary<string, Type[]> _typesByNamespace;
    private readonly ArchitectureReferenceGraph _referenceGraph;
    private readonly Lazy<IReadOnlyList<ArchitectureCoverageDependencyEdge>> _dependencyEdges;

    private ArchitectureCoverageInventory(
        IReadOnlyList<ArchitectureCoverageNamespaceEntry> namespaces,
        Dictionary<string, Type[]> typesByNamespace,
        ArchitectureReferenceGraph referenceGraph,
        IReadOnlyList<ArchitectureLayerContract> declaredLayers,
        IReadOnlyList<ArchitectureLayerContract> expandedLayerTemplates,
        ProjectDiscoveryResult? projectDiscovery)
    {
        Namespaces = namespaces;
        _typesByNamespace = typesByNamespace;
        _referenceGraph = referenceGraph;
        DeclaredLayers = declaredLayers;
        ExpandedLayerTemplates = expandedLayerTemplates;
        ProjectDiscovery = projectDiscovery;
        _dependencyEdges = new Lazy<IReadOnlyList<ArchitectureCoverageDependencyEdge>>(BuildDependencyEdges);
    }

    public IReadOnlyList<ArchitectureCoverageNamespaceEntry> Namespaces { get; }

    public IReadOnlyList<ArchitectureLayerContract> DeclaredLayers { get; }

    public IReadOnlyList<ArchitectureLayerContract> ExpandedLayerTemplates { get; }

    public ProjectDiscoveryResult? ProjectDiscovery { get; }

    public IReadOnlyList<ArchitectureCoverageDependencyEdge> DependencyEdges => _dependencyEdges.Value;

    internal static ArchitectureCoverageInventory Build(
        ArchitectureContractDocument document,
        ArchitectureAnalysisSession session,
        ProjectDiscoveryResult? projectDiscovery = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(session);

        Dictionary<string, Type[]> typesByNamespace = session.TypeIndex.AllTypes()
            .GroupBy(ArchitectureTypeNames.SafeNamespace, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.OrderBy(type => type.FullName, StringComparer.Ordinal).ToArray(),
                StringComparer.Ordinal);

        List<ArchitectureCoverageNamespaceEntry> namespaces = typesByNamespace
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => new ArchitectureCoverageNamespaceEntry(pair.Key, pair.Value[0].FullName ?? pair.Value[0].Name))
            .ToList();

        List<ArchitectureLayerContract> declaredLayers = document.Layers
            .Select(pair => new ArchitectureLayerContract
            {
                Name = pair.Key,
                Layers = new List<string> { pair.Value.Namespace }
            })
            .ToList();

        List<ArchitectureLayerContract> expandedTemplates = LayerTemplateExpander.Expand(
            document.Contracts.StrictLayerTemplates.Concat(document.Contracts.AuditLayerTemplates));

        return new ArchitectureCoverageInventory(
            namespaces,
            typesByNamespace,
            session.ReferenceGraph,
            declaredLayers,
            expandedTemplates,
            projectDiscovery);
    }

    private List<ArchitectureCoverageDependencyEdge> BuildDependencyEdges()
    {
        HashSet<(string Source, string Target)> edges = new();

        foreach ((string sourceNamespace, Type[] typesInNamespace) in _typesByNamespace)
        {
            foreach (Type sourceType in typesInNamespace)
            {
                foreach (Type referencedType in _referenceGraph.GetReferencedTypes(sourceType))
                {
                    string targetNamespace = ArchitectureTypeNames.SafeNamespace(referencedType);
                    if (string.Equals(sourceNamespace, targetNamespace, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    if (!_typesByNamespace.ContainsKey(targetNamespace))
                    {
                        continue;
                    }

                    edges.Add((sourceNamespace, targetNamespace));
                }
            }
        }

        return edges
            .OrderBy(edge => edge.Source, StringComparer.Ordinal)
            .ThenBy(edge => edge.Target, StringComparer.Ordinal)
            .Select(edge => new ArchitectureCoverageDependencyEdge(edge.Source, edge.Target))
            .ToList();
    }
}
