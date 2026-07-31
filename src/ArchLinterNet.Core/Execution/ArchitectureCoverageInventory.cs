using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Families;
using ArchLinterNet.Core.Discovery;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Scanning;

namespace ArchLinterNet.Core.Execution;

public sealed record ArchitectureCoverageNamespaceEntry(string Namespace, string RepresentativeType);

public sealed record ArchitectureCoverageDependencyEdge(string SourceNamespace, string TargetNamespace);

public sealed record ArchitectureCoverageLayerEntry(string Name, ArchitectureLayer Layer);

public sealed class ArchitectureCoverageInventory
{
    private readonly Dictionary<string, Type[]> _typesByNamespace;
    private readonly ArchitectureReferenceGraph _referenceGraph;
    private readonly Lazy<IReadOnlyList<ArchitectureCoverageDependencyEdge>> _dependencyEdges;

    // Bundles the Build factory's fully-computed inputs into one value, so the constructor itself
    // only has to name "the assembled data", not each of its eight ingredients individually.
    private sealed record Args(
        IReadOnlyList<ArchitectureCoverageNamespaceEntry> Namespaces,
        Dictionary<string, Type[]> TypesByNamespace,
        ArchitectureReferenceGraph ReferenceGraph,
        IReadOnlyList<ArchitectureCoverageLayerEntry> DeclaredLayers,
        IReadOnlyList<ArchitectureLayerContract> ExpandedLayerTemplates,
        ArchitectureSourceExpansionInventory SourceExpansion,
        IReadOnlyList<ArchitectureSubtractiveMatcherParticipation> SelectorParticipation,
        ProjectDiscoveryResult? ProjectDiscovery);

    private ArchitectureCoverageInventory(Args args)
    {
        Namespaces = args.Namespaces;
        _typesByNamespace = args.TypesByNamespace;
        _referenceGraph = args.ReferenceGraph;
        DeclaredLayers = args.DeclaredLayers;
        ExpandedLayerTemplates = args.ExpandedLayerTemplates;
        SourceExpansion = args.SourceExpansion;
        SelectorParticipation = args.SelectorParticipation;
        ProjectDiscovery = args.ProjectDiscovery;
        _dependencyEdges = new Lazy<IReadOnlyList<ArchitectureCoverageDependencyEdge>>(BuildDependencyEdges);
    }

    public IReadOnlyList<ArchitectureCoverageNamespaceEntry> Namespaces { get; }

    public IReadOnlyList<ArchitectureCoverageLayerEntry> DeclaredLayers { get; }

    public IReadOnlyList<ArchitectureLayerContract> ExpandedLayerTemplates { get; }

    // The policy's resolved source-set expansion, carried alongside expanded layer templates so a
    // coverage consumer can prove which sources each authored contract resolved to without
    // re-running expansion.
    public ArchitectureSourceExpansionInventory SourceExpansion { get; }

    // The run-time stream is held by reference to the session's append-only collection. A coverage
    // contract may request the lazy inventory before type/layout execution; this lets its consumer
    // observe the completed effective scope without replaying matcher evaluation.
    public IReadOnlyList<ArchitectureSubtractiveMatcherParticipation> SelectorParticipation { get; }

    // Kept for consumers introduced when this stream contained exclusions only. New consumers
    // should prefer SelectorParticipation, which includes typed positive-selector evidence too.
    public IReadOnlyList<ArchitectureSubtractiveMatcherParticipation> SubtractiveMatcherParticipation =>
        SelectorParticipation;

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

        List<ArchitectureCoverageLayerEntry> declaredLayers = document.Layers
            .Select(pair => new ArchitectureCoverageLayerEntry(pair.Key, pair.Value))
            .ToList();

        List<ArchitectureLayerContract> expandedTemplates = LayerTemplateExpander.Expand(
            document.Contracts.StrictLayerTemplates.Concat(document.Contracts.AuditLayerTemplates));

        return new ArchitectureCoverageInventory(new Args(
            namespaces,
            typesByNamespace,
            session.ReferenceGraph,
            declaredLayers,
            expandedTemplates,
            document.SourceExpansion,
            session.SubtractiveMatcherParticipation,
            projectDiscovery));
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
