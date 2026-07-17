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
/// The "source"/"target" <see cref="CelValue"/> object instances are built once in
/// <see cref="Setup"/>, not inside the timed benchmark methods below — <see cref="CelValue"/> is
/// immutable, so the same instances can be reused across every <c>Set()</c>/<c>Build()</c> call
/// without affecting correctness. This keeps each benchmark measuring only
/// <see cref="Evaluation.CelEvaluationContextBuilder"/>'s own cost (handle/name resolution plus
/// structural validation), not the cost of allocating the dictionaries/lists/object values passed
/// into it.
/// </remarks>
[MemoryDiagnoser]
public class ContextConstructionBenchmarks
{
    private CelEnvironment _environment = null!;
    private CelVariable _source = null!;
    private CelVariable _target = null!;
    private CelValue _sourceValue = null!;
    private CelValue _targetValue = null!;
    private CelContextSchema _primitiveOnlySchema = null!;
    private CelVariable _flag = null!;

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
        // BuildContext_NoObjectCatalog_PrimitivesOnly measures only context-builder/Set()/Build()
        // cost, not schema construction cost (already isolated separately by
        // EnvironmentConstructionBenchmarks).
        _primitiveOnlySchema = BuildPrimitiveOnlySchema(out _flag);
    }

    [Benchmark(Description = "Build source/target context via stable variable handles (precomputed values)")]
    public CelEvaluationContext BuildContext_StableHandles() =>
        BenchmarkFixtures.BuildContextFromValues(_environment, _source, _target, _sourceValue, _targetValue);

    [Benchmark(Description = "Build source/target context via name-based Set() convenience overload (precomputed values)")]
    public CelEvaluationContext BuildContext_NameBased() =>
        BenchmarkFixtures.BuildContextFromValuesByName(_environment, _sourceValue, _targetValue);

    [Benchmark(Description = "Build context without object-schema catalog (schema.CreateEvaluationContextBuilder())")]
    public CelEvaluationContext BuildContext_NoObjectCatalog_PrimitivesOnly() =>
        _primitiveOnlySchema.CreateEvaluationContextBuilder()
            .Set(_flag, CelValue.Bool(true))
            .Build();

    private static CelContextSchema BuildPrimitiveOnlySchema(out CelVariable flag)
    {
        var builder = CelContextSchema.CreateBuilder("primitive-only");
        flag = builder.AddVariable("flag", CelType.Bool);
        return builder.Build();
    }
}
