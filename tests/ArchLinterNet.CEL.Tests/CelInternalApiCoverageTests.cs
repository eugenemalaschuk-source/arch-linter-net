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
