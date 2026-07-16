using System.Linq;
using System.Threading.Tasks;
using ArchLinterNet.CEL;
using ArchLinterNet.CEL.Compilation;
using ArchLinterNet.CEL.Diagnostics;
using ArchLinterNet.CEL.Evaluation;
using ArchLinterNet.CEL.Profile;
using ArchLinterNet.CEL.Schema;
using ArchLinterNet.CEL.Values;
using NUnit.Framework;

namespace ArchLinterNet.CEL.Tests;

[TestFixture]
public sealed class CelEvaluatorTests
{
    [Test]
    public void Predicate_Evaluate_ReturnsExpectedBooleanResult()
    {
        var env = BuildEnvironment();
        var compilation = env.CompilePredicate("x && s.startsWith(prefix)");

        Assert.That(compilation.IsSuccess, Is.True);

        var result = compilation.Program!.Evaluate(BuildContext(
            env,
            x: true,
            s: "alphabet",
            prefix: "alpha"));

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.AsBool(), Is.True);
    }

    [Test]
    public void Expression_Evaluate_ReturnsTypedValue()
    {
        var env = BuildEnvironment();
        var compilation = env.Compile("obj.count");

        Assert.That(compilation.IsSuccess, Is.True);

        var result = compilation.Program!.Evaluate(BuildContext(env, objCount: 42));

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Value, Is.Not.Null);
        Assert.That(result.Value!.AsInt(), Is.EqualTo(42));
    }

    [Test]
    public void LogicalOperators_DeterminingOperandAbsorbsMissingKeyFailure()
    {
        var env = BuildEnvironment();
        var orProgram = env.CompilePredicate("x || map[key]");
        var andProgram = env.CompilePredicate("x && map[key]");

        Assert.Multiple(() =>
        {
            Assert.That(orProgram.IsSuccess, Is.True);
            Assert.That(andProgram.IsSuccess, Is.True);
        });

        var orResult = orProgram.Program!.Evaluate(BuildContext(
            env,
            x: true,
            key: "missing",
            map: new Dictionary<string, CelValue>()));
        var andResult = andProgram.Program!.Evaluate(BuildContext(
            env,
            x: false,
            key: "missing",
            map: new Dictionary<string, CelValue>()));

        Assert.Multiple(() =>
        {
            Assert.That(orResult.IsSuccess, Is.True);
            Assert.That(orResult.AsBool(), Is.True);
            Assert.That(andResult.IsSuccess, Is.True);
            Assert.That(andResult.AsBool(), Is.False);
        });
    }

    [Test]
    public void LogicalOperators_NonDeterminingOperandPropagatesMissingKeyFailure()
    {
        var env = BuildEnvironment();
        var compilation = env.CompilePredicate("x && map[key]");

        Assert.That(compilation.IsSuccess, Is.True);

        var result = compilation.Program!.Evaluate(BuildContext(
            env,
            x: true,
            key: "missing",
            map: new Dictionary<string, CelValue>()));

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.Diagnostics[0].Code, Is.EqualTo(CelDiagnosticCode.EvaluationFailure));
        Assert.That(result.Diagnostics[0].Parameters["failureKind"], Is.EqualTo("missingKey"));
        Assert.That(result.Diagnostics[0].Span, Is.Not.Null);
    }

    [Test]
    public void ListIndex_OutOfRange_ReturnsStructuredFailure()
    {
        var env = BuildEnvironment();
        var compilation = env.Compile("list[index]");

        Assert.That(compilation.IsSuccess, Is.True);

        var result = compilation.Program!.Evaluate(BuildContext(
            env,
            index: 8,
            list: [CelValue.Int(1), CelValue.Int(2)]));

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.Diagnostics[0].Code, Is.EqualTo(CelDiagnosticCode.EvaluationFailure));
        Assert.That(result.Diagnostics[0].Parameters["failureKind"], Is.EqualTo("invalidIndex"));
        Assert.That(result.Diagnostics[0].Parameters["index"], Is.EqualTo("8"));
    }

    [Test]
    public void Evaluate_WithDifferentSchemaContext_ReturnsSchemaMismatch()
    {
        var env = BuildEnvironment();
        var compilation = env.CompilePredicate("x");

        Assert.That(compilation.IsSuccess, Is.True);

        var otherSchemaBuilder = CelContextSchema.CreateBuilder("other-schema");
        otherSchemaBuilder.AddVariable("x", CelType.Bool);
        var otherContext = otherSchemaBuilder.Build()
            .CreateEvaluationContextBuilder()
            .Set("x", CelValue.Bool(true))
            .Build();

        var result = compilation.Program!.Evaluate(otherContext);

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.Diagnostics[0].Code, Is.EqualTo(CelDiagnosticCode.SchemaMismatch));
        Assert.That(result.Diagnostics[0].Parameters, Contains.Key("schemaId"));
        Assert.That(result.Diagnostics[0].Parameters, Contains.Key("expectedSchemaId"));
    }

    [Test]
    public void Evaluate_MaxIterationsExceeded_ReturnsBudgetExceeded()
    {
        var env = BuildEnvironment();
        var compilation = env.CompilePredicate("x && y");

        Assert.That(compilation.IsSuccess, Is.True);

        var result = compilation.Program!.Evaluate(
            BuildContext(env, x: true, y: true),
            new CelEvaluationLimits(maxIterations: 1, maxCostUnits: env.EvaluationLimits.MaxCostUnits));

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.Diagnostics[0].Code, Is.EqualTo(CelDiagnosticCode.BudgetExceeded));
        Assert.That(result.Diagnostics[0].Parameters["limitName"], Is.EqualTo("MaxIterations"));
    }

    [Test]
    public void Evaluate_MaxCostUnitsExceeded_ReturnsBudgetExceeded()
    {
        var env = BuildEnvironment();
        var compilation = env.CompilePredicate("s.contains(prefix)");

        Assert.That(compilation.IsSuccess, Is.True);

        var result = compilation.Program!.Evaluate(
            BuildContext(env, s: new string('a', 12), prefix: new string('a', 12)),
            new CelEvaluationLimits(maxIterations: env.EvaluationLimits.MaxIterations, maxCostUnits: 100));

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.Diagnostics[0].Code, Is.EqualTo(CelDiagnosticCode.BudgetExceeded));
        Assert.That(result.Diagnostics[0].Parameters["limitName"], Is.EqualTo("MaxCostUnits"));
    }

    [Test]
    public async Task CompiledProgram_CanBeReusedConcurrentlyAcrossIndependentContexts()
    {
        var env = BuildEnvironment();
        var compilation = env.CompilePredicate("x && s.startsWith(prefix)");

        Assert.That(compilation.IsSuccess, Is.True);

        var contexts = new[]
        {
            BuildContext(env, x: true, s: "alpha", prefix: "a"),
            BuildContext(env, x: true, s: "beta", prefix: "a"),
            BuildContext(env, x: false, s: "alpha", prefix: "a"),
            BuildContext(env, x: true, s: "gamma", prefix: "g"),
        };

        var results = await Task.WhenAll(contexts.Select(context => Task.Run(() => compilation.Program!.Evaluate(context))));

        Assert.That(results.Select(result => result.AsBool()).ToArray(), Is.EqualTo(new[] { true, false, false, true }));
    }

    private static CelEnvironment BuildEnvironment(CelEvaluationLimits? evaluationLimits = null)
    {
        var schemaBuilder = CelContextSchema.CreateBuilder("eval-schema");
        schemaBuilder.AddVariable("x", CelType.Bool);
        schemaBuilder.AddVariable("y", CelType.Bool);
        schemaBuilder.AddVariable("s", CelType.String);
        schemaBuilder.AddVariable("prefix", CelType.String);
        schemaBuilder.AddVariable("needle", CelType.Int);
        schemaBuilder.AddVariable("key", CelType.String);
        schemaBuilder.AddVariable("index", CelType.Int);
        schemaBuilder.AddVariable("list", CelType.ListOf(CelType.Int));
        schemaBuilder.AddVariable("map", CelType.MapOf(CelType.Bool));
        schemaBuilder.AddVariable("obj", CelType.ObjectOf("widget"));

        var objectSchemaBuilder = CelObjectSchema.CreateBuilder("widget");
        objectSchemaBuilder.AddMember("name", CelType.String);
        objectSchemaBuilder.AddMember("count", CelType.Int);
        var objectSchema = objectSchemaBuilder.Build();

        return CelEnvironment.CreateBuilder(CelProfile.V1)
            .WithContextSchema(schemaBuilder.Build())
            .WithObjectSchema(objectSchema)
            .WithEvaluationLimits(evaluationLimits ?? CelEvaluationLimits.SafeDefaults)
            .Build();
    }

    private static CelEvaluationContext BuildContext(
        CelEnvironment env,
        bool x = true,
        bool y = true,
        string s = "alpha",
        string prefix = "a",
        long needle = 1,
        string key = "name",
        long index = 0,
        IReadOnlyList<CelValue>? list = null,
        IReadOnlyDictionary<string, CelValue>? map = null,
        string objName = "name",
        long objCount = 1)
    {
        var builder = env.CreateEvaluationContextBuilder();
        builder.Set("x", CelValue.Bool(x));
        builder.Set("y", CelValue.Bool(y));
        builder.Set("s", CelValue.String(s));
        builder.Set("prefix", CelValue.String(prefix));
        builder.Set("needle", CelValue.Int(needle));
        builder.Set("key", CelValue.String(key));
        builder.Set("index", CelValue.Int(index));
        builder.Set("list", CelValue.List(list ?? [CelValue.Int(1), CelValue.Int(2)]));
        builder.Set("map", CelValue.Map(map ?? new Dictionary<string, CelValue> { ["name"] = CelValue.Bool(true) }));
        builder.Set(
            "obj",
            CelValue.Object(new CelObjectValue(
                "widget",
                new Dictionary<string, CelValue>
                {
                    ["name"] = CelValue.String(objName),
                    ["count"] = CelValue.Int(objCount),
                })));
        return builder.Build();
    }
}
