using System.Reflection;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Discovery;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Resolution;
using ArchLinterNet.Core.Scanning;

namespace ArchLinterNet.Core.Execution;

// The session's fact/index access surface: layer/type resolution, assembly and project lookups, and
// contextual-selector matching, all derived from the indices the session builds once per run.
//
// These were private helpers spread across the family-checking partials until the checker extraction
// in #452 gave family behavior its own home. They are `internal` (not public) and reached by checkers
// only through ArchitectureCheckerContext, so the session keeps owning the indices and the checkers
// keep seeing exactly this much of it.
public sealed partial class ArchitectureAnalysisSession
{
    private ArchitectureCheckerContext? _checkerContext;

    // One instance per session: checkers hold it for the duration of a single contract check only,
    // and it carries no state of its own, so sharing it cannot leak anything between contracts.
    internal ArchitectureCheckerContext CheckerContext => _checkerContext ??= new ArchitectureCheckerContext(this);

    internal Type[] FindTypesInLayer(ArchitectureLayer layer)
    {
        return TypeIndex.FindTypesInLayer(layer, RoleIndex, ExpressionFacts);
    }

    internal bool MatchesLayer(ArchitectureLayer layer, Type type)
    {
        return ArchitectureLayerTypeMatcher.Matches(layer, type, RoleIndex, ExpressionFacts);
    }

    internal bool IsInAnyDeclaredLayer(Type type)
    {
        return Document.Layers.Values.Any(layer => MatchesLayer(layer, type));
    }

    internal string? ResolveContainingLayer(Type type, IReadOnlySet<string> candidateLayerNames)
    {
        return candidateLayerNames
            .Select(layerName => new
            {
                LayerName = layerName,
                Layer = ArchitectureLayerResolver.ResolveLayer(Document, "type-resolution", layerName)
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

    internal Dictionary<string, Assembly> BuildAssemblyLookup()
    {
        return Context.TargetAssemblies
            .GroupBy(assembly => assembly.GetName().Name ?? string.Empty)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
    }

    // "Project" residency is resolved to assembly-name equivalence via project discovery: there is
    // no Type -> .csproj mapping anywhere in this codebase (a project maps 1:1 to a single assembly
    // name). A project name that doesn't match any discovered project contributes no assembly name,
    // which is fail-closed (never widens what's allowed), consistent with how other allow-only
    // contracts treat an unresolvable name.
    internal IEnumerable<string> ResolveProjectAssemblyNames(List<string> projectNames)
    {
        if (projectNames.Count == 0)
        {
            yield break;
        }

        IReadOnlyCollection<ArchitectureDiscoveredProject> discoveredProjects =
            Context.ProjectDiscovery?.DiscoveredProjects ?? Array.Empty<ArchitectureDiscoveredProject>();

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

    internal IEnumerable<Type> FindContextSelectorMatchingTypes(ArchitectureContextSelector selector)
    {
        return RoleIndex.ClassifiedTypes().Where(type =>
            ArchitectureContextSelectorMatcher.Matches(
                selector, type, RoleIndex, sourceDescriptor: null, ExpressionFacts, sourceType: null));
    }

    // sourceType is optional: the contextual dependency/allow-only families always supply it (their
    // exclude selectors are an approved `when` location and need the real source Type to build a
    // ContextualTargetEnvironment). Port-boundary's own exclude selectors reuse this same shape but
    // structurally never carry a compiled `when` (see ArchitectureContextSelector's own doc comment),
    // so its call site omits sourceType — the `when`-evaluation branch is provably unreachable there.
    internal bool IsExcludedFromContextMatch(
        Type candidateType,
        IReadOnlyList<ArchitectureContextSelector> excludeSelectors,
        ArchitectureTypeClassificationResult sourceDescriptor,
        Type? sourceType = null)
    {
        return excludeSelectors.Any(selector =>
            ArchitectureContextSelectorMatcher.Matches(
                selector, candidateType, RoleIndex, sourceDescriptor, ExpressionFacts, sourceType));
    }
}
