using ArchLinterNet.CEL.Benchmarks.Fixtures;
using ArchLinterNet.CEL.Compilation;
using ArchLinterNet.CEL.Evaluation;
using ArchLinterNet.CEL.Profile;
using ArchLinterNet.CEL.Schema;
using ArchLinterNet.CEL.Values;
using BenchmarkDotNet.Attributes;

namespace ArchLinterNet.CEL.Benchmarks;

/// <summary>
/// Covers the API-specific benchmark scenarios enumerated in the benchmarking issue that are not
/// already isolated by another class: deterministic budget-exhaustion evaluation, schema-mismatch
/// rejection cost, and the safe-default <c>Evaluate(context)</c> overload versus the explicit-limits
/// overload. (Compile-once/evaluate-many, stable-handle vs. name-based context population, and
/// concurrent reuse are covered by <see cref="EvaluationBenchmarks"/>,
/// <see cref="ContextConstructionBenchmarks"/>, and <see cref="ConcurrencyBenchmarks"/>
/// respectively.)
/// </summary>
[MemoryDiagnoser]
public class ApiScenarioBenchmarks
{
    private CelCompiledPredicate _successPredicate = null!;
    private CelEvaluationContext _successContext = null!;
    private CelEvaluationLimits _fullLimits = null!;

    private CelCompiledPredicate _budgetExhaustionPredicate = null!;
    private CelEvaluationContext _budgetExhaustionContext = null!;
    private CelEvaluationLimits _exhaustedLimits = null!;

    private CelCompiledPredicate _schemaBoundPredicate = null!;
    private CelEvaluationContext _mismatchedSchemaContext = null!;

    [GlobalSetup]
    public void Setup()
    {
        var environment = BenchmarkFixtures.BuildEnvironment();
        var compilation = environment.CompilePredicate(BenchmarkFixtures.RepresentativePredicateSource);
        _successPredicate = compilation.Program!;
        _successContext = BenchmarkFixtures.BuildMatchingContext(environment, environment.Schema.Variables[0], environment.Schema.Variables[1]);
        _fullLimits = environment.EvaluationLimits;

        SetupBudgetExhaustion();

        _schemaBoundPredicate = _successPredicate;
        (_, _mismatchedSchemaContext) = BenchmarkFixtures.BuildMismatchedSchemaContext();
    }

    private void SetupBudgetExhaustion()
    {
        // "in" membership cost scales with haystack length (CelEvaluator charges per-element
        // comparison work). A single-cost-unit ceiling against a non-trivial haystack guarantees
        // BudgetExceeded deterministically on every call — no wall-clock timing involved.
        var schemaBuilder = CelContextSchema.CreateBuilder("budget-exhaustion-v1");
        var needle = schemaBuilder.AddVariable("needle", CelType.String);
        var haystack = schemaBuilder.AddVariable("haystack", CelType.ListOf(CelType.String));
        var schema = schemaBuilder.Build();

        var tightLimits = new CelEvaluationLimits(maxIterations: 1_000_000, maxCostUnits: 1L);
        var environment = CelEnvironment.CreateBuilder(CelProfile.V1)
            .WithContextSchema(schema)
            .WithEvaluationLimits(tightLimits)
            .Build();

        var compilation = environment.CompilePredicate("needle in haystack");
        _budgetExhaustionPredicate = compilation.Program!;
        _exhaustedLimits = tightLimits;

        var elements = new List<CelValue>(256);
        for (var i = 0; i < 256; i++)
            elements.Add(CelValue.String($"item-{i}"));

        _budgetExhaustionContext = environment.CreateEvaluationContextBuilder()
            .Set(needle, CelValue.String("not-present"))
            .Set(haystack, CelValue.List(elements))
            .Build();
    }

    [Benchmark(Description = "Evaluate: deterministic BudgetExceeded (cost ceiling of 1, 256-element scan)")]
    public CelEvaluationResult DeterministicBudgetExhaustion() =>
        _budgetExhaustionPredicate.Evaluate(_budgetExhaustionContext, _exhaustedLimits);

    [Benchmark(Description = "Evaluate: deterministic SchemaMismatch rejection (context from a different schema)")]
    public CelEvaluationResult SchemaMismatchRejection() =>
        _schemaBoundPredicate.Evaluate(_mismatchedSchemaContext);

    [Benchmark(Baseline = true, Description = "Evaluate(context): successful predicate, zero diagnostics, safe-default overload (implicit environment ceiling)")]
    public CelEvaluationResult SafeDefaultOverload() =>
        _successPredicate.Evaluate(_successContext);

    [Benchmark(Description = "Evaluate(context, limits): explicit-limits overload with the same ceiling value")]
    public CelEvaluationResult ExplicitLimitsOverload() =>
        _successPredicate.Evaluate(_successContext, _fullLimits);
}
