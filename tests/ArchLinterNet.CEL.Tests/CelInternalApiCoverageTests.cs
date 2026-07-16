using ArchLinterNet.CEL;
using ArchLinterNet.CEL.Binding;
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
        var pred = new CelCompiledPredicate(CelProfile.V1, schema, key, CelCompilationLimits.SafeDefaults, CelEvaluationLimits.SafeDefaults, BuildBoundExpression());

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
            CelProfile.V1, schema, BuildKey("x"), CelCompilationLimits.SafeDefaults, CelEvaluationLimits.SafeDefaults, BuildBoundExpression());

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
            CelProfile.V1, schema, BuildKey("x"), CelCompilationLimits.SafeDefaults, CelEvaluationLimits.SafeDefaults, BuildBoundExpression());

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
            CelProfile.V1, schema, BuildKey("x"), CelCompilationLimits.SafeDefaults, ceiling, BuildBoundExpression());

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
            CelProfile.V1, schema, BuildKey("x"), CelCompilationLimits.SafeDefaults, ceiling, BuildBoundExpression());

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
            CelProfile.V1, schema, BuildKey("x"), CelCompilationLimits.SafeDefaults, CelEvaluationLimits.SafeDefaults, BuildBoundExpression());

        var tighter = new CelEvaluationLimits(maxIterations: 1, maxCostUnits: 1);
        Assert.That(() => pred.Evaluate(ctx, tighter), Throws.TypeOf<NotImplementedException>());
    }

    // ── CelCompiledExpression — internal constructor and stub Evaluate ────────

    [Test]
    public void CelCompiledExpression_InternalConstructor_SetsAllProperties()
    {
        var schema = BuildSimpleSchema();
        var key = BuildKey("x + 1");
        var expr = new CelCompiledExpression(CelProfile.V1, schema, key, CelCompilationLimits.SafeDefaults, CelEvaluationLimits.SafeDefaults, BuildBoundExpression());

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
            CelProfile.V1, schema, BuildKey("x"), CelCompilationLimits.SafeDefaults, CelEvaluationLimits.SafeDefaults, BuildBoundExpression());

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
            CelProfile.V1, schema, BuildKey("x"), CelCompilationLimits.SafeDefaults, CelEvaluationLimits.SafeDefaults, BuildBoundExpression());

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
        Assert.That(result.Diagnostics[0].Parameters["profileId"], Is.EqualTo("arch-linter/cel/v1"));
    }

    [Test]
    public void CelCompilationResult_ValidLiteralPredicate_SucceedsWithNoDiagnostics()
    {
        var schemaBuilder = CelContextSchema.CreateBuilder("s");
        schemaBuilder.AddVariable("x", CelType.String);
        var env = CelEnvironment.CreateBuilder(CelProfile.V1)
            .WithContextSchema(schemaBuilder.Build())
            .Build();

        var result = env.CompilePredicate("true");

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Program, Is.Not.Null);
        Assert.That(result.Diagnostics, Has.Count.EqualTo(0));
    }

    [Test]
    public void CelCompilationResult_BinderDiagnostic_CarriesProfileIdParameter()
    {
        var schemaBuilder = CelContextSchema.CreateBuilder("s");
        schemaBuilder.AddVariable("x", CelType.String);
        var env = CelEnvironment.CreateBuilder(CelProfile.V1)
            .WithContextSchema(schemaBuilder.Build())
            .Build();

        var result = env.CompilePredicate("y == 'z'");

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.Diagnostics[0].Code, Is.EqualTo(CelDiagnosticCode.BindingError));
        Assert.That(result.Diagnostics[0].Category, Is.EqualTo("binder"));
        Assert.That(result.Diagnostics[0].Parameters["profileId"], Is.EqualTo("arch-linter/cel/v1"));
        Assert.That(result.Diagnostics[0].Parameters["identifier"], Is.EqualTo("y"));
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

    private static CelBoundExpression BuildBoundExpression() =>
        new(new CelBoundBoolLiteral(new CelSourceSpan(0, 4), true));
}
