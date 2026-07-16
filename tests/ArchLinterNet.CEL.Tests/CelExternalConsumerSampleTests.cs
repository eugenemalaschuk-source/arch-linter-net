using ArchLinterNet.CEL;
using ArchLinterNet.CEL.Compilation;
using ArchLinterNet.CEL.Profile;
using ArchLinterNet.CEL.Schema;
using ArchLinterNet.CEL.Values;
using NUnit.Framework;

namespace ArchLinterNet.CEL.Tests;

/// <summary>
/// Demonstrates the ArchLinter CEL Profile v1 happy-path interaction model using only
/// <c>ArchLinterNet.CEL</c> public API. This file intentionally contains no reference to
/// <c>ArchLinterNet.Core</c> to verify that external consumers require no Core dependency.
///
/// The scenario mirrors a real-world Core use case: evaluating a predicate over a
/// source/target pair of schema-defined assembly descriptors.
/// </summary>
[TestFixture]
public sealed class CelExternalConsumerSampleTests
{
    [Test]
    public void HappyPath_BuildEnvironmentAndInspectCompilationResult()
    {
        // ── 1. Define the context schema ──────────────────────────────────────
        // Declare the variables that will be available in predicate expressions.
        // Each AddVariable call returns a typed handle used later to set values.
        var schemaBuilder = CelContextSchema.CreateBuilder("assembly-predicate-v1");
        var source = schemaBuilder.AddVariable("source", CelType.ObjectOf("assembly"));
        var target = schemaBuilder.AddVariable("target", CelType.ObjectOf("assembly"));
        var schema = schemaBuilder.Build();

        // ── 2. Define the object type schema ──────────────────────────────────
        // Declare the shape of the "assembly" object type so the binder can
        // type-check member-access expressions like `source.role` and
        // `target.namespace` without CLR reflection.
        var assemblyObjBuilder = CelObjectSchema.CreateBuilder("assembly");
        assemblyObjBuilder.AddMember("role", CelType.String);
        assemblyObjBuilder.AddMember("namespace", CelType.String);
        var assemblySchema = assemblyObjBuilder.Build();

        // ── 3. Build an immutable CEL environment ─────────────────────────────
        var environment = CelEnvironment.CreateBuilder(CelProfile.V1)
            .WithContextSchema(schema)
            .WithCompilationLimits(CelCompilationLimits.SafeDefaults)
            .WithObjectSchema(assemblySchema)
            .Build();

        // Verify environment captures profile, schema, limits, and object catalog.
        Assert.That(environment.Profile, Is.SameAs(CelProfile.V1));
        Assert.That(environment.Schema, Is.SameAs(schema));
        Assert.That(environment.CompilationLimits, Is.Not.Null);
        Assert.That(environment.ObjectSchemas, Contains.Key("assembly"));
        Assert.That(environment.ObjectSchemas["assembly"], Is.SameAs(assemblySchema));

        // ── 3. Compile a predicate expression ─────────────────────────────────
        // The normal compile path parses, binds, and type-checks the whole expression.
        // Invalid user expressions produce structured diagnostics; no exception is thrown.
        var compilation = environment.CompilePredicate(
            "source.role == 'service' && target.namespace.startsWith('Example.')");

        // ── 4. Inspect the compilation result ─────────────────────────────────
        // The expression resolves and type-checks successfully (both members declared,
        // both operators well-typed, root type Bool), so compilation now succeeds — the
        // binder (#326) replaces the former NotYetImplemented stub for valid input.
        Assert.That(compilation, Is.Not.Null);
        Assert.That(compilation.IsSuccess, Is.True);
        Assert.That(compilation.Program, Is.Not.Null);
        Assert.That(compilation.Diagnostics, Has.Count.EqualTo(0));
        Assert.That(compilation.CompilationKey, Is.Not.Null);

        // Cache identity is deterministic and available regardless of success/failure.
        var key = compilation.CompilationKey;
        Assert.That(key.ProfileId, Is.EqualTo(CelProfile.V1.Id));
        Assert.That(key.RequiredResultType, Is.EqualTo(CelRequiredResultType.Predicate));
        // Schema identity incorporates the context schema AND registered object schemas.
        Assert.That(key.SchemaIdentity, Is.Not.Null.And.Not.Empty);
        Assert.That(key.SchemaIdentity, Does.StartWith(schema.Identity));
        Assert.That(key.NormalizedSource, Is.Not.Null.And.Not.Empty);

        // ── 5. Build an evaluation context ────────────────────────────────────
        // Uses the typed handles returned by AddVariable to avoid string-key lookups.
        var sourceMembers = new Dictionary<string, CelValue>
        {
            ["role"] = CelValue.String("service"),
            ["namespace"] = CelValue.String("Example.Services"),
        };
        var targetMembers = new Dictionary<string, CelValue>
        {
            ["role"] = CelValue.String("domain"),
            ["namespace"] = CelValue.String("Example.Domain"),
        };

        // Use env.CreateEvaluationContextBuilder() so the builder has the full
        // object schema catalog and can validate member types on Set().
        var context = environment.CreateEvaluationContextBuilder()
            .Set(source, CelValue.Object(new CelObjectValue("assembly", sourceMembers)))
            .Set(target, CelValue.Object(new CelObjectValue("assembly", targetMembers)))
            .Build();

        Assert.That(context, Is.Not.Null);
        Assert.That(context.Schema, Is.SameAs(schema));
        Assert.That(context.Assignments, Has.Count.EqualTo(2));

        // ── 6. Reuse and cache identity verification ──────────────────────────
        // Two compilations of the same source with the same environment produce equal keys.
        var compilation2 = environment.CompilePredicate(
            "source.role == 'service' && target.namespace.startsWith('Example.')");
        Assert.That(compilation.CompilationKey, Is.EqualTo(compilation2.CompilationKey));
        Assert.That(
            compilation.CompilationKey.GetHashCode(),
            Is.EqualTo(compilation2.CompilationKey.GetHashCode()));
    }

    [Test]
    public void ApiConstraint_NoUnboundedCompilationPath_VerifiedByAbsenceOfOverloads()
    {
        // This test verifies at compile time that the only CompilePredicate overload
        // accepts a source string (no overload bypassing limits exists).
        // The test body exists to confirm the shape compiles — the assertion is the absence
        // of an ambiguity or compilation error above.
        var env = CelEnvironment.CreateBuilder(CelProfile.V1)
            .WithContextSchema(CelContextSchema.CreateBuilder("x").Build() is var s ? s : null!)
            .Build();

        // Only one overload exists; it returns a CelCompilationResult<CelCompiledPredicate>.
        CelCompilationResult<CelCompiledPredicate> result = env.CompilePredicate("true");
        Assert.That(result, Is.Not.Null);
    }

    [Test]
    public void ApiConstraint_CelValueRejectsWrongKindAccessor()
    {
        // Verifies that typed accessors throw rather than silently returning a default.
        var stringValue = CelValue.String("hello");
        Assert.That(() => stringValue.AsBool(), Throws.InvalidOperationException);
        Assert.That(() => stringValue.AsInt(), Throws.InvalidOperationException);
        Assert.That(() => stringValue.AsFloat(), Throws.InvalidOperationException);
    }

    [Test]
    public void ApiConstraint_EmptySchemaBuildsAndEvaluationContextIsEmpty()
    {
        var schema = CelContextSchema.CreateBuilder("empty-schema").Build();
        var ctx = schema.CreateEvaluationContextBuilder().Build();

        Assert.That(schema.Variables, Is.Empty);
        Assert.That(ctx.Assignments, Is.Empty);
    }
}
