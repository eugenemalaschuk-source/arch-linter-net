using ArchLinterNet.CEL;
using NUnit.Framework;

namespace ArchLinterNet.CEL.Tests;

/// <summary>
/// Compile-and-pass tests that verify the ArchLinter CEL Profile v1 public API surface.
/// These tests do not exercise runtime language behavior (parser, type-checker, or evaluator)
/// — they verify that the types are correctly shaped, builders chain correctly, and result
/// objects expose the documented properties.
///
/// This file intentionally contains no 'using ArchLinterNet.Core' directive.
/// </summary>
[TestFixture]
public sealed class CelApiShapeTests
{
    [Test]
    public void CelProfile_V1_IsNonNull()
    {
        Assert.That(CelProfile.V1, Is.Not.Null);
    }

    [Test]
    public void CelProfile_V1_HasExpectedId()
    {
        Assert.That(CelProfile.V1.Id.Value, Is.EqualTo("arch-linter/cel/v1"));
    }

    [Test]
    public void CelProfileId_Equality_ByValue()
    {
        var a = new CelProfileId("arch-linter/cel/v1");
        var b = new CelProfileId("arch-linter/cel/v1");
        Assert.That(a, Is.EqualTo(b));
        Assert.That(a.GetHashCode(), Is.EqualTo(b.GetHashCode()));
    }

    [Test]
    public void CelType_PrimitiveConstants_AreNonNull()
    {
        Assert.That(CelType.Bool, Is.Not.Null);
        Assert.That(CelType.String, Is.Not.Null);
        Assert.That(CelType.Int, Is.Not.Null);
        Assert.That(CelType.Float, Is.Not.Null);
    }

    [Test]
    public void CelType_PrimitiveConstants_HaveCorrectKinds()
    {
        Assert.That(CelType.Bool.Kind, Is.EqualTo(CelTypeKind.Bool));
        Assert.That(CelType.String.Kind, Is.EqualTo(CelTypeKind.String));
        Assert.That(CelType.Int.Kind, Is.EqualTo(CelTypeKind.Int));
        Assert.That(CelType.Float.Kind, Is.EqualTo(CelTypeKind.Float));
    }

    [Test]
    public void CelType_ListOf_ProducesListKindWithElementType()
    {
        var listType = CelType.ListOf(CelType.String);
        Assert.That(listType.Kind, Is.EqualTo(CelTypeKind.List));
        Assert.That(listType.ElementType, Is.SameAs(CelType.String));
    }

    [Test]
    public void CelType_MapOf_ProducesMapKindWithValueType()
    {
        var mapType = CelType.MapOf(CelType.Bool);
        Assert.That(mapType.Kind, Is.EqualTo(CelTypeKind.Map));
        Assert.That(mapType.ValueType, Is.SameAs(CelType.Bool));
    }

    [Test]
    public void CelType_ObjectOf_ProducesObjectKindWithSchemaId()
    {
        var objType = CelType.ObjectOf("assembly");
        Assert.That(objType.Kind, Is.EqualTo(CelTypeKind.Object));
        Assert.That(objType.SchemaId, Is.EqualTo("assembly"));
    }

    [Test]
    public void CelContextSchema_CreateBuilder_ReturnsBuilder()
    {
        var builder = CelContextSchema.CreateBuilder("test-schema-v1");
        Assert.That(builder, Is.Not.Null);
    }

    [Test]
    public void CelContextSchemaBuilder_AddVariable_ReturnsHandle()
    {
        var builder = CelContextSchema.CreateBuilder("test-schema-v1");
        var variable = builder.AddVariable("source", CelType.String);
        Assert.That(variable, Is.Not.Null);
        Assert.That(variable.Name, Is.EqualTo("source"));
        Assert.That(variable.Type, Is.SameAs(CelType.String));
    }

    [Test]
    public void CelContextSchemaBuilder_Build_ReturnsImmutableSchema()
    {
        var builder = CelContextSchema.CreateBuilder("test-schema-v1");
        builder.AddVariable("x", CelType.Int);
        var schema = builder.Build();
        Assert.That(schema, Is.Not.Null);
        Assert.That(schema.SchemaId, Is.EqualTo("test-schema-v1"));
        Assert.That(schema.Variables, Has.Count.EqualTo(1));
        Assert.That(schema.Identity, Is.Not.Null.And.Not.Empty);
    }

    [Test]
    public void CelContextSchemaBuilder_DuplicateVariableName_ThrowsArgumentException()
    {
        var builder = CelContextSchema.CreateBuilder("test-schema-v1");
        builder.AddVariable("x", CelType.Int);
        Assert.That(() => builder.AddVariable("x", CelType.String), Throws.ArgumentException);
    }

    [Test]
    public void CelContextSchema_TwoIdenticalSchemas_HaveSameIdentity()
    {
        var builderA = CelContextSchema.CreateBuilder("s");
        builderA.AddVariable("a", CelType.Bool);
        var a = builderA.Build();

        var builderB = CelContextSchema.CreateBuilder("s");
        builderB.AddVariable("a", CelType.Bool);
        var b = builderB.Build();

        Assert.That(a.Identity, Is.EqualTo(b.Identity));
    }

    [Test]
    public void CelEnvironment_CreateBuilder_ReturnsBuilder()
    {
        var envBuilder = CelEnvironment.CreateBuilder(CelProfile.V1);
        Assert.That(envBuilder, Is.Not.Null);
    }

    [Test]
    public void CelEnvironmentBuilder_WithContextSchemaAndBuild_ReturnsEnvironment()
    {
        var schema = BuildSimpleSchema();
        var env = CelEnvironment.CreateBuilder(CelProfile.V1)
            .WithContextSchema(schema)
            .WithCompilationLimits(CelCompilationLimits.SafeDefaults)
            .Build();

        Assert.That(env, Is.Not.Null);
        Assert.That(env.Profile, Is.SameAs(CelProfile.V1));
        Assert.That(env.Schema, Is.SameAs(schema));
        Assert.That(env.CompilationLimits, Is.Not.Null);
    }

    [Test]
    public void CelEnvironment_CompilePredicate_ReturnsCompilationResult()
    {
        var env = BuildSimpleEnvironment();
        var result = env.CompilePredicate("source == 'hello'");
        Assert.That(result, Is.Not.Null);
    }

    [Test]
    public void CelCompilationResult_HasExpectedProperties()
    {
        var env = BuildSimpleEnvironment();
        var result = env.CompilePredicate("source == 'hello'");

        Assert.That(result.IsSuccess, Is.TypeOf<bool>());
        Assert.That(result.Diagnostics, Is.Not.Null);
        Assert.That(result.CompilationKey, Is.Not.Null);
    }

    [Test]
    public void CelCompilationResult_StubReturnsNotYetImplementedDiagnostic()
    {
        var env = BuildSimpleEnvironment();
        var result = env.CompilePredicate("source == 'hello'");

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.Diagnostics, Has.Count.GreaterThan(0));
        Assert.That(result.Diagnostics[0].Code, Is.EqualTo(CelDiagnosticCode.NotYetImplemented));
    }

    [Test]
    public void CelCompilationKey_IsNonNullAndHasExpectedProperties()
    {
        var env = BuildSimpleEnvironment();
        var result = env.CompilePredicate("source == 'hello'");
        var key = result.CompilationKey;

        Assert.That(key.NormalizedSource, Is.Not.Null.And.Not.Empty);
        Assert.That(key.ProfileId, Is.EqualTo(CelProfile.V1.Id));
        Assert.That(key.SchemaIdentity, Is.Not.Null.And.Not.Empty);
        Assert.That(key.RequiredResultType, Is.EqualTo(CelRequiredResultType.Predicate));
    }

    [Test]
    public void CelCompilationLimits_SafeDefaults_IsNonNull()
    {
        Assert.That(CelCompilationLimits.SafeDefaults, Is.Not.Null);
        Assert.That(CelCompilationLimits.SafeDefaults.MaxExpressionLength, Is.GreaterThan(0));
        Assert.That(CelCompilationLimits.SafeDefaults.MaxNestingDepth, Is.GreaterThan(0));
        Assert.That(CelCompilationLimits.SafeDefaults.MaxIdentifierCount, Is.GreaterThan(0));
    }

    [Test]
    public void CelEvaluationLimits_SafeDefaults_IsNonNull()
    {
        Assert.That(CelEvaluationLimits.SafeDefaults, Is.Not.Null);
        Assert.That(CelEvaluationLimits.SafeDefaults.MaxIterations, Is.GreaterThan(0));
        Assert.That(CelEvaluationLimits.SafeDefaults.MaxCostUnits, Is.GreaterThan(0));
    }

    [Test]
    public void CelValue_BoolFactory_ProducesCorrectKind()
    {
        var v = CelValue.Bool(true);
        Assert.That(v.Kind, Is.EqualTo(CelValueKind.Bool));
        Assert.That(v.AsBool(), Is.True);
    }

    [Test]
    public void CelValue_StringFactory_ProducesCorrectKind()
    {
        var v = CelValue.String("hello");
        Assert.That(v.Kind, Is.EqualTo(CelValueKind.String));
        Assert.That(v.AsString(), Is.EqualTo("hello"));
    }

    [Test]
    public void CelValue_IntFactory_ProducesCorrectKind()
    {
        var v = CelValue.Int(42L);
        Assert.That(v.Kind, Is.EqualTo(CelValueKind.Int));
        Assert.That(v.AsInt(), Is.EqualTo(42L));
    }

    [Test]
    public void CelValue_FloatFactory_ProducesCorrectKind()
    {
        var v = CelValue.Float(3.14);
        Assert.That(v.Kind, Is.EqualTo(CelValueKind.Float));
        Assert.That(v.AsFloat(), Is.EqualTo(3.14).Within(1e-9));
    }

    [Test]
    public void CelValue_WrongKindAccessor_ThrowsInvalidOperationException()
    {
        var v = CelValue.Bool(true);
        Assert.That(() => v.AsString(), Throws.InvalidOperationException);
    }

    [Test]
    public void CelValue_ListFactory_ProducesCorrectKind()
    {
        var v = CelValue.List([CelValue.Int(1), CelValue.Int(2)]);
        Assert.That(v.Kind, Is.EqualTo(CelValueKind.List));
        Assert.That(v.AsList(), Has.Count.EqualTo(2));
    }

    [Test]
    public void CelValue_MapFactory_ProducesCorrectKind()
    {
        var v = CelValue.Map(new Dictionary<string, CelValue> { ["key"] = CelValue.String("val") });
        Assert.That(v.Kind, Is.EqualTo(CelValueKind.Map));
        Assert.That(v.AsMap(), Contains.Key("key"));
    }

    [Test]
    public void CelDiagnostic_HasStableCode()
    {
        var env = BuildSimpleEnvironment();
        var result = env.CompilePredicate("x");
        var diag = result.Diagnostics[0];

        Assert.That(diag.Code, Is.EqualTo(CelDiagnosticCode.NotYetImplemented));
        Assert.That(diag.Severity, Is.EqualTo(CelDiagnosticSeverity.Error));
        Assert.That(diag.Category, Is.Not.Null.And.Not.Empty);
        Assert.That(diag.Message, Is.Not.Null.And.Not.Empty);
    }

    [Test]
    public void CelEvaluationContextBuilder_Set_WithCompatibleValue_Succeeds()
    {
        var schemaBuilder = CelContextSchema.CreateBuilder("ctx-v1");
        var handle = schemaBuilder.AddVariable("name", CelType.String);
        var schema = schemaBuilder.Build();

        var ctx = schema.CreateEvaluationContextBuilder()
            .Set(handle, CelValue.String("Alice"))
            .Build();

        Assert.That(ctx, Is.Not.Null);
        Assert.That(ctx.Schema, Is.SameAs(schema));
        Assert.That(ctx.Assignments, Has.Count.EqualTo(1));
    }

    [Test]
    public void CelEvaluationContextBuilder_Set_WithIncompatibleKind_ThrowsArgumentException()
    {
        var schemaBuilder = CelContextSchema.CreateBuilder("ctx-v1");
        var handle = schemaBuilder.AddVariable("count", CelType.Int);
        var schema = schemaBuilder.Build();

        Assert.That(
            () => schema.CreateEvaluationContextBuilder().Set(handle, CelValue.Bool(true)),
            Throws.ArgumentException);
    }

    [Test]
    public void CelEvaluationContextBuilder_Build_WithMissingVariable_ThrowsInvalidOperationException()
    {
        var schemaBuilder = CelContextSchema.CreateBuilder("ctx-v1");
        schemaBuilder.AddVariable("required", CelType.String);
        var schema = schemaBuilder.Build();

        Assert.That(
            () => schema.CreateEvaluationContextBuilder().Build(),
            Throws.InvalidOperationException);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static CelContextSchema BuildSimpleSchema()
    {
        var builder = CelContextSchema.CreateBuilder("test-v1");
        builder.AddVariable("source", CelType.String);
        return builder.Build();
    }

    private static CelEnvironment BuildSimpleEnvironment()
    {
        return CelEnvironment.CreateBuilder(CelProfile.V1)
            .WithContextSchema(BuildSimpleSchema())
            .WithCompilationLimits(CelCompilationLimits.SafeDefaults)
            .Build();
    }
}
