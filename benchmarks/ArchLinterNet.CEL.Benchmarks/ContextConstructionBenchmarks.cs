using ArchLinterNet.CEL.Benchmarks.Fixtures;
using ArchLinterNet.CEL.Evaluation;
using ArchLinterNet.CEL.Schema;
using ArchLinterNet.CEL.Values;
using BenchmarkDotNet.Attributes;

namespace ArchLinterNet.CEL.Benchmarks;

/// <summary>
/// Compares the stable-handle <c>Set(CelVariable, CelValue)</c> context-population path against
/// the ergonomic name-based <c>Set(string, CelValue)</c> convenience overload, and measures
/// schema-bound evaluation-context construction (including structural value validation) in
/// isolation from compilation and evaluation.
/// </summary>
/// <remarks>
/// <para>
/// Every <see cref="CelValue"/> passed to <c>Set()</c> below — the "source"/"target" object
/// instances and the two <c>Bool</c> values used by
/// <see cref="BuildContext_NoObjectCatalog_TwoPrimitiveVariables"/> — is built once in
/// <see cref="Setup"/>, not inside the timed benchmark methods. <see cref="CelValue"/> is
/// immutable, so the same instances can be reused across every <c>Set()</c>/<c>Build()</c> call
/// without affecting correctness. This keeps each benchmark measuring only
/// <see cref="Evaluation.CelEvaluationContextBuilder"/>'s own cost (handle/name resolution plus
/// structural validation), not the cost of allocating the values passed into it — on either side
/// of the object-typed/primitive-typed comparison equally.
/// </para>
/// <para>
/// <see cref="BuildContext_NoObjectCatalog_TwoPrimitiveVariables"/> declares exactly two
/// <c>Bool</c> variables and calls <c>Set()</c> exactly twice — matching the source/target
/// schema's variable count and <c>Set()</c> call count — so its comparison against
/// <see cref="BuildContext_StableHandles"/> isolates one variable: object-typed member-by-member
/// structural validation vs. primitive-typed validation, not variable count or <c>Set()</c> call
/// count as well.
/// </para>
/// <para>
/// <b>Construction cost is not free of the object-schema catalog either.</b>
/// <c>CelEvaluationContextBuilder</c>'s constructor computes
/// <c>schema.ComputeEnvironmentIdentity(objectSchemas)</c> on every call: for a schema with a
/// non-empty catalog this rebuilds a <c>StringBuilder</c> and reconcatenates every registered
/// object schema's identity string, uncached, on every single
/// <c>CreateEvaluationContextBuilder()</c> call — it is not the cheap, already-computed
/// <c>CelContextSchema.Identity</c> property lookup the no-catalog path gets. So the
/// <c>BuildContext_StableHandles</c> vs. <c>BuildContext_NoObjectCatalog_TwoPrimitiveVariables</c>
/// gap is <i>construction identity-string cost plus <c>Set()</c>+<c>Build()</c> cost</i> combined,
/// not <c>Set()</c> validation alone — <c>Build()</c> itself does real work too (checking every
/// declared variable was set, then constructing the assignment list and the
/// <see cref="Evaluation.CelEvaluationContext"/>), so subtracting only the isolated construction
/// cost below leaves "<c>Set()</c> + <c>Build()</c>" as the correctly-labeled remainder, not
/// "<c>Set()</c>" alone. <see cref="ConstructBuilderOnly_WithObjectCatalog"/> and
/// <see cref="ConstructBuilderOnly_NoObjectCatalog"/> isolate the construction-only component so
/// the two can be told apart — see <c>RESULTS.md</c> for the resulting split and its exact
/// labeling.
/// </para>
/// </remarks>
[MemoryDiagnoser]
public class ContextConstructionBenchmarks
{
    private CelEnvironment _environment = null!;
    private CelVariable _source = null!;
    private CelVariable _target = null!;
    private CelValue _sourceValue = null!;
    private CelValue _targetValue = null!;
    private CelContextSchema _twoPrimitiveSchema = null!;
    private CelVariable _flagA = null!;
    private CelVariable _flagB = null!;
    private CelValue _flagAValue = null!;
    private CelValue _flagBValue = null!;

    [GlobalSetup]
    public void Setup()
    {
        var schema = BenchmarkFixtures.BuildSourceTargetSchema(out _source, out _target);
        _environment = CelEnvironment.CreateBuilder(Profile.CelProfile.V1)
            .WithContextSchema(schema)
            .WithObjectSchema(BenchmarkFixtures.BuildAssemblyObjectSchema())
            .Build();

        _sourceValue = BenchmarkFixtures.BuildMatchingSourceValue();
        _targetValue = BenchmarkFixtures.BuildMatchingTargetValue();

        // Schema construction happens once here, not inside the benchmarked method below, so
        // BuildContext_NoObjectCatalog_TwoPrimitiveVariables measures only
        // context-builder/Set()/Build() cost, not schema construction cost (already isolated
        // separately by EnvironmentConstructionBenchmarks).
        _twoPrimitiveSchema = BuildTwoPrimitiveVariableSchema(out _flagA, out _flagB);

        // Precomputed for the same reason the object-typed source/target values are precomputed
        // above: CelValue.Bool(...) must not run inside the timed method, or the object-typed and
        // primitive-typed benchmarks would not be measuring the same thing (builder cost only) on
        // both sides.
        _flagAValue = CelValue.Bool(true);
        _flagBValue = CelValue.Bool(false);
    }

    [Benchmark(Description = "Build source/target context via stable variable handles (precomputed values)")]
    public CelEvaluationContext BuildContext_StableHandles() =>
        BenchmarkFixtures.BuildContextFromValues(_environment, _source, _target, _sourceValue, _targetValue);

    [Benchmark(Description = "Build source/target context via name-based Set() convenience overload (precomputed values)")]
    public CelEvaluationContext BuildContext_NameBased() =>
        BenchmarkFixtures.BuildContextFromValuesByName(_environment, _sourceValue, _targetValue);

    [Benchmark(Description = "Build context with 2 primitive Bool variables, no object-schema catalog (same variable/Set() count as the source/target schema)")]
    public CelEvaluationContext BuildContext_NoObjectCatalog_TwoPrimitiveVariables() =>
        _twoPrimitiveSchema.CreateEvaluationContextBuilder()
            .Set(_flagA, _flagAValue)
            .Set(_flagB, _flagBValue)
            .Build();

    // Isolates CelEvaluationContextBuilder's constructor cost alone (no Set()/Build()) so the
    // object-schema-catalog identity-recomputation cost baked into every construction can be told
    // apart from Set()'s own per-member structural validation cost — see the class remarks above.
    [Benchmark(Description = "Construct CelEvaluationContextBuilder only, object-schema catalog present, no Set()/Build()")]
    public CelEvaluationContextBuilder ConstructBuilderOnly_WithObjectCatalog() =>
        _environment.CreateEvaluationContextBuilder();

    [Benchmark(Description = "Construct CelEvaluationContextBuilder only, no object-schema catalog, no Set()/Build()")]
    public CelEvaluationContextBuilder ConstructBuilderOnly_NoObjectCatalog() =>
        _twoPrimitiveSchema.CreateEvaluationContextBuilder();

    private static CelContextSchema BuildTwoPrimitiveVariableSchema(out CelVariable flagA, out CelVariable flagB)
    {
        var builder = CelContextSchema.CreateBuilder("two-primitive-variables");
        flagA = builder.AddVariable("flagA", CelType.Bool);
        flagB = builder.AddVariable("flagB", CelType.Bool);
        return builder.Build();
    }
}
