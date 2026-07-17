using ArchLinterNet.CEL.Compilation;
using ArchLinterNet.CEL.Evaluation;
using ArchLinterNet.CEL.Profile;
using ArchLinterNet.CEL.Schema;
using ArchLinterNet.CEL.Values;

namespace ArchLinterNet.CEL.Benchmarks.Fixtures;

/// <summary>
/// A generic (non-Core) operations schema covering string, list, map, comparison, and boolean
/// operands, plus a compiled predicate per Profile v1 operator/built-in category. Used by
/// <see cref="EvaluationBenchmarks"/> to attribute evaluation cost to specific operation kinds
/// rather than one aggregate number.
/// </summary>
internal sealed class OperationFixtures
{
    public CelEnvironment Environment { get; }
    public CelEvaluationContext Context { get; }

    public CelCompiledPredicate StringEquality { get; }
    public CelCompiledPredicate StringStartsWith { get; }
    public CelCompiledPredicate StringContains { get; }
    public CelCompiledPredicate ListSizeComparison { get; }
    public CelCompiledPredicate ListMembership { get; }
    public CelCompiledPredicate MapMembership { get; }
    public CelCompiledPredicate MapContainsKey { get; }
    public CelCompiledExpression MapIndexing { get; }
    public CelCompiledPredicate NumericComparison { get; }
    public CelCompiledPredicate BooleanCombination { get; }

    public OperationFixtures()
    {
        var schemaBuilder = CelContextSchema.CreateBuilder("operations-v1");
        var a = schemaBuilder.AddVariable("a", CelType.String);
        var b = schemaBuilder.AddVariable("b", CelType.String);
        var n = schemaBuilder.AddVariable("n", CelType.Int);
        var f = schemaBuilder.AddVariable("f", CelType.Float);
        var flag = schemaBuilder.AddVariable("flag", CelType.Bool);
        var names = schemaBuilder.AddVariable("names", CelType.ListOf(CelType.String));
        var needle = schemaBuilder.AddVariable("needle", CelType.String);
        var lookup = schemaBuilder.AddVariable("lookup", CelType.MapOf(CelType.String));
        var key = schemaBuilder.AddVariable("key", CelType.String);
        var schema = schemaBuilder.Build();

        Environment = CelEnvironment.CreateBuilder(CelProfile.V1)
            .WithContextSchema(schema)
            .Build();

        Context = Environment.CreateEvaluationContextBuilder()
            .Set(a, CelValue.String("Example.Services.Api"))
            .Set(b, CelValue.String("Example.Services.Api"))
            .Set(n, CelValue.Int(42))
            .Set(f, CelValue.Float(3.5))
            .Set(flag, CelValue.Bool(true))
            .Set(names, CelValue.List([CelValue.String("alpha"), CelValue.String("beta"), CelValue.String("gamma")]))
            .Set(needle, CelValue.String("beta"))
            .Set(lookup, CelValue.Map(new Dictionary<string, CelValue> { ["owner"] = CelValue.String("platform-team") }))
            .Set(key, CelValue.String("owner"))
            .Build();

        StringEquality = Compile(Environment, "a == b");
        StringStartsWith = Compile(Environment, "a.startsWith('Example.')");
        StringContains = Compile(Environment, "a.contains('Services')");
        ListSizeComparison = Compile(Environment, "names.size() > 0");
        ListMembership = Compile(Environment, "needle in names");
        MapMembership = Compile(Environment, "key in lookup");
        MapContainsKey = Compile(Environment, "lookup.containsKey(key)");
        MapIndexing = CompileGeneral(Environment, "lookup[key]");
        NumericComparison = Compile(Environment, "n > 0 && f < 10.5");
        BooleanCombination = Compile(Environment, "flag && a == b && names.size() > 0");
    }

    private static CelCompiledPredicate Compile(CelEnvironment environment, string source)
    {
        var result = environment.CompilePredicate(source);
        if (!result.IsSuccess)
            throw new InvalidOperationException($"Benchmark fixture failed to compile '{source}': {result.Diagnostics[0]}");
        return result.Program!;
    }

    private static CelCompiledExpression CompileGeneral(CelEnvironment environment, string source)
    {
        var result = environment.Compile(source);
        if (!result.IsSuccess)
            throw new InvalidOperationException($"Benchmark fixture failed to compile '{source}': {result.Diagnostics[0]}");
        return result.Program!;
    }
}
