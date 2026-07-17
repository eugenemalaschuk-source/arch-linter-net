using ArchLinterNet.CEL.Benchmarks.Fixtures;
using ArchLinterNet.CEL.Evaluation;
using ArchLinterNet.CEL.Schema;
using BenchmarkDotNet.Attributes;

namespace ArchLinterNet.CEL.Benchmarks;

/// <summary>
/// Compares the stable-handle <c>Set(CelVariable, CelValue)</c> context-population path against
/// the ergonomic name-based <c>Set(string, CelValue)</c> convenience overload, and measures
/// schema-bound evaluation-context construction (including structural value validation) in
/// isolation from compilation and evaluation.
/// </summary>
[MemoryDiagnoser]
public class ContextConstructionBenchmarks
{
    private CelEnvironment _environment = null!;
    private CelVariable _source = null!;
    private CelVariable _target = null!;

    [GlobalSetup]
    public void Setup()
    {
        var schema = BenchmarkFixtures.BuildSourceTargetSchema(out _source, out _target);
        _environment = CelEnvironment.CreateBuilder(Profile.CelProfile.V1)
            .WithContextSchema(schema)
            .WithObjectSchema(BenchmarkFixtures.BuildAssemblyObjectSchema())
            .Build();
    }

    [Benchmark(Description = "Build source/target context via stable variable handles")]
    public CelEvaluationContext BuildContext_StableHandles() =>
        BenchmarkFixtures.BuildMatchingContext(_environment, _source, _target);

    [Benchmark(Description = "Build source/target context via name-based Set() convenience overload")]
    public CelEvaluationContext BuildContext_NameBased() =>
        BenchmarkFixtures.BuildMatchingContextByName(_environment);

    [Benchmark(Description = "Build context without object-schema catalog (schema.CreateEvaluationContextBuilder())")]
    public CelEvaluationContext BuildContext_NoObjectCatalog_PrimitivesOnly()
    {
        var schema = BuildPrimitiveOnlySchema(out var flag);
        return schema.CreateEvaluationContextBuilder()
            .Set(flag, Values.CelValue.Bool(true))
            .Build();
    }

    private static CelContextSchema BuildPrimitiveOnlySchema(out CelVariable flag)
    {
        var builder = CelContextSchema.CreateBuilder("primitive-only");
        flag = builder.AddVariable("flag", CelType.Bool);
        return builder.Build();
    }
}
