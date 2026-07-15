using ArchLinterNet.CEL;
using ArchLinterNet.CEL.Compilation;
using ArchLinterNet.CEL.Diagnostics;
using ArchLinterNet.CEL.Evaluation;
using ArchLinterNet.CEL.Profile;
using ArchLinterNet.CEL.Schema;
using ArchLinterNet.CEL.Values;
using NUnit.Framework;

namespace ArchLinterNet.CEL.Tests;

/// <summary>
/// Coverage tests that exercise internal constructors and stub behaviors via
/// <c>InternalsVisibleTo</c>. These are distinct from the API shape tests because they require
/// direct access to internal members that no public path can produce yet (e.g. compiled programs,
/// evaluation results) while the parser and evaluator (#325–#327) are stubs.
/// </summary>
[TestFixture]
public sealed class CelInternalApiCoverageTests
{
    // ── CelCompilationKey — internal constructor and equality ─────────────────

    [Test]
    public void CelCompilationKey_InternalConstructor_SetsAllProperties()
    {
        var key = new CelCompilationKey(
            normalizedSource: "x == 1",
            profileId: CelProfile.V1.Id,
            schemaIdentity: "schema-id",
            requiredResultType: CelRequiredResultType.Predicate,
            compilationLimitsIdentity: "len=4096,nest=32,ids=64",
            evaluationLimitsIdentity: "iters=1000,cost=100000");

        Assert.That(key.NormalizedSource, Is.EqualTo("x == 1"));
        Assert.That(key.ProfileId, Is.EqualTo(CelProfile.V1.Id));
        Assert.That(key.SchemaIdentity, Is.EqualTo("schema-id"));
        Assert.That(key.RequiredResultType, Is.EqualTo(CelRequiredResultType.Predicate));
        Assert.That(key.CompilationLimitsIdentity, Is.EqualTo("len=4096,nest=32,ids=64"));
        Assert.That(key.EvaluationLimitsIdentity, Is.EqualTo("iters=1000,cost=100000"));
    }

    [Test]
    public void CelCompilationKey_EqualToItself()
    {
        var key = BuildKey("x");
        Assert.That(key.Equals(key), Is.True);
    }

    [Test]
    public void CelCompilationKey_EqualToNull_ReturnsFalse()
    {
        var key = BuildKey("x");
        Assert.That(key.Equals(null), Is.False);
    }

    [Test]
    public void CelCompilationKey_ToString_ContainsProfileAndResultType()
    {
        var key = BuildKey("x");
        var s = key.ToString();
        Assert.That(s, Does.Contain("arch-linter/cel/v1"));
        Assert.That(s, Does.Contain("Predicate"));
    }

    // ── CelCompiledPredicate — internal constructor and stub Evaluate ─────────

    [Test]
    public void CelCompiledPredicate_InternalConstructor_SetsAllProperties()
    {
        var schema = BuildSimpleSchema();
        var key = BuildKey("x == 1");
        var pred = new CelCompiledPredicate(CelProfile.V1, schema, key, CelCompilationLimits.SafeDefaults, CelEvaluationLimits.SafeDefaults);

        Assert.That(pred.Profile, Is.SameAs(CelProfile.V1));
        Assert.That(pred.Schema, Is.SameAs(schema));
        Assert.That(pred.CompilationKey, Is.SameAs(key));
        Assert.That(pred.CompilationLimits, Is.SameAs(CelCompilationLimits.SafeDefaults));
    }

    [Test]
    public void CelCompiledPredicate_Evaluate_ThrowsNotImplementedException()
    {
        var schema = BuildSimpleSchema();
        var handle = schema.Variables[0];
        var ctx = schema.CreateEvaluationContextBuilder()
            .Set(handle, CelValue.String("v"))
            .Build();
        var pred = new CelCompiledPredicate(
            CelProfile.V1, schema, BuildKey("x"), CelCompilationLimits.SafeDefaults, CelEvaluationLimits.SafeDefaults);

        Assert.That(
            () => pred.Evaluate(ctx, CelEvaluationLimits.SafeDefaults),
            Throws.TypeOf<NotImplementedException>());
    }

    [Test]
    public void CelCompiledPredicate_Evaluate_SafeDefaultOverload_ThrowsNotImplementedException()
    {
        var schema = BuildSimpleSchema();
        var handle = schema.Variables[0];
        var ctx = schema.CreateEvaluationContextBuilder()
            .Set(handle, CelValue.String("v"))
            .Build();
        var pred = new CelCompiledPredicate(
            CelProfile.V1, schema, BuildKey("x"), CelCompilationLimits.SafeDefaults, CelEvaluationLimits.SafeDefaults);

        Assert.That(
            () => pred.Evaluate(ctx),
            Throws.TypeOf<NotImplementedException>(),
            "The safe-default Evaluate(context) overload must delegate to Evaluate(context, limits).");
    }

    [Test]
    public void CelCompiledPredicate_Evaluate_LimitsExceedCeiling_ThrowsArgumentException()
    {
        var schema = BuildSimpleSchema();
        var handle = schema.Variables[0];
        var ctx = schema.CreateEvaluationContextBuilder()
            .Set(handle, CelValue.String("v"))
            .Build();
        var ceiling = new CelEvaluationLimits(maxIterations: 10, maxCostUnits: 100);
        var pred = new CelCompiledPredicate(
            CelProfile.V1, schema, BuildKey("x"), CelCompilationLimits.SafeDefaults, ceiling);

        // SafeDefaults (1000 iterations) exceeds the captured ceiling (10) — programmer error.
        Assert.That(
            () => pred.Evaluate(ctx, CelEvaluationLimits.SafeDefaults),
            Throws.ArgumentException,
            "Per-call limits above the environment ceiling must be rejected.");
    }

    [Test]
    public void CelCompiledPredicate_Evaluate_DefaultOverload_UsesCapturedCeiling()
    {
        var schema = BuildSimpleSchema();
        var handle = schema.Variables[0];
        var ctx = schema.CreateEvaluationContextBuilder()
            .Set(handle, CelValue.String("v"))
            .Build();
        var ceiling = new CelEvaluationLimits(maxIterations: 10, maxCostUnits: 100);
        var pred = new CelCompiledPredicate(
            CelProfile.V1, schema, BuildKey("x"), CelCompilationLimits.SafeDefaults, ceiling);

        Assert.That(pred.EvaluationLimits, Is.SameAs(ceiling));
        // Default overload uses the captured ceiling, never the global SafeDefaults, so it must
        // pass the ceiling check and reach the not-yet-implemented evaluator stub.
        Assert.That(() => pred.Evaluate(ctx), Throws.TypeOf<NotImplementedException>());
    }

    [Test]
    public void CelCompiledPredicate_Evaluate_TighterLimits_PassCeilingCheck()
    {
        var schema = BuildSimpleSchema();
        var handle = schema.Variables[0];
        var ctx = schema.CreateEvaluationContextBuilder()
            .Set(handle, CelValue.String("v"))
            .Build();
        var pred = new CelCompiledPredicate(
            CelProfile.V1, schema, BuildKey("x"), CelCompilationLimits.SafeDefaults, CelEvaluationLimits.SafeDefaults);

        var tighter = new CelEvaluationLimits(maxIterations: 1, maxCostUnits: 1);
        Assert.That(() => pred.Evaluate(ctx, tighter), Throws.TypeOf<NotImplementedException>());
    }

    // ── CelCompiledExpression — internal constructor and stub Evaluate ────────

    [Test]
    public void CelCompiledExpression_InternalConstructor_SetsAllProperties()
    {
        var schema = BuildSimpleSchema();
        var key = BuildKey("x + 1");
        var expr = new CelCompiledExpression(CelProfile.V1, schema, key, CelCompilationLimits.SafeDefaults, CelEvaluationLimits.SafeDefaults);

        Assert.That(expr.Profile, Is.SameAs(CelProfile.V1));
        Assert.That(expr.Schema, Is.SameAs(schema));
        Assert.That(expr.CompilationKey, Is.SameAs(key));
        Assert.That(expr.CompilationLimits, Is.SameAs(CelCompilationLimits.SafeDefaults));
    }

    [Test]
    public void CelCompiledExpression_Evaluate_ThrowsNotImplementedException()
    {
        var schema = BuildSimpleSchema();
        var handle = schema.Variables[0];
        var ctx = schema.CreateEvaluationContextBuilder()
            .Set(handle, CelValue.String("v"))
            .Build();
        var expr = new CelCompiledExpression(
            CelProfile.V1, schema, BuildKey("x"), CelCompilationLimits.SafeDefaults, CelEvaluationLimits.SafeDefaults);

        Assert.That(
            () => expr.Evaluate(ctx, CelEvaluationLimits.SafeDefaults),
            Throws.TypeOf<NotImplementedException>());
    }

    [Test]
    public void CelCompiledExpression_Evaluate_SafeDefaultOverload_ThrowsNotImplementedException()
    {
        var schema = BuildSimpleSchema();
        var handle = schema.Variables[0];
        var ctx = schema.CreateEvaluationContextBuilder()
            .Set(handle, CelValue.String("v"))
            .Build();
        var expr = new CelCompiledExpression(
            CelProfile.V1, schema, BuildKey("x"), CelCompilationLimits.SafeDefaults, CelEvaluationLimits.SafeDefaults);

        Assert.That(
            () => expr.Evaluate(ctx),
            Throws.TypeOf<NotImplementedException>(),
            "The safe-default Evaluate(context) overload must delegate to Evaluate(context, limits).");
    }

    // ── CelEvaluationResult — internal constructor and AsBool paths ───────────

    [Test]
    public void CelEvaluationResult_SuccessBool_AsBool_ReturnsValue()
    {
        var result = new CelEvaluationResult(
            isSuccess: true,
            value: CelValue.Bool(true),
            diagnostics: []);

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.AsBool(), Is.True);
        Assert.That(result.Diagnostics, Is.Empty);
    }

    [Test]
    public void CelEvaluationResult_Failed_AsBool_ThrowsInvalidOperationException()
    {
        var result = new CelEvaluationResult(
            isSuccess: false,
            value: null,
            diagnostics:
            [
                new CelDiagnostic(
                    CelDiagnosticCode.EvaluationFailure,
                    "eval",
                    CelDiagnosticSeverity.Error,
                    span: null,
                    "evaluation failed"),
            ]);

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.Value, Is.Null);
        Assert.That(result.Diagnostics, Has.Count.EqualTo(1));
        Assert.That(() => result.AsBool(), Throws.InvalidOperationException);
    }

    [Test]
    public void CelEvaluationResult_SuccessNonBool_AsBool_ThrowsInvalidOperationException()
    {
        var result = new CelEvaluationResult(
            isSuccess: true,
            value: CelValue.String("hello"),
            diagnostics: []);

        Assert.That(() => result.AsBool(), Throws.InvalidOperationException);
    }

    // ── CelDiagnostic with non-null CelSourceSpan ────────────────────────────

    [Test]
    public void CelDiagnostic_WithSourceSpan_ExposesSpan()
    {
        var span = new CelSourceSpan(2, 7);
        var diag = new CelDiagnostic(
            CelDiagnosticCode.SyntaxError,
            "parser",
            CelDiagnosticSeverity.Error,
            span,
            "unexpected token");

        Assert.That(diag.Span, Is.Not.Null);
        Assert.That(diag.Span!.Value.Start, Is.EqualTo(2));
        Assert.That(diag.Span!.Value.End, Is.EqualTo(7));
        Assert.That(diag.Code, Is.EqualTo(CelDiagnosticCode.SyntaxError));
    }

    [Test]
    public void CelDiagnostic_WithoutParameters_ExposesEmptyParameters()
    {
        var diag = new CelDiagnostic(
            CelDiagnosticCode.SyntaxError,
            "parser",
            CelDiagnosticSeverity.Error,
            span: null,
            "unexpected token");

        Assert.That(diag.Parameters, Is.Not.Null);
        Assert.That(diag.Parameters, Is.Empty);
    }

    [Test]
    public void CelDiagnostic_WithParameters_ExposesFrozenParameters()
    {
        var source = new Dictionary<string, string> { ["limitName"] = "MaxIterations" };
        var diag = new CelDiagnostic(
            CelDiagnosticCode.BudgetExceeded,
            "limits",
            CelDiagnosticSeverity.Error,
            span: null,
            "budget exceeded",
            parameters: source);

        Assert.That(diag.Parameters["limitName"], Is.EqualTo("MaxIterations"));
        Assert.That(diag.Parameters as Dictionary<string, string>, Is.Null,
            "Parameters must not be a mutable Dictionary<> that can be mutated via cast.");

        // Mutating the source dictionary after construction must not affect the diagnostic.
        source["limitName"] = "changed";
        Assert.That(diag.Parameters["limitName"], Is.EqualTo("MaxIterations"));
    }

    [Test]
    public void CelCompilationResult_BudgetExceeded_CarriesStructuredParameters()
    {
        var schemaBuilder = CelContextSchema.CreateBuilder("s");
        schemaBuilder.AddVariable("x", CelType.String);
        var env = CelEnvironment.CreateBuilder(CelProfile.V1)
            .WithContextSchema(schemaBuilder.Build())
            .WithCompilationLimits(new CelCompilationLimits(
                maxExpressionLength: 3, maxNestingDepth: 4, maxIdentifierCount: 4,
                maxTokenCount: 64, maxAstNodeCount: 64, maxLiteralSize: 64))
            .Build();

        var result = env.CompilePredicate("x == 'long'");

        Assert.That(result.Diagnostics[0].Code, Is.EqualTo(CelDiagnosticCode.BudgetExceeded));
        Assert.That(result.Diagnostics[0].Parameters["limitName"], Is.EqualTo("MaxExpressionLength"));
        Assert.That(result.Diagnostics[0].Parameters, Contains.Key("observedValue"));
    }

    [Test]
    public void CelSourceSpan_ZeroLengthSpan_IsValid()
    {
        var span = new CelSourceSpan(5, 5);
        Assert.That(span.Start, Is.EqualTo(5));
        Assert.That(span.End, Is.EqualTo(5));
    }

    [Test]
    public void CelSourceSpan_ToString_ContainsStartAndEnd()
    {
        var span = new CelSourceSpan(3, 9);
        Assert.That(span.ToString(), Does.Contain("3"));
        Assert.That(span.ToString(), Does.Contain("9"));
    }

    // ── Builder mutation after Build() must not affect environment (Critical 3) ─

    [Test]
    public void CelEnvironmentBuilder_MutatingAfterBuild_DoesNotAffectBuiltEnvironment()
    {
        var schemaBuilder = CelContextSchema.CreateBuilder("s");
        schemaBuilder.AddVariable("x", CelType.String);
        var schema = schemaBuilder.Build();

        var objSchemaA = CelObjectSchema.CreateBuilder("typeA").Build();

        var builder = CelEnvironment.CreateBuilder(CelProfile.V1)
            .WithContextSchema(schema)
            .WithObjectSchema(objSchemaA);
        var env = builder.Build();

        // Add another object schema AFTER Build() — must not appear in the already-built env.
        var objSchemaB = CelObjectSchema.CreateBuilder("typeB").Build();
        builder.WithObjectSchema(objSchemaB);

        Assert.That(env.ObjectSchemas, Contains.Key("typeA"));
        Assert.That(env.ObjectSchemas, Does.Not.ContainKey("typeB"));
    }

    // ── Cast-and-mutate immutability regressions (Important 4) ───────────────

    [Test]
    public void CelContextSchema_Variables_CannotMutateThroughCast()
    {
        var schemaBuilder = CelContextSchema.CreateBuilder("s");
        schemaBuilder.AddVariable("a", CelType.Bool);
        var schema = schemaBuilder.Build();

        // Variables is IReadOnlyList<CelVariable> — must NOT be castable back to List<> or T[].
        Assert.That(schema.Variables as CelVariable[], Is.Null,
            "Variables must not be a raw array that can be mutated via cast.");
        Assert.That(schema.Variables as List<CelVariable>, Is.Null,
            "Variables must not be a mutable List<> that can be mutated via cast.");
    }

    [Test]
    public void CelObjectSchema_Members_CannotMutateThroughCast()
    {
        var objSchemaBuilder = CelObjectSchema.CreateBuilder("t");
        objSchemaBuilder.AddMember("n", CelType.Int);
        var objSchema = objSchemaBuilder.Build();

        Assert.That(objSchema.Members as CelObjectMember[], Is.Null,
            "Members must not be a raw array that can be mutated via cast.");
        Assert.That(objSchema.Members as List<CelObjectMember>, Is.Null,
            "Members must not be a mutable List<> that can be mutated via cast.");
    }

    [Test]
    public void CelEvaluationContext_Assignments_CannotMutateThroughCast()
    {
        var schema = BuildSimpleSchema();
        var handle = schema.Variables[0];
        var ctx = schema.CreateEvaluationContextBuilder()
            .Set(handle, CelValue.String("v"))
            .Build();

        Assert.That(ctx.Assignments as List<(CelVariable, CelValue)>, Is.Null,
            "Assignments must not be a mutable List<> that can be mutated via cast.");
    }

    [Test]
    public void CelCompilationResult_Diagnostics_CannotMutateThroughCast()
    {
        var schemaBuilder = CelContextSchema.CreateBuilder("s");
        schemaBuilder.AddVariable("x", CelType.String);
        var env = CelEnvironment.CreateBuilder(CelProfile.V1)
            .WithContextSchema(schemaBuilder.Build())
            .Build();

        var result = env.CompilePredicate("true");

        Assert.That(result.Diagnostics as CelDiagnostic[], Is.Null,
            "Diagnostics must not be a raw array that can be mutated via cast.");
        Assert.That(result.Diagnostics as List<CelDiagnostic>, Is.Null,
            "Diagnostics must not be a mutable List<> that can be mutated via cast.");
    }

    [Test]
    public void CelEvaluationResult_Diagnostics_CannotMutateThroughCast()
    {
        var result = new CelEvaluationResult(
            isSuccess: false,
            value: null,
            diagnostics:
            [
                new CelDiagnostic(
                    CelDiagnosticCode.EvaluationFailure,
                    "eval",
                    CelDiagnosticSeverity.Error,
                    span: null,
                    "evaluation failed"),
            ]);

        Assert.That(result.Diagnostics as CelDiagnostic[], Is.Null,
            "Diagnostics must not be a raw array that can be mutated via cast.");
        Assert.That(result.Diagnostics as List<CelDiagnostic>, Is.Null,
            "Diagnostics must not be a mutable List<> that can be mutated via cast.");
    }

    // ── Depth limit prevents stack overflow via public Set() ─────────────────

    [Test]
    public void CelEvaluationContextBuilder_DeeplyNestedList_FailsValidation()
    {
        // Build a declared type AND a value both nested 21 levels deep, so the declared type
        // matches the value at every level and the only possible rejection is the depth budget
        // (MaxValidationDepth = 16) — not an ordinary type mismatch.
        const int Nesting = 21;
        var declaredType = CelType.Int;
        var value = CelValue.Int(1);
        for (var i = 0; i < Nesting; i++)
        {
            declaredType = CelType.ListOf(declaredType);
            value = CelValue.List([value]);
        }

        var schemaBuilder = CelContextSchema.CreateBuilder("ctx");
        var handle = schemaBuilder.AddVariable("v", declaredType);
        var schema = schemaBuilder.Build();

        Assert.That(
            () => schema.CreateEvaluationContextBuilder().Set(handle, value),
            Throws.ArgumentException,
            "Set() must reject values that exceed the maximum structural validation depth.");
    }

    [Test]
    public void CelEvaluationContextBuilder_NestedListWithinDepthLimit_PassesValidation()
    {
        // 10 levels is comfortably within MaxValidationDepth (16); with a matching declared
        // type the assignment must succeed, proving the previous test fails on depth alone.
        const int Nesting = 10;
        var declaredType = CelType.Int;
        var value = CelValue.Int(1);
        for (var i = 0; i < Nesting; i++)
        {
            declaredType = CelType.ListOf(declaredType);
            value = CelValue.List([value]);
        }

        var schemaBuilder = CelContextSchema.CreateBuilder("ctx");
        var handle = schemaBuilder.AddVariable("v", declaredType);
        var schema = schemaBuilder.Build();

        Assert.That(
            () => schema.CreateEvaluationContextBuilder().Set(handle, value),
            Throws.Nothing);
    }

    // ── Name-based Set() convenience overload (issue #168 benchmark surface) ─

    [Test]
    public void CelEvaluationContextBuilder_SetByName_ResolvesToDeclaredHandle()
    {
        var schemaBuilder = CelContextSchema.CreateBuilder("ctx");
        schemaBuilder.AddVariable("x", CelType.String);
        var schema = schemaBuilder.Build();

        var ctx = schema.CreateEvaluationContextBuilder()
            .Set("x", CelValue.String("v"))
            .Build();

        Assert.That(ctx.Assignments, Has.Count.EqualTo(1));
        Assert.That(ctx.Assignments[0].Variable.Name, Is.EqualTo("x"));
        Assert.That(ctx.Assignments[0].Value.AsString(), Is.EqualTo("v"));
    }

    [Test]
    public void CelEvaluationContextBuilder_SetByName_UnknownName_ThrowsArgumentException()
    {
        var schema = BuildSimpleSchema();

        Assert.That(
            () => schema.CreateEvaluationContextBuilder().Set("does-not-exist", CelValue.String("v")),
            Throws.ArgumentException);
    }

    [Test]
    public void CelEvaluationContextBuilder_SetByName_AndSetByHandle_ProduceEquivalentContexts()
    {
        var schemaBuilder = CelContextSchema.CreateBuilder("ctx");
        var handle = schemaBuilder.AddVariable("x", CelType.String);
        var schema = schemaBuilder.Build();

        var byHandle = schema.CreateEvaluationContextBuilder().Set(handle, CelValue.String("v")).Build();
        var byName = schema.CreateEvaluationContextBuilder().Set("x", CelValue.String("v")).Build();

        Assert.That(byHandle.Assignments[0].Value.AsString(), Is.EqualTo(byName.Assignments[0].Value.AsString()));
    }

    // ── Strict object validation: exact member-set match (Profile v1 has no
    //    null/optional members, so missing and extra members are both rejected) ─

    private static (CelContextSchema Schema, CelVariable Handle, CelEnvironment Env) BuildObjectEnvironment()
    {
        var schemaBuilder = CelContextSchema.CreateBuilder("obj-ctx");
        var handle = schemaBuilder.AddVariable("o", CelType.ObjectOf("thing"));
        var schema = schemaBuilder.Build();

        var objSchemaBuilder = CelObjectSchema.CreateBuilder("thing");
        objSchemaBuilder.AddMember("name", CelType.String);
        objSchemaBuilder.AddMember("count", CelType.Int);
        var objSchema = objSchemaBuilder.Build();

        var env = CelEnvironment.CreateBuilder(CelProfile.V1)
            .WithContextSchema(schema)
            .WithObjectSchema(objSchema)
            .Build();
        return (schema, handle, env);
    }

    [Test]
    public void CelEvaluationContextBuilder_ObjectWithAllDeclaredMembers_PassesValidation()
    {
        var (_, handle, env) = BuildObjectEnvironment();
        var value = CelValue.Object(new CelObjectValue("thing", new Dictionary<string, CelValue>
        {
            ["name"] = CelValue.String("a"),
            ["count"] = CelValue.Int(1),
        }));

        Assert.That(
            () => env.CreateEvaluationContextBuilder().Set(handle, value),
            Throws.Nothing);
    }

    [Test]
    public void CelEvaluationContextBuilder_ObjectMissingDeclaredMember_FailsValidation()
    {
        var (_, handle, env) = BuildObjectEnvironment();
        var value = CelValue.Object(new CelObjectValue("thing", new Dictionary<string, CelValue>
        {
            ["name"] = CelValue.String("a"),
            // "count" is missing — normatively impossible at evaluation time, so rejected here.
        }));

        Assert.That(
            () => env.CreateEvaluationContextBuilder().Set(handle, value),
            Throws.ArgumentException);
    }

    [Test]
    public void CelEvaluationContextBuilder_EmptyObjectAgainstNonEmptySchema_FailsValidation()
    {
        var (_, handle, env) = BuildObjectEnvironment();
        var value = CelValue.Object(new CelObjectValue("thing", new Dictionary<string, CelValue>()));

        Assert.That(
            () => env.CreateEvaluationContextBuilder().Set(handle, value),
            Throws.ArgumentException);
    }

    [Test]
    public void CelEvaluationContextBuilder_ObjectWithExtraMember_FailsValidation()
    {
        var (_, handle, env) = BuildObjectEnvironment();
        var value = CelValue.Object(new CelObjectValue("thing", new Dictionary<string, CelValue>
        {
            ["name"] = CelValue.String("a"),
            ["count"] = CelValue.Int(1),
            ["extra"] = CelValue.Bool(true),
        }));

        Assert.That(
            () => env.CreateEvaluationContextBuilder().Set(handle, value),
            Throws.ArgumentException);
    }

    [Test]
    public void CelEvaluationContextBuilder_UnregisteredObjectTypeId_FailsValidation()
    {
        var schemaBuilder = CelContextSchema.CreateBuilder("obj-ctx");
        var handle = schemaBuilder.AddVariable("o", CelType.ObjectOf("unregistered"));
        var schema = schemaBuilder.Build();
        var env = CelEnvironment.CreateBuilder(CelProfile.V1)
            .WithContextSchema(schema)
            .Build();

        var value = CelValue.Object(new CelObjectValue("unregistered", new Dictionary<string, CelValue>()));

        Assert.That(
            () => env.CreateEvaluationContextBuilder().Set(handle, value),
            Throws.ArgumentException,
            "An object type with no registered schema cannot be validated and must be rejected.");
    }

    [Test]
    public void CelEvaluationContextBuilder_NestedObjectMissingMember_FailsValidation()
    {
        var schemaBuilder = CelContextSchema.CreateBuilder("obj-ctx");
        var handle = schemaBuilder.AddVariable("o", CelType.ObjectOf("outer"));
        var schema = schemaBuilder.Build();

        var innerBuilder = CelObjectSchema.CreateBuilder("inner");
        innerBuilder.AddMember("id", CelType.Int);
        var innerSchema = innerBuilder.Build();

        var outerBuilder = CelObjectSchema.CreateBuilder("outer");
        outerBuilder.AddMember("child", CelType.ObjectOf("inner"));
        var outerSchema = outerBuilder.Build();

        var env = CelEnvironment.CreateBuilder(CelProfile.V1)
            .WithContextSchema(schema)
            .WithObjectSchema(innerSchema)
            .WithObjectSchema(outerSchema)
            .Build();

        // Inner object is missing its declared "id" member.
        var value = CelValue.Object(new CelObjectValue("outer", new Dictionary<string, CelValue>
        {
            ["child"] = CelValue.Object(new CelObjectValue("inner", new Dictionary<string, CelValue>())),
        }));

        Assert.That(
            () => env.CreateEvaluationContextBuilder().Set(handle, value),
            Throws.ArgumentException);
    }

    [Test]
    public void CelEvaluationContextBuilder_SchemaOnlyBuilder_ObjectValue_ThrowsInvalidOperation()
    {
        var schemaBuilder = CelContextSchema.CreateBuilder("obj-ctx");
        var handle = schemaBuilder.AddVariable("o", CelType.ObjectOf("thing"));
        var schema = schemaBuilder.Build();

        var value = CelValue.Object(new CelObjectValue("thing", new Dictionary<string, CelValue>()));

        // schema.CreateEvaluationContextBuilder() has no object catalog; accepting the object
        // unvalidated would bypass schema invariants, so it must throw instead.
        Assert.That(
            () => schema.CreateEvaluationContextBuilder().Set(handle, value),
            Throws.InvalidOperationException);
    }

    // ── Collection-size limit prevents unbounded CPU use via public Set() ────

    [Test]
    public void CelEvaluationContextBuilder_OversizedList_FailsValidation()
    {
        // 1025 elements exceeds MaxValidationCollectionSize (1024).
        var oversized = CelValue.List(Enumerable.Range(0, 1025).Select(i => CelValue.Int(i)).ToList());

        var schemaBuilder = CelContextSchema.CreateBuilder("ctx");
        var handle = schemaBuilder.AddVariable("v", CelType.ListOf(CelType.Int));
        var schema = schemaBuilder.Build();

        Assert.That(
            () => schema.CreateEvaluationContextBuilder().Set(handle, oversized),
            Throws.ArgumentException,
            "Set() must reject list values that exceed the maximum collection size.");
    }

    [Test]
    public void CelEvaluationContextBuilder_OversizedMap_FailsValidation()
    {
        // 1025 entries exceeds MaxValidationCollectionSize (1024).
        var entries = Enumerable.Range(0, 1025)
            .ToDictionary(i => i.ToString(), i => CelValue.Int(i));
        var oversized = CelValue.Map(entries);

        var schemaBuilder = CelContextSchema.CreateBuilder("ctx");
        var handle = schemaBuilder.AddVariable("v", CelType.MapOf(CelType.Int));
        var schema = schemaBuilder.Build();

        Assert.That(
            () => schema.CreateEvaluationContextBuilder().Set(handle, oversized),
            Throws.ArgumentException,
            "Set() must reject map values that exceed the maximum collection size.");
    }

    [Test]
    public void CelEvaluationContextBuilder_ListAtSizeLimit_PassesValidation()
    {
        // Exactly 1024 elements is within MaxValidationCollectionSize.
        var atLimit = CelValue.List(Enumerable.Range(0, 1024).Select(i => CelValue.Int(i)).ToList());

        var schemaBuilder = CelContextSchema.CreateBuilder("ctx");
        var handle = schemaBuilder.AddVariable("v", CelType.ListOf(CelType.Int));
        var schema = schemaBuilder.Build();

        Assert.That(
            () => schema.CreateEvaluationContextBuilder().Set(handle, atLimit),
            Throws.Nothing);
    }

    // ── CEL identifier validation on schema-declared names ───────────────────

    [Test]
    public void CelContextSchemaBuilder_NonIdentifierVariableName_ThrowsArgumentException()
    {
        var builder = CelContextSchema.CreateBuilder("s");
        Assert.That(() => builder.AddVariable("source role", CelType.String), Throws.ArgumentException);
        Assert.That(() => builder.AddVariable("1st", CelType.String), Throws.ArgumentException);
        Assert.That(() => builder.AddVariable("a-b", CelType.String), Throws.ArgumentException);
        Assert.That(() => builder.AddVariable("_ok1", CelType.String), Throws.Nothing);
    }

    [Test]
    public void CelObjectSchemaBuilder_NonIdentifierMemberName_ThrowsArgumentException()
    {
        var builder = CelObjectSchema.CreateBuilder("t");
        Assert.That(() => builder.AddMember("my member", CelType.String), Throws.ArgumentException);
        Assert.That(() => builder.AddMember("9lives", CelType.Int), Throws.ArgumentException);
        Assert.That(() => builder.AddMember("valid_Name2", CelType.Bool), Throws.Nothing);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static CelContextSchema BuildSimpleSchema()
    {
        var builder = CelContextSchema.CreateBuilder("cov-v1");
        builder.AddVariable("x", CelType.String);
        return builder.Build();
    }

    private static CelCompilationKey BuildKey(string source) =>
        new(
            normalizedSource: source,
            profileId: CelProfile.V1.Id,
            schemaIdentity: "cov-v1-identity",
            requiredResultType: CelRequiredResultType.Predicate,
            compilationLimitsIdentity: CelCompilationLimits.SafeDefaults.ComputeIdentity(),
            evaluationLimitsIdentity: CelEvaluationLimits.SafeDefaults.ComputeIdentity());
}
