using System.Reflection;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Discovery;
using ArchLinterNet.Core.Execution.Expressions;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Resolution;
using ArchLinterNet.Core.Scanning;

namespace ArchLinterNet.Core.Execution;

// Provides the immutable, index-backed facts that contract checkers need. It deliberately owns no
// lifecycle state, so a checker cannot reach selection, baselines, diagnostics, or policy flow.
internal sealed class ArchitectureAnalysisFactService
{
    private readonly ArchitectureAnalysisContext _context;
    private readonly ArchitectureContractDocument _document;
    private readonly ArchitectureTypeIndex _typeIndex;
    private readonly ArchitectureRoleIndex _roleIndex;
    private readonly ArchitectureExpressionFactService _expressionFacts;
    private readonly ArchitectureSessionMetadataIndexes _metadataIndexes;

    public ArchitectureAnalysisFactService(
        ArchitectureAnalysisContext context,
        ArchitectureContractDocument document,
        ArchitectureTypeIndex typeIndex,
        ArchitectureRoleIndex roleIndex,
        ArchitectureExpressionFactService expressionFacts,
        ArchitectureSessionMetadataIndexes metadataIndexes)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _document = document ?? throw new ArgumentNullException(nameof(document));
        _typeIndex = typeIndex ?? throw new ArgumentNullException(nameof(typeIndex));
        _roleIndex = roleIndex ?? throw new ArgumentNullException(nameof(roleIndex));
        _expressionFacts = expressionFacts ?? throw new ArgumentNullException(nameof(expressionFacts));
        _metadataIndexes = metadataIndexes ?? throw new ArgumentNullException(nameof(metadataIndexes));
    }

    public Type[] FindTypesInLayer(ArchitectureLayer layer)
    {
        return _typeIndex.FindTypesInLayer(layer, _roleIndex, _expressionFacts);
    }

    public bool MatchesLayer(ArchitectureLayer layer, Type type)
    {
        return ArchitectureLayerTypeMatcher.Matches(layer, type, _roleIndex, _expressionFacts);
    }

    public bool IsInAnyDeclaredLayer(Type type)
    {
        return _document.Layers.Values.Any(layer => MatchesLayer(layer, type));
    }

    public string? ResolveContainingLayer(Type type, IReadOnlySet<string> candidateLayerNames)
    {
        return candidateLayerNames
            .Select(layerName => new
            {
                LayerName = layerName,
                Layer = ArchitectureLayerResolver.ResolveLayer(_document, "type-resolution", layerName)
            })
            .Where(entry => MatchesLayer(entry.Layer, type))
            .Select(entry =>
            {
                bool hasNamespace = !string.IsNullOrWhiteSpace(entry.Layer.Namespace);
                NamespaceGlobPattern? pattern = hasNamespace ? entry.Layer.GlobPattern : null;
                return new
                {
                    entry.LayerName,
                    HasSelector = entry.Layer.Selector != null,
                    HasNamespace = hasNamespace,
                    IsGlob = pattern?.IsGlob ?? false,
                    LiteralCount = pattern?.LiteralCount ?? -1,
                    HasSuffix = !string.IsNullOrEmpty(entry.Layer.NamespaceSuffix),
                    WildcardCount = pattern?.WildcardCount ?? int.MaxValue
                };
            })
            .OrderByDescending(entry => entry.HasSelector)
            .ThenByDescending(entry => entry.HasNamespace)
            .ThenByDescending(entry => entry.HasNamespace && !entry.IsGlob)
            .ThenByDescending(entry => entry.LiteralCount)
            .ThenByDescending(entry => entry.HasSuffix)
            .ThenBy(entry => entry.WildcardCount)
            .ThenBy(entry => entry.LayerName, StringComparer.Ordinal)
            .Select(entry => entry.LayerName)
            .FirstOrDefault();
    }

    public IReadOnlyDictionary<string, Assembly> BuildAssemblyLookup() => _metadataIndexes.AssembliesByName;

    public bool TryGetAssembly(string assemblyName, out Assembly assembly) =>
        _metadataIndexes.TryGetAssembly(assemblyName, out assembly!);

    public bool TryGetProjectByAssemblyName(string assemblyName, out ArchitectureDiscoveredProject project) =>
        _metadataIndexes.TryGetProjectByAssemblyName(assemblyName, out project!);

    public bool TryGetProjectByNormalizedPath(string normalizedProjectPath, out ArchitectureDiscoveredProject project) =>
        _metadataIndexes.TryGetProjectByNormalizedPath(normalizedProjectPath, out project!);

    public bool TryGetProjectByResolvedAssembly(Assembly assembly, out ArchitectureDiscoveredProject project) =>
        _metadataIndexes.TryGetProjectByResolvedAssembly(assembly, out project!);

    public bool HasAmbiguousProjectOutputAssemblyName(string assemblyName) =>
        _metadataIndexes.HasAmbiguousProjectOutputAssemblyName(assemblyName);

    public bool TryGetPackageReferences(
        string assemblyName,
        out IReadOnlyList<ArchitectureDiscoveredPackageReference> references) =>
        _metadataIndexes.TryGetPackageReferences(assemblyName, out references!);

    public IEnumerable<string> ResolveProjectAssemblyNames(List<string> projectNames)
    {
        if (projectNames.Count == 0)
        {
            yield break;
        }

        IReadOnlyCollection<ArchitectureDiscoveredProject> discoveredProjects =
            _context.ProjectDiscovery?.DiscoveredProjects ?? Array.Empty<ArchitectureDiscoveredProject>();
        HashSet<string> requestedProjectNames = new(projectNames, StringComparer.Ordinal);

        foreach (ArchitectureDiscoveredProject project in discoveredProjects)
        {
            string projectFileName = Path.GetFileNameWithoutExtension(project.Path);
            if (requestedProjectNames.Contains(projectFileName))
            {
                yield return project.AssemblyName;
            }
        }
    }

    public IEnumerable<Type> FindContextSelectorMatchingTypes(ArchitectureContextSelector selector)
    {
        return _roleIndex.ClassifiedTypes().Where(type =>
            ArchitectureContextSelectorMatcher.Matches(
                selector, type, _roleIndex, sourceDescriptor: null, _expressionFacts, sourceType: null));
    }

    public bool IsExcludedFromContextMatch(
        Type candidateType,
        IReadOnlyList<ArchitectureContextSelector> excludeSelectors,
        ArchitectureTypeClassificationResult sourceDescriptor,
        Type? sourceType = null)
    {
        return excludeSelectors.Any(selector =>
            ArchitectureContextSelectorMatcher.Matches(
                selector, candidateType, _roleIndex, sourceDescriptor, _expressionFacts, sourceType));
    }
}
