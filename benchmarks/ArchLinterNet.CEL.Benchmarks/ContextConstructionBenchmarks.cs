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
/// The "source"/"target" <see cref="CelValue"/> object instances are built once in
/// <see cref="Setup"/>, not inside the timed benchmark methods below — <see cref="CelValue"/> is
/// immutable, so the same instances can be reused across every <c>Set()</c>/<c>Build()</c> call
/// without affecting correctness. This keeps each benchmark measuring only
/// <see cref="Evaluation.CelEvaluationContextBuilder"/>'s own cost (handle/name resolution plus
/// structural validation), not the cost of allocating the dictionaries/lists/object values passed
/// into it.
/// </para>
/// <para>
/// <see cref="BuildContext_NoObjectCatalog_TwoPrimitiveVariables"/> declares exactly two
/// <c>Bool</c> variables and calls <c>Set()</c> exactly twice — matching the source/target
/// schema's variable count and <c>Set()</c> call count — so its comparison against
/// <see cref="BuildContext_StableHandles"/> isolates one variable: object-typed member-by-member
/// structural validation vs. primitive-typed validation, not variable count or <c>Set()</c> call
/// count as well.
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
            .Set(_flagA, CelValue.Bool(true))
            .Set(_flagB, CelValue.Bool(false))
            .Build();

    private static CelContextSchema BuildTwoPrimitiveVariableSchema(out CelVariable flagA, out CelVariable flagB)
    {
        var builder = CelContextSchema.CreateBuilder("two-primitive-variables");
        flagA = builder.AddVariable("flagA", CelType.Bool);
        flagB = builder.AddVariable("flagB", CelType.Bool);
        return builder.Build();
    }
}
