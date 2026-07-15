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
            compilationLimitsIdentity: "len=4096,nest=32,ids=64");

        Assert.That(key.NormalizedSource, Is.EqualTo("x == 1"));
        Assert.That(key.ProfileId, Is.EqualTo(CelProfile.V1.Id));
        Assert.That(key.SchemaIdentity, Is.EqualTo("schema-id"));
        Assert.That(key.RequiredResultType, Is.EqualTo(CelRequiredResultType.Predicate));
        Assert.That(key.CompilationLimitsIdentity, Is.EqualTo("len=4096,nest=32,ids=64"));
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
        var pred = new CelCompiledPredicate(CelProfile.V1, schema, key, CelCompilationLimits.SafeDefaults);

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
            CelProfile.V1, schema, BuildKey("x"), CelCompilationLimits.SafeDefaults);

        Assert.That(
            () => pred.Evaluate(ctx, CelEvaluationLimits.SafeDefaults),
            Throws.TypeOf<NotImplementedException>());
    }

    // ── CelCompiledExpression — internal constructor and stub Evaluate ────────

    [Test]
    public void CelCompiledExpression_InternalConstructor_SetsAllProperties()
    {
        var schema = BuildSimpleSchema();
        var key = BuildKey("x + 1");
        var expr = new CelCompiledExpression(CelProfile.V1, schema, key, CelCompilationLimits.SafeDefaults);

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
            CelProfile.V1, schema, BuildKey("x"), CelCompilationLimits.SafeDefaults);

        Assert.That(
            () => expr.Evaluate(ctx, CelEvaluationLimits.SafeDefaults),
            Throws.TypeOf<NotImplementedException>());
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

    // ── Depth limit prevents stack overflow via public Set() ─────────────────

    [Test]
    public void CelEvaluationContextBuilder_DeeplyNestedList_FailsValidation()
    {
        // Build a list nested 20 levels deep — exceeds MaxValidationDepth (16).
        var inner = CelValue.List([CelValue.Int(1)]);
        for (var i = 0; i < 20; i++)
            inner = CelValue.List([inner]);

        var schemaBuilder = CelContextSchema.CreateBuilder("ctx");
        var handle = schemaBuilder.AddVariable("v",
            CelType.ListOf(CelType.ListOf(CelType.ListOf(CelType.Int))));
        var schema = schemaBuilder.Build();

        Assert.That(
            () => schema.CreateEvaluationContextBuilder().Set(handle, inner),
            Throws.ArgumentException,
            "Set() must reject values that exceed the maximum structural validation depth.");
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
            compilationLimitsIdentity: CelCompilationLimits.SafeDefaults.ComputeIdentity());
}
