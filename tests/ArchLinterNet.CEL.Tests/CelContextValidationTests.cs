using ArchLinterNet.CEL;
using ArchLinterNet.CEL.Profile;
using ArchLinterNet.CEL.Schema;
using ArchLinterNet.CEL.Values;
using NUnit.Framework;

namespace ArchLinterNet.CEL.Tests;

/// <summary>
/// Regression tests for the evaluation-context input-validation surface: structural depth and
/// collection-size limits, strict object-value validation, name-based Set(), CEL identifier
/// rules for schema-declared names, and CelValue.String Unicode well-formedness. Split out of
/// <c>CelInternalApiCoverageTests</c> to keep both files under the repository size threshold.
/// </summary>
[TestFixture]
public sealed class CelContextValidationTests
{
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

    // ── Cumulative node budget: per-collection caps alone do not bound total ──
    //    validation work (a wide-but-shallow structure can stay under
    //    MaxValidationCollectionSize at every level while still visiting far
    //    more nodes than MaxValidationNodeCount permits) ──────────────────────

    private static CelValue BuildNestedIntLists(int outerCount, int innerCount) =>
        CelValue.List(Enumerable.Range(0, outerCount)
            .Select(_ => CelValue.List(Enumerable.Range(0, innerCount).Select(i => CelValue.Int(i)).ToList()))
            .ToList());

    [Test]
    public void CelEvaluationContextBuilder_WideShallowStructure_ExceedsCumulativeNodeBudget_FailsValidation()
    {
        // 100 outer elements x 100 inner ints = 1 + 100*(1 + 100) = 10,101 visited nodes,
        // exceeding MaxValidationNodeCount (10,000), while every individual list (100 elements)
        // stays far below MaxValidationCollectionSize (1024) and nesting depth (2) stays far
        // below MaxValidationDepth (16) — isolating the cumulative node budget as the only
        // possible rejection reason.
        var value = BuildNestedIntLists(outerCount: 100, innerCount: 100);

        var schemaBuilder = CelContextSchema.CreateBuilder("ctx");
        var handle = schemaBuilder.AddVariable("v", CelType.ListOf(CelType.ListOf(CelType.Int)));
        var schema = schemaBuilder.Build();

        Assert.That(
            () => schema.CreateEvaluationContextBuilder().Set(handle, value),
            Throws.ArgumentException,
            "Set() must reject values whose total visited node count exceeds the cumulative " +
            "validation node budget, even when every individual collection is within the " +
            "per-collection size limit.");
    }

    [Test]
    public void CelEvaluationContextBuilder_WideShallowStructure_JustUnderCumulativeNodeBudget_PassesValidation()
    {
        // 100 outer elements x 98 inner ints = 1 + 100*(1 + 98) = 9,901 visited nodes, within
        // MaxValidationNodeCount (10,000), proving the previous test fails on the cumulative
        // budget alone rather than on collection size, depth, or an ordinary type mismatch.
        var value = BuildNestedIntLists(outerCount: 100, innerCount: 98);

        var schemaBuilder = CelContextSchema.CreateBuilder("ctx");
        var handle = schemaBuilder.AddVariable("v", CelType.ListOf(CelType.ListOf(CelType.Int)));
        var schema = schemaBuilder.Build();

        Assert.That(
            () => schema.CreateEvaluationContextBuilder().Set(handle, value),
            Throws.Nothing);
    }

    // ── CelValue.String: Unicode well-formedness (CEL strings = code points) ──

    [Test]
    public void CelValue_String_UnpairedHighSurrogate_ThrowsArgumentException()
    {
        Assert.That(() => CelValue.String("\ud83d"), Throws.ArgumentException);
        Assert.That(() => CelValue.String("a\ud83db"), Throws.ArgumentException);
    }

    [Test]
    public void CelValue_String_UnpairedLowSurrogate_ThrowsArgumentException()
    {
        Assert.That(() => CelValue.String("\ude00"), Throws.ArgumentException);
        Assert.That(() => CelValue.String("x\ude00"), Throws.ArgumentException);
    }

    [Test]
    public void CelValue_String_WellFormedSurrogatePair_IsAccepted()
    {
        // 😀 U+1F600 — one code point, two UTF-16 units, valid pair.
        var v = CelValue.String("😀");
        Assert.That(v.AsString(), Is.EqualTo("😀"));
    }

    [Test]
    public void CelValue_String_CombiningSequence_IsAccepted()
    {
        // "e" + COMBINING ACUTE ACCENT (U+0301) - two code points, both valid UTF-16.
        var v = CelValue.String("é");
        Assert.That(v.AsString(), Is.EqualTo("é"));
        Assert.That(v.AsString().Length, Is.EqualTo(2));
    }

    // ── Aggregate value factories reject null and malformed structural input ─
    //    (Profile v1 defines no null CEL value; map keys are CEL strings and
    //    are therefore held to the same UTF-16 well-formedness rule as
    //    CelValue.String)

    [Test]
    public void CelValue_List_NullElement_ThrowsArgumentException()
    {
        Assert.That(
            () => CelValue.List(new CelValue[] { null! }),
            Throws.ArgumentException);
    }

    [Test]
    public void CelValue_Map_NullValue_ThrowsArgumentException()
    {
        Assert.That(
            () => CelValue.Map(new Dictionary<string, CelValue> { ["key"] = null! }),
            Throws.ArgumentException);
    }

    [Test]
    public void CelValue_Map_MalformedUtf16Key_ThrowsArgumentException()
    {
        Assert.That(
            () => CelValue.Map(new Dictionary<string, CelValue> { ["\ud83d"] = CelValue.Int(1) }),
            Throws.ArgumentException);
    }

    [Test]
    public void CelObjectValue_NullMemberValue_ThrowsArgumentException()
    {
        Assert.That(
            () => new CelObjectValue("thing", new Dictionary<string, CelValue> { ["member"] = null! }),
            Throws.ArgumentException);
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
    public void CelContextSchemaBuilder_KeywordVariableName_ThrowsArgumentException()
    {
        // CEL keywords can never be identifiers: IDENT = SELECTOR - RESERVED, SELECTOR excludes keywords.
        var builder = CelContextSchema.CreateBuilder("s");
        Assert.That(() => builder.AddVariable("true", CelType.Bool), Throws.ArgumentException);
        Assert.That(() => builder.AddVariable("false", CelType.Bool), Throws.ArgumentException);
        Assert.That(() => builder.AddVariable("null", CelType.String), Throws.ArgumentException);
        Assert.That(() => builder.AddVariable("in", CelType.String), Throws.ArgumentException);
    }

    [Test]
    public void CelContextSchemaBuilder_ReservedWordVariableName_ThrowsArgumentException()
    {
        // CEL reserved identifiers are invalid as plain identifiers (variables).
        var builder = CelContextSchema.CreateBuilder("s");
        Assert.That(() => builder.AddVariable("as", CelType.String), Throws.ArgumentException);
        Assert.That(() => builder.AddVariable("namespace", CelType.String), Throws.ArgumentException);
        Assert.That(() => builder.AddVariable("var", CelType.String), Throws.ArgumentException);
        Assert.That(() => builder.AddVariable("_source1", CelType.String), Throws.Nothing);
    }

    [Test]
    public void CelObjectSchemaBuilder_NonIdentifierMemberName_ThrowsArgumentException()
    {
        var builder = CelObjectSchema.CreateBuilder("t");
        Assert.That(() => builder.AddMember("my member", CelType.String), Throws.ArgumentException);
        Assert.That(() => builder.AddMember("9lives", CelType.Int), Throws.ArgumentException);
        Assert.That(() => builder.AddMember("valid_Name2", CelType.Bool), Throws.Nothing);
    }

    [Test]
    public void CelObjectSchemaBuilder_KeywordMemberName_ThrowsArgumentException()
    {
        // Keywords are invalid even in selector position.
        var builder = CelObjectSchema.CreateBuilder("t");
        Assert.That(() => builder.AddMember("true", CelType.Bool), Throws.ArgumentException);
        Assert.That(() => builder.AddMember("in", CelType.String), Throws.ArgumentException);
        Assert.That(() => builder.AddMember("null", CelType.String), Throws.ArgumentException);
    }

    [Test]
    public void CelObjectSchemaBuilder_ReservedWordMemberName_IsAccepted()
    {
        // Reserved identifiers ARE valid selectors per the CEL grammar (SELECTOR excludes only
        // keywords), so members named "as"/"namespace" are legal member-access targets.
        var builder = CelObjectSchema.CreateBuilder("t");
        Assert.That(() => builder.AddMember("as", CelType.String), Throws.Nothing);
        Assert.That(() => builder.AddMember("namespace", CelType.String), Throws.Nothing);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static CelContextSchema BuildSimpleSchema()
    {
        var builder = CelContextSchema.CreateBuilder("val-v1");
        builder.AddVariable("x", CelType.String);
        return builder.Build();
    }
}
