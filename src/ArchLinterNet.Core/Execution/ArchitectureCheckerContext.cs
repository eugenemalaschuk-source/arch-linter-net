using System.Reflection;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Discovery;
using ArchLinterNet.Core.Execution.Expressions;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Resolution;
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
    }

    public ArchitectureContractDocument Document => _session.Document;

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

    public Type[] FindTypesInLayer(ArchitectureLayer layer) => _session.FindTypesInLayer(layer);

    public bool MatchesLayer(ArchitectureLayer layer, Type type) => _session.MatchesLayer(layer, type);

    public bool IsInAnyDeclaredLayer(Type type) => _session.IsInAnyDeclaredLayer(type);

    public string? ResolveContainingLayer(Type type, IReadOnlySet<string> candidateLayerNames) =>
        _session.ResolveContainingLayer(type, candidateLayerNames);

    public IEnumerable<string> ResolveProjectAssemblyNames(List<string> projectNames) =>
        _session.ResolveProjectAssemblyNames(projectNames);

    public Dictionary<string, Assembly> BuildAssemblyLookup() => _session.BuildAssemblyLookup();

    public IEnumerable<Type> FindContextSelectorMatchingTypes(ArchitectureContextSelector selector) =>
        _session.FindContextSelectorMatchingTypes(selector);

    public bool IsExcludedFromContextMatch(
        Type candidateType,
        IReadOnlyList<ArchitectureContextSelector> excludeSelectors,
        ArchitectureTypeClassificationResult sourceDescriptor,
        Type? sourceType = null) =>
        _session.IsExcludedFromContextMatch(candidateType, excludeSelectors, sourceDescriptor, sourceType);

    // Evaluation is cached per (project, configuration) on the session for the lifetime of the run,
    // so several framework contracts sharing a source project still trigger one design-time build.
    public ArchitectureDiscoveredFrameworkReference[] ResolveFrameworkReferences(string sourceAssemblyName) =>
        _session.ResolveFrameworkReferences(sourceAssemblyName);

    // Recording port: the participation list itself stays session-owned, so contract-family
    // execution order remains the only thing that determines record order.
    public void RecordSubtractiveMatcherParticipation(
        IArchitectureContract contract,
        string field,
        int? index,
        bool matched,
        bool evaluationFailed = false,
        ArchitectureSelectorParticipationKind kind = ArchitectureSelectorParticipationKind.Exclusion) =>
        _session.RecordSubtractiveMatcherParticipation(contract, field, index, matched, evaluationFailed, kind);
}
