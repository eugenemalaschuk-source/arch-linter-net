using ArchLinterNet.CEL.Benchmarks.Fixtures;
using ArchLinterNet.CEL.Compilation;
using ArchLinterNet.CEL.Diagnostics;
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

    // "not present in a 256-element list" needle. Deliberately longer than every haystack item
    // ("item-0".."item-255", 6-8 chars) so CelEvaluator.CompareStrings' Math.Max(left.Length,
    // right.Length) cost is exactly this needle's length on every "in"-scan iteration — see
    // SetupBudgetExhaustion for the resulting per-iteration cost math.
    private const string BudgetHaystackNeedle = "definitely-absent-benchmark-needle-value";
    private const int HaystackSize = 256;

    // Calibrated to let roughly this many of the 256 iterations succeed before BudgetExceeded, not
    // just the first one — proving the deterministic failure genuinely depends on haystack length
    // (a much shorter haystack would finish under budget and succeed with `false` instead).
    private const int TargetIterationsBeforeExceeding = 200;

    private void SetupBudgetExhaustion()
    {
        var schemaBuilder = CelContextSchema.CreateBuilder("budget-exhaustion-v1");
        var needle = schemaBuilder.AddVariable("needle", CelType.String);
        var haystack = schemaBuilder.AddVariable("haystack", CelType.ListOf(CelType.String));
        var schema = schemaBuilder.Build();

        // Self-calibrating, not a hardcoded copy of CelEvaluator's private cost constants (which
        // would silently drift if the cost model ever changes): a MaxCostUnits=1 probe against a
        // single haystack element is guaranteed to fail on that element's first comparison, and the
        // BudgetExceeded diagnostic's "observedValue" IS the real per-iteration cost — fixed
        // comparison charge plus the string-length charge — as CelEvaluator actually computes it
        // today, whatever that turns out to be.
        var probeLimits = new CelEvaluationLimits(maxIterations: 1_000_000, maxCostUnits: 1L);
        var probeEnvironment = CelEnvironment.CreateBuilder(CelProfile.V1)
            .WithContextSchema(schema)
            .WithEvaluationLimits(probeLimits)
            .Build();
        var probeCompilation = probeEnvironment.CompilePredicate("needle in haystack");
        var probeContext = probeEnvironment.CreateEvaluationContextBuilder()
            .Set(needle, CelValue.String(BudgetHaystackNeedle))
            .Set(haystack, CelValue.List([CelValue.String("item-0")]))
            .Build();
        var probeResult = probeCompilation.Program!.Evaluate(probeContext, probeLimits);
        if (probeResult.IsSuccess
            || probeResult.Diagnostics.Count == 0
            || probeResult.Diagnostics[0].Code != CelDiagnosticCode.BudgetExceeded
            || !probeResult.Diagnostics[0].Parameters.TryGetValue("observedValue", out var observedValueText)
            || !long.TryParse(observedValueText, out var costPerIteration))
        {
            throw new InvalidOperationException(
                "Budget-exhaustion calibration probe did not produce the expected BudgetExceeded " +
                "diagnostic with a numeric 'observedValue' parameter. CelEvaluator's cost-accounting " +
                "diagnostics may have changed shape — update this probe to match.");
        }

        var maxCostUnits = costPerIteration * TargetIterationsBeforeExceeding;
        var tightLimits = new CelEvaluationLimits(maxIterations: 1_000_000, maxCostUnits: maxCostUnits);
        var environment = CelEnvironment.CreateBuilder(CelProfile.V1)
            .WithContextSchema(schema)
            .WithEvaluationLimits(tightLimits)
            .Build();

        var compilation = environment.CompilePredicate("needle in haystack");
        _budgetExhaustionPredicate = compilation.Program!;
        _exhaustedLimits = tightLimits;

        var elements = new List<CelValue>(HaystackSize);
        for (var i = 0; i < HaystackSize; i++)
            elements.Add(CelValue.String($"item-{i}"));

        _budgetExhaustionContext = environment.CreateEvaluationContextBuilder()
            .Set(needle, CelValue.String(BudgetHaystackNeedle))
            .Set(haystack, CelValue.List(elements))
            .Build();

        // Self-check, run once at setup time rather than assumed: confirm the calibrated scenario
        // actually produces BudgetExceeded today. If a future cost-model change makes this
        // silently succeed instead (e.g. because "in" no longer charges per element), this throws
        // immediately instead of letting DeterministicBudgetExhaustion quietly start measuring a
        // successful `false` result under its old, now-misleading name.
        var actualResult = _budgetExhaustionPredicate.Evaluate(_budgetExhaustionContext, _exhaustedLimits);
        if (actualResult.IsSuccess || actualResult.Diagnostics.Count == 0
            || actualResult.Diagnostics[0].Code != CelDiagnosticCode.BudgetExceeded)
        {
            throw new InvalidOperationException(
                "DeterministicBudgetExhaustion's calibrated 256-element scenario no longer produces " +
                "BudgetExceeded — CelEvaluator's cost model for 'in' has likely changed. This " +
                "benchmark would otherwise silently start measuring a successful evaluation under a " +
                "name that claims budget exhaustion.");
        }

        // Second self-check: confirm haystack length is causally relevant, not just an artifact of
        // the first comparison — the same budget against a much shorter haystack must succeed.
        var shortHaystackContext = environment.CreateEvaluationContextBuilder()
            .Set(needle, CelValue.String(BudgetHaystackNeedle))
            .Set(haystack, CelValue.List(elements.Take(HaystackSize / 4).ToList()))
            .Build();
        var shortHaystackResult = _budgetExhaustionPredicate.Evaluate(shortHaystackContext, _exhaustedLimits);
        if (!shortHaystackResult.IsSuccess)
        {
            throw new InvalidOperationException(
                $"A {HaystackSize / 4}-element haystack under the same calibrated budget also " +
                "produced BudgetExceeded — the scenario no longer demonstrates that haystack length " +
                "drives the failure (it would fail regardless of haystack length).");
        }
    }

    [Benchmark(Description = "Evaluate: deterministic BudgetExceeded (~200 of a 256-element scan, cost-calibrated so haystack length actually drives the failure)")]
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
