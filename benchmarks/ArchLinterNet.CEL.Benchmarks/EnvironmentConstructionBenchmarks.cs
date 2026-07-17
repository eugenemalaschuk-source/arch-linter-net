using ArchLinterNet.CEL.Benchmarks.Fixtures;
using ArchLinterNet.CEL.Profile;
using ArchLinterNet.CEL.Schema;
using BenchmarkDotNet.Attributes;

namespace ArchLinterNet.CEL.Benchmarks;

/// <summary>
/// Measures one-time construction/freeze costs: context schema, object schema, and the immutable
/// <see cref="CelEnvironment"/> that wraps them. These costs are expected to be paid once per
/// process/host lifetime under the compile-once/evaluate-many architecture, so they are reported
/// separately from parsing, binding, and evaluation.
/// </summary>
[MemoryDiagnoser]
public class EnvironmentConstructionBenchmarks
{
    [Benchmark(Description = "Build source/target context schema (2 object-typed variables)")]
    public CelContextSchema BuildContextSchema() =>
        BenchmarkFixtures.BuildSourceTargetSchema(out _, out _);

    [Benchmark(Description = "Build the 'assembly' object schema (5 members)")]
    public CelObjectSchema BuildObjectSchema() =>
        BenchmarkFixtures.BuildAssemblyObjectSchema();

    [Benchmark(Description = "Build full CelEnvironment (schema + object schema + Build())")]
    public CelEnvironment BuildFullEnvironment() =>
        BenchmarkFixtures.BuildEnvironment();

    [Benchmark(Description = "CelEnvironmentBuilder with SafeDefaults limits only, no object schemas")]
    public CelEnvironment BuildMinimalEnvironment()
    {
        var schema = CelContextSchema.CreateBuilder("minimal").Build();
        return CelEnvironment.CreateBuilder(CelProfile.V1)
            .WithContextSchema(schema)
            .Build();
    }
}
