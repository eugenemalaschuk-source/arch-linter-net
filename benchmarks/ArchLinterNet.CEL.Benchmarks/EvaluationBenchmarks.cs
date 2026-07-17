using ArchLinterNet.CEL.Benchmarks.Fixtures;
using ArchLinterNet.CEL.Evaluation;
using BenchmarkDotNet.Attributes;

namespace ArchLinterNet.CEL.Benchmarks;

/// <summary>
/// Compile-once/evaluate-many: every benchmark here reuses one <see cref="OperationFixtures"/>
/// instance built in <see cref="GlobalSetup"/>, so only repeated <c>Evaluate</c> cost is measured —
/// no parsing, binding, or type-checking occurs on this path (the internal bound plan is never
/// re-derived; see <c>CelCompiledPredicate.Evaluate</c>/<c>CelCompiledExpression.Evaluate</c>).
/// Each benchmark isolates one Profile v1 operator/built-in category.
/// </summary>
[MemoryDiagnoser]
public class EvaluationBenchmarks
{
    private OperationFixtures _fixtures = null!;
    private CelEvaluationLimits _tighterLimits = null!;

    [GlobalSetup]
    public void Setup()
    {
        _fixtures = new OperationFixtures();
        // Constructed once here, not inside the benchmarked method below, so the measurement is
        // Evaluate's own budget-check overhead and not CelEvaluationLimits's constructor cost.
        _tighterLimits = new CelEvaluationLimits(maxIterations: 100, maxCostUnits: 10_000L);
    }

    [Benchmark(Baseline = true, Description = "Evaluate: string equality (a == b)")]
    public CelEvaluationResult StringEquality() =>
        _fixtures.StringEquality.Evaluate(_fixtures.Context);

    [Benchmark(Description = "Evaluate: string startsWith()")]
    public CelEvaluationResult StringStartsWith() =>
        _fixtures.StringStartsWith.Evaluate(_fixtures.Context);

    [Benchmark(Description = "Evaluate: string contains()")]
    public CelEvaluationResult StringContains() =>
        _fixtures.StringContains.Evaluate(_fixtures.Context);

    [Benchmark(Description = "Evaluate: list.size() comparison")]
    public CelEvaluationResult ListSizeComparison() =>
        _fixtures.ListSizeComparison.Evaluate(_fixtures.Context);

    [Benchmark(Description = "Evaluate: list membership (needle in names)")]
    public CelEvaluationResult ListMembership() =>
        _fixtures.ListMembership.Evaluate(_fixtures.Context);

    [Benchmark(Description = "Evaluate: map membership (key in lookup)")]
    public CelEvaluationResult MapMembership() =>
        _fixtures.MapMembership.Evaluate(_fixtures.Context);

    [Benchmark(Description = "Evaluate: map.containsKey()")]
    public CelEvaluationResult MapContainsKey() =>
        _fixtures.MapContainsKey.Evaluate(_fixtures.Context);

    [Benchmark(Description = "Evaluate: map indexing (lookup[key]), general-expression path")]
    public CelEvaluationResult MapIndexing() =>
        _fixtures.MapIndexing.Evaluate(_fixtures.Context);

    [Benchmark(Description = "Evaluate: numeric comparison (n > 0 && f < 10.5)")]
    public CelEvaluationResult NumericComparison() =>
        _fixtures.NumericComparison.Evaluate(_fixtures.Context);

    [Benchmark(Description = "Evaluate: boolean combination (3-way &&, short-circuiting)")]
    public CelEvaluationResult BooleanCombination() =>
        _fixtures.BooleanCombination.Evaluate(_fixtures.Context);

    // _tighterLimits is deliberately below the environment's SafeDefaults ceiling (100/10,000 vs.
    // 1,000/100,000) — this benchmark isolates the cost of the explicit-limits overload itself
    // (limits comparison in Evaluate(context, limits)) using arbitrary but valid tighter limits,
    // NOT a same-ceiling comparison against the safe-default overload. For that comparison — two
    // Evaluate calls under the *same* ceiling value, safe-default vs. explicit — see
    // ApiScenarioBenchmarks.SafeDefaultOverload/ExplicitLimitsOverload, which use the
    // environment's actual EvaluationLimits on both sides.
    [Benchmark(Description = "Evaluate: explicit-limits overload with tighter-than-default limits (not a same-ceiling comparison)")]
    public CelEvaluationResult Evaluate_WithExplicitTighterLimits() =>
        _fixtures.StringEquality.Evaluate(_fixtures.Context, _tighterLimits);
}
