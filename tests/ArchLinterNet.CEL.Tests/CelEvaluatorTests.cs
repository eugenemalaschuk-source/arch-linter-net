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
    public void LogicalOperators_DeterminingRightOperandAbsorbsOrdinaryLeftFailure()
    {
        var env = BuildEnvironment();
        var orCompilation = env.CompilePredicate("map[key] || x");
        var andCompilation = env.CompilePredicate("map[key] && y");

        Assert.Multiple(() =>
        {
            Assert.That(orCompilation.IsSuccess, Is.True);
            Assert.That(andCompilation.IsSuccess, Is.True);
        });

        var orResult = orCompilation.Program!.Evaluate(BuildContext(
            env,
            x: true,
            key: "missing",
            map: new Dictionary<string, CelValue>()));
        var andResult = andCompilation.Program!.Evaluate(BuildContext(
            env,
            y: false,
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
    public void Evaluate_WithDifferentObjectSchemaCatalogForSameObjectType_ReturnsSchemaMismatch()
    {
        var contextSchemaBuilder = CelContextSchema.CreateBuilder("shared-schema");
        contextSchemaBuilder.AddVariable("obj", CelType.ObjectOf("widget"));
        var contextSchema = contextSchemaBuilder.Build();

        var intWidgetBuilder = CelObjectSchema.CreateBuilder("widget");
        intWidgetBuilder.AddMember("count", CelType.Int);
        var intWidget = intWidgetBuilder.Build();

        var stringWidgetBuilder = CelObjectSchema.CreateBuilder("widget");
        stringWidgetBuilder.AddMember("count", CelType.String);
        var stringWidget = stringWidgetBuilder.Build();

        var envA = CelEnvironment.CreateBuilder(CelProfile.V1)
            .WithContextSchema(contextSchema)
            .WithObjectSchema(intWidget)
            .WithEvaluationLimits(CelEvaluationLimits.SafeDefaults)
            .Build();
        var envB = CelEnvironment.CreateBuilder(CelProfile.V1)
            .WithContextSchema(contextSchema)
            .WithObjectSchema(stringWidget)
            .WithEvaluationLimits(CelEvaluationLimits.SafeDefaults)
            .Build();

        var compilation = envA.CompilePredicate("obj.count > 0");
        Assert.That(compilation.IsSuccess, Is.True);

        var context = envB.CreateEvaluationContextBuilder()
            .Set(
                "obj",
                CelValue.Object(new CelObjectValue(
                    "widget",
                    new Dictionary<string, CelValue>
                    {
                        ["count"] = CelValue.String("wrong type"),
                    })))
            .Build();

        var result = compilation.Program!.Evaluate(context);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Diagnostics[0].Code, Is.EqualTo(CelDiagnosticCode.SchemaMismatch));
            Assert.That(result.Diagnostics[0].Parameters["expectedSchemaId"], Is.EqualTo(compilation.Program.CompilationKey.SchemaIdentity));
            Assert.That(result.Diagnostics[0].Parameters["schemaId"], Is.Not.EqualTo(compilation.Program.CompilationKey.SchemaIdentity));
        });
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
    public void Evaluate_MaxCostUnitsExactBoundary_Succeeds()
    {
        var env = BuildEnvironment();
        var compilation = env.CompilePredicate("needle in list");

        Assert.That(compilation.IsSuccess, Is.True);

        var result = compilation.Program!.Evaluate(
            BuildContext(
                env,
                needle: 2,
                list: [CelValue.Int(1), CelValue.Int(2)]),
            new CelEvaluationLimits(
                maxIterations: env.EvaluationLimits.MaxIterations,
                maxCostUnits: 2));

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.AsBool(), Is.True);
    }

    [Test]
    public void LogicalOperators_DoNotAbsorbBudgetExceededFromLeftOperand()
    {
        var env = BuildEnvironment();
        var orCompilation = env.CompilePredicate("s.contains(prefix) || x");
        var andCompilation = env.CompilePredicate("s.contains(prefix) && y");

        Assert.Multiple(() =>
        {
            Assert.That(orCompilation.IsSuccess, Is.True);
            Assert.That(andCompilation.IsSuccess, Is.True);
        });

        var tightLimits = new CelEvaluationLimits(
            maxIterations: env.EvaluationLimits.MaxIterations,
            maxCostUnits: 100);

        var orResult = orCompilation.Program!.Evaluate(
            BuildContext(env, x: true, s: new string('a', 12), prefix: new string('a', 12)),
            tightLimits);
        var andResult = andCompilation.Program!.Evaluate(
            BuildContext(env, y: false, s: new string('a', 12), prefix: new string('a', 12)),
            tightLimits);

        Assert.Multiple(() =>
        {
            Assert.That(orResult.IsSuccess, Is.False);
            Assert.That(orResult.Diagnostics[0].Code, Is.EqualTo(CelDiagnosticCode.BudgetExceeded));
            Assert.That(andResult.IsSuccess, Is.False);
            Assert.That(andResult.Diagnostics[0].Code, Is.EqualTo(CelDiagnosticCode.BudgetExceeded));
        });
    }

    [Test]
    public void LogicalOperators_DeterminingLeftOperand_SkipsBudgetedRightOperand()
    {
        var env = BuildEnvironment();
        var orCompilation = env.CompilePredicate("x || s.contains(prefix)");
        var andCompilation = env.CompilePredicate("x && s.contains(prefix)");

        Assert.Multiple(() =>
        {
            Assert.That(orCompilation.IsSuccess, Is.True);
            Assert.That(andCompilation.IsSuccess, Is.True);
        });

        var tightLimits = new CelEvaluationLimits(
            maxIterations: env.EvaluationLimits.MaxIterations,
            maxCostUnits: 100);

        var orResult = orCompilation.Program!.Evaluate(
            BuildContext(env, x: true, s: new string('a', 12), prefix: new string('a', 12)),
            tightLimits);
        var andResult = andCompilation.Program!.Evaluate(
            BuildContext(env, x: false, s: new string('a', 12), prefix: new string('a', 12)),
            tightLimits);

        Assert.Multiple(() =>
        {
            Assert.That(orResult.IsSuccess, Is.True);
            Assert.That(orResult.AsBool(), Is.True);
            Assert.That(andResult.IsSuccess, Is.True);
            Assert.That(andResult.AsBool(), Is.False);
        });
    }

    [Test]
    public void InList_ChargesCostProportionalToCollectionSize()
    {
        var env = BuildEnvironment();
        var compilation = env.CompilePredicate("needle in list");

        Assert.That(compilation.IsSuccess, Is.True);

        var result = compilation.Program!.Evaluate(
            BuildContext(
                env,
                needle: 999,
                list: Enumerable.Range(0, 32).Select(value => CelValue.Int(value)).ToArray()),
            new CelEvaluationLimits(
                maxIterations: env.EvaluationLimits.MaxIterations,
                maxCostUnits: 10));

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.Diagnostics[0].Code, Is.EqualTo(CelDiagnosticCode.BudgetExceeded));
        Assert.That(result.Diagnostics[0].Parameters["limitName"], Is.EqualTo("MaxCostUnits"));
    }

    [Test]
    public void InMap_ChargesCostProportionalToLookupWork()
    {
        var env = BuildEnvironment();
        var compilation = env.CompilePredicate("key in map");

        Assert.That(compilation.IsSuccess, Is.True);

        var map = Enumerable.Range(0, 32)
            .ToDictionary(
                value => $"entry-{value}",
                _ => CelValue.Bool(true));

        var result = compilation.Program!.Evaluate(
            BuildContext(
                env,
                key: new string('k', 20),
                map: map),
            new CelEvaluationLimits(
                maxIterations: env.EvaluationLimits.MaxIterations,
                maxCostUnits: 50));

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.Diagnostics[0].Code, Is.EqualTo(CelDiagnosticCode.BudgetExceeded));
        Assert.That(result.Diagnostics[0].Parameters["limitName"], Is.EqualTo("MaxCostUnits"));
    }

    [Test]
    public void DeepEquality_ChargesCostForNestedCollectionTraversal()
    {
        var env = BuildNestedListEnvironment();
        var compilation = env.CompilePredicate("left == right");

        Assert.That(compilation.IsSuccess, Is.True);

        var leftValue = CelValue.List(
        [
            CelValue.List([CelValue.Int(1), CelValue.Int(2), CelValue.Int(3)]),
            CelValue.List([CelValue.Int(4), CelValue.Int(5), CelValue.Int(6)]),
            CelValue.List([CelValue.Int(7), CelValue.Int(8), CelValue.Int(9)]),
        ]);

        var result = compilation.Program!.Evaluate(
            BuildNestedListContext(env, leftValue, leftValue),
            new CelEvaluationLimits(
                maxIterations: env.EvaluationLimits.MaxIterations,
                maxCostUnits: 12));

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.Diagnostics[0].Code, Is.EqualTo(CelDiagnosticCode.BudgetExceeded));
        Assert.That(result.Diagnostics[0].Parameters["limitName"], Is.EqualTo("MaxCostUnits"));
    }

    [Test]
    public void FloatEquality_NaNIsNotEqualToNaN()
    {
        var env = BuildEnvironment();
        var compilation = env.CompilePredicate("f == g");

        Assert.That(compilation.IsSuccess, Is.True);

        var result = compilation.Program!.Evaluate(BuildContext(
            env,
            f: double.NaN,
            g: double.NaN));

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.AsBool(), Is.False);
    }

    [Test]
    public void FloatOrdering_WithNaN_ReturnsFalse()
    {
        var env = BuildEnvironment();
        var compilation = env.CompilePredicate("f < g");

        Assert.That(compilation.IsSuccess, Is.True);

        var result = compilation.Program!.Evaluate(BuildContext(
            env,
            f: double.NaN,
            g: 1.0));

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.AsBool(), Is.False);
    }

    [Test]
    public void FloatOrdering_WithInfinityAndZero_PreservesExpectedSemantics()
    {
        var env = BuildEnvironment();
        var compilation = env.CompilePredicate("f < g && g < h");

        Assert.That(compilation.IsSuccess, Is.True);

        var result = compilation.Program!.Evaluate(BuildContext(
            env,
            f: double.NegativeInfinity,
            g: 0.0,
            h: double.PositiveInfinity));

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.AsBool(), Is.True);
    }

    [Test]
    public void MapEquality_ReturnsTrueForStructurallyEqualMaps()
    {
        var env = BuildEnvironment();
        var compilation = env.CompilePredicate("map == otherMap");

        Assert.That(compilation.IsSuccess, Is.True);

        var map = new Dictionary<string, CelValue>
        {
            ["left"] = CelValue.Bool(true),
            ["right"] = CelValue.Bool(false),
        };

        var result = compilation.Program!.Evaluate(BuildContext(env, map: map, otherMap: map));

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.AsBool(), Is.True);
    }

    [Test]
    public void ObjectEquality_ReturnsFalseForDifferentMemberValues()
    {
        var env = BuildEnvironment();
        var compilation = env.CompilePredicate("obj == otherObj");

        Assert.That(compilation.IsSuccess, Is.True);

        var result = compilation.Program!.Evaluate(BuildContext(
            env,
            objCount: 1,
            otherObjCount: 2));

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.AsBool(), Is.False);
    }

    [Test]
    public void MapIndex_SuccessfulLookup_ReturnsValue()
    {
        var env = BuildEnvironment();
        var compilation = env.Compile("map[key]");

        Assert.That(compilation.IsSuccess, Is.True);

        var result = compilation.Program!.Evaluate(BuildContext(
            env,
            key: "target",
            map: new Dictionary<string, CelValue> { ["target"] = CelValue.Bool(true) }));

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Value, Is.Not.Null);
        Assert.That(result.Value!.AsBool(), Is.True);
    }

    [Test]
    public void ListIndex_SuccessfulLookup_ReturnsValue()
    {
        var env = BuildEnvironment();
        var compilation = env.Compile("list[index]");

        Assert.That(compilation.IsSuccess, Is.True);

        var result = compilation.Program!.Evaluate(BuildContext(
            env,
            index: 1,
            list: [CelValue.Int(1), CelValue.Int(7)]));

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Value, Is.Not.Null);
        Assert.That(result.Value!.AsInt(), Is.EqualTo(7));
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
        schemaBuilder.AddVariable("otherList", CelType.ListOf(CelType.Int));
        schemaBuilder.AddVariable("map", CelType.MapOf(CelType.Bool));
        schemaBuilder.AddVariable("otherMap", CelType.MapOf(CelType.Bool));
        schemaBuilder.AddVariable("obj", CelType.ObjectOf("widget"));
        schemaBuilder.AddVariable("otherObj", CelType.ObjectOf("widget"));
        schemaBuilder.AddVariable("f", CelType.Float);
        schemaBuilder.AddVariable("g", CelType.Float);
        schemaBuilder.AddVariable("h", CelType.Float);

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
        IReadOnlyList<CelValue>? otherList = null,
        IReadOnlyDictionary<string, CelValue>? map = null,
        IReadOnlyDictionary<string, CelValue>? otherMap = null,
        string objName = "name",
        long objCount = 1,
        string otherObjName = "name",
        long otherObjCount = 1,
        double f = 1.0,
        double g = 2.0,
        double h = 3.0)
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
        builder.Set("otherList", CelValue.List(otherList ?? [CelValue.Int(1), CelValue.Int(2)]));
        builder.Set("map", CelValue.Map(map ?? new Dictionary<string, CelValue> { ["name"] = CelValue.Bool(true) }));
        builder.Set("otherMap", CelValue.Map(otherMap ?? new Dictionary<string, CelValue> { ["name"] = CelValue.Bool(true) }));
        builder.Set(
            "obj",
            CelValue.Object(new CelObjectValue(
                "widget",
                new Dictionary<string, CelValue>
                {
                    ["name"] = CelValue.String(objName),
                    ["count"] = CelValue.Int(objCount),
                })));
        builder.Set(
            "otherObj",
            CelValue.Object(new CelObjectValue(
                "widget",
                new Dictionary<string, CelValue>
                {
                    ["name"] = CelValue.String(otherObjName),
                    ["count"] = CelValue.Int(otherObjCount),
                })));
        builder.Set("f", CelValue.Float(f));
        builder.Set("g", CelValue.Float(g));
        builder.Set("h", CelValue.Float(h));
        return builder.Build();
    }

    private static CelEnvironment BuildNestedListEnvironment()
    {
        var schemaBuilder = CelContextSchema.CreateBuilder("nested-list-eval-schema");
        schemaBuilder.AddVariable("left", CelType.ListOf(CelType.ListOf(CelType.Int)));
        schemaBuilder.AddVariable("right", CelType.ListOf(CelType.ListOf(CelType.Int)));

        return CelEnvironment.CreateBuilder(CelProfile.V1)
            .WithContextSchema(schemaBuilder.Build())
            .WithEvaluationLimits(CelEvaluationLimits.SafeDefaults)
            .Build();
    }

    private static CelEvaluationContext BuildNestedListContext(
        CelEnvironment env,
        CelValue left,
        CelValue right)
    {
        var builder = env.CreateEvaluationContextBuilder();
        builder.Set("left", left);
        builder.Set("right", right);
        return builder.Build();
    }
}
