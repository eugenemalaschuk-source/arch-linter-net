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

    public ArchitectureAnalysisFactService(
        ArchitectureAnalysisContext context,
        ArchitectureContractDocument document,
        ArchitectureTypeIndex typeIndex,
        ArchitectureRoleIndex roleIndex,
        ArchitectureExpressionFactService expressionFacts)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _document = document ?? throw new ArgumentNullException(nameof(document));
        _typeIndex = typeIndex ?? throw new ArgumentNullException(nameof(typeIndex));
        _roleIndex = roleIndex ?? throw new ArgumentNullException(nameof(roleIndex));
        _expressionFacts = expressionFacts ?? throw new ArgumentNullException(nameof(expressionFacts));
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

    public Dictionary<string, Assembly> BuildAssemblyLookup()
    {
        return _context.TargetAssemblies
            .GroupBy(assembly => assembly.GetName().Name ?? string.Empty)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
    }

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
