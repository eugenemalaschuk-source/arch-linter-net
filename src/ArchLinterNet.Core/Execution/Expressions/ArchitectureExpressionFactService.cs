using ArchLinterNet.CEL.Compilation;
using ArchLinterNet.CEL.Evaluation;
using ArchLinterNet.Core.Discovery;
using ArchLinterNet.Core.Scanning;

namespace ArchLinterNet.Core.Execution.Expressions;

// Session-scoped builder for the typed CEL fact contexts declared by ArchitectureExpressionSchemas,
// and the single evaluation choke point selector/contextual matchers call once a compiled `when`
// needs to run. Per-type subject facts are memoized for the lifetime of one analysis session, since
// the same candidate type is evaluated against many selectors. See openspec/specs/cel-policy-model/
// spec.md for the closed fact catalog and openspec/changes/cel-selector-contextual-integration/
// design.md Decision D1 (memoization) and D6 (dependency-fact scope for this wave).
internal sealed class ArchitectureExpressionFactService
{
    private readonly ArchitectureRoleIndex _roleIndex;
    private readonly ArchitectureSourceFileFactIndex _sourceFileFactIndex;
    private readonly ProjectDiscoveryResult? _projectDiscovery;
    private readonly Dictionary<Type, ArchitectureExpressionSubjectFacts> _subjectFactsCache = new();

    public ArchitectureExpressionFactService(
        ArchitectureRoleIndex roleIndex,
        ArchitectureSourceFileFactIndex sourceFileFactIndex,
        ProjectDiscoveryResult? projectDiscovery)
    {
        _roleIndex = roleIndex ?? throw new ArgumentNullException(nameof(roleIndex));
        _sourceFileFactIndex = sourceFileFactIndex ?? throw new ArgumentNullException(nameof(sourceFileFactIndex));
        _projectDiscovery = projectDiscovery;
    }

    public ArchitectureExpressionSubjectFacts BuildSubjectFacts(Type type)
    {
        if (_subjectFactsCache.TryGetValue(type, out ArchitectureExpressionSubjectFacts? cached))
        {
            return cached;
        }

        ArchitectureExpressionSubjectFacts facts = ArchitectureExpressionSubjectFactBuilder.Build(
            type, _roleIndex, _sourceFileFactIndex, _projectDiscovery);
        _subjectFactsCache[type] = facts;
        return facts;
    }

    // The reference scanner this wave's contextual matching runs on (ArchitectureReferenceScanner)
    // reports type-level reference existence only — it does not track which specific member produced
    // an edge or whether the edge is only reachable via a method body (that requires the separate,
    // pattern-scoped IL scan ArchitectureIlMethodBodyScanner performs for forbidden-call contracts).
    // Building a real per-edge dependency fact would require re-architecting the reference scanner
    // into structured edges, which is out of scope for this change (see design.md Decision D6).
    // Facts here are therefore deterministic but intentionally minimal: `kind` names the detection
    // mechanism this wave actually has, `viaMethodBody` is always false (that signal isn't produced
    // by this scan path), and member names are empty. `source`/`target` subject-to-subject
    // comparisons — the issue's actual cross-context use case — do not depend on this method at all.
    public static ArchitectureExpressionDependencyFacts BuildDependencyFacts()
    {
        return new ArchitectureExpressionDependencyFacts(
            Kind: "declared-member-reference",
            ViaMethodBody: false,
            SourceMemberName: string.Empty,
            TargetMemberName: string.Empty);
    }

    // Evaluates a compiled `when` predicate and fails closed: a well-typed true/false result is
    // returned, but an evaluation error (e.g. a missing map key) throws instead of being treated as
    // a non-match, per the "Predicate semantics are fail-closed" requirement in
    // openspec/specs/cel-policy-model/spec.md. description identifies the owning selector/contract
    // and the expression source text for the resulting error message, mirroring how
    // ExpressionCompilationValidator already reports compile-time failures.
    public static bool Evaluate(CelCompiledPredicate predicate, CelEvaluationContext context, string description)
    {
        ArchitectureExpressionEvaluationResult result = ArchitectureExpressionEvaluator.Evaluate(predicate, context);
        if (result.IsError)
        {
            throw new InvalidOperationException(
                $"{description} 'when' expression failed to evaluate: {result.ErrorMessage}");
        }

        return result.IsMatch;
    }
}
