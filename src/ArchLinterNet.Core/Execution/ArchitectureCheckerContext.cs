using System.Reflection;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Discovery;
using ArchLinterNet.Core.Execution.Expressions;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Profiling;
using ArchLinterNet.Core.Scanning;

namespace ArchLinterNet.Core.Execution;

// The narrow read/record port a contract-family checker gets instead of the whole session (issue
// #452). Every member here is fact/index access a family checker demonstrably needs, or a recording
// port for state the session still owns; lifecycle concerns the session keeps for itself —
// contract selection, execution-context creation, unmatched-ignore collection, coverage,
// policy consistency and cache-facing state — are deliberately absent, so a checker cannot reach
// them and a new contract family cannot grow session-owned checking by accident.
//
// This is a forwarding facade, not a second copy of session state: the session constructs exactly
// one instance of it and every member below delegates straight back, so ordering, caching and
// mutation semantics are identical to the pre-extraction call sites.
internal sealed class ArchitectureCheckerContext
{
    private readonly ArchitectureAnalysisSession _session;

    public ArchitectureCheckerContext(ArchitectureAnalysisSession session)
    {
        _session = session;
        BenchmarkWorkObserver = session.Context.BenchmarkWorkObserver;
    }

    public ArchitectureContractDocument Document => _session.Document;

    internal BenchmarkWorkObserver? BenchmarkWorkObserver { get; }

    public ArchitectureAnalysisContext AnalysisContext => _session.Context;

    public ArchitectureTypeIndex TypeIndex => _session.TypeIndex;

    public ArchitectureRoleIndex RoleIndex => _session.RoleIndex;

    public ArchitectureSourceFileFactIndex SourceFileFactIndex => _session.SourceFileFactIndex;

    public ArchitectureExpressionFactService ExpressionFacts => _session.ExpressionFacts;

    public ArchitectureReferenceGraph ReferenceGraph => _session.ReferenceGraph;

    public IReadOnlyList<string>? PreprocessorSymbols => _session.PreprocessorSymbols;

    // Matches analysis.configuration, defaulting to "Debug" exactly like project discovery's own
    // output-path resolution. Shared by the framework-reference checker and CheckConfiguration's
    // evaluation-failure surfacing, so both read one resolution instead of two.
    public string ResolvedBuildConfiguration => _session.ResolvedBuildConfiguration;

    public ArchitecturePublicApiSurfaceRootResolution ResolvePublicApiSurfaceRoots(string publicApiSurfaceId)
    {
        return _session.ResolvePublicApiSurfaceRoots(publicApiSurfaceId);
    }

    public ArchitecturePublicApiSurfaceMaterialization GetPublicApiSurface(Assembly assembly)
    {
        return _session.GetPublicApiSurface(assembly);
    }

    public ArchitectureContractSurfaceExposureResult GetContractSurfaceExposure(
        IEnumerable<Type> roots,
        ArchitectureContractSurfaceShape surfaceShape)
    {
        return _session.GetContractSurfaceExposure(roots, surfaceShape);
    }

    public Type[] FindTypesInLayer(ArchitectureLayer layer)
    {
        return _session.Facts.FindTypesInLayer(layer);
    }

    public bool MatchesLayer(ArchitectureLayer layer, Type type)
    {
        return _session.Facts.MatchesLayer(layer, type);
    }

    public bool IsInAnyDeclaredLayer(Type type)
    {
        return _session.Facts.IsInAnyDeclaredLayer(type);
    }

    public string? ResolveContainingLayer(Type type, IReadOnlySet<string> candidateLayerNames)
    {
        if (BenchmarkWorkObserver is not null)
            for (int index = 0; index < candidateLayerNames.Count; index++)
                BenchmarkWorkObserver.RecordLayerMembershipProbe();

        return _session.Facts.ResolveContainingLayer(type, candidateLayerNames);
    }

    public IEnumerable<string> ResolveProjectAssemblyNames(List<string> projectNames)
    {
        return _session.Facts.ResolveProjectAssemblyNames(projectNames);
    }

    public bool TryGetAssembly(string assemblyName, out Assembly assembly)
    {
        return _session.Facts.TryGetAssembly(assemblyName, out assembly!);
    }

    public bool TryGetProjectByNormalizedPath(string normalizedProjectPath, out ArchitectureDiscoveredProject project)
    {
        return _session.Facts.TryGetProjectByNormalizedPath(normalizedProjectPath, out project!);
    }

    public bool TryGetPackageReferences(
        string assemblyName,
        out IReadOnlyList<ArchitectureDiscoveredPackageReference> references)
    {
        return _session.Facts.TryGetPackageReferences(assemblyName, out references!);
    }

    public IEnumerable<Type> FindContextSelectorMatchingTypes(ArchitectureContextSelector selector)
    {
        return _session.Facts.FindContextSelectorMatchingTypes(selector);
    }

    public bool IsExcludedFromContextMatch(
        Type candidateType,
        IReadOnlyList<ArchitectureContextSelector> excludeSelectors,
        ArchitectureTypeClassificationResult sourceDescriptor,
        Type? sourceType = null)
    {
        return _session.Facts.IsExcludedFromContextMatch(candidateType, excludeSelectors, sourceDescriptor, sourceType);
    }

    // Evaluation is cached per (project, configuration) on the session for the lifetime of the run,
    // so several framework contracts sharing a source project still trigger one design-time build.
    public ArchitectureDiscoveredFrameworkReference[] ResolveFrameworkReferences(string sourceAssemblyName)
    {
        return _session.ResolveFrameworkReferences(sourceAssemblyName);
    }

    // Recording port: the participation list itself stays session-owned, so contract-family
    // execution order remains the only thing that determines record order.
    public void RecordSubtractiveMatcherParticipation(
        IArchitectureContract contract,
        string field,
        int? index,
        bool matched,
        bool evaluationFailed = false,
        ArchitectureSelectorParticipationKind kind = ArchitectureSelectorParticipationKind.Exclusion)
    {
        _session.RecordSubtractiveMatcherParticipation(contract, field, index, matched, evaluationFailed, kind);
    }
}
