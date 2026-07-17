using ArchLinterNet.CEL.Compilation;
using ArchLinterNet.CEL.Evaluation;
using ArchLinterNet.CEL.Profile;
using ArchLinterNet.CEL.Schema;
using ArchLinterNet.CEL.Values;

namespace ArchLinterNet.CEL.Benchmarks.Fixtures;

/// <summary>
/// Shared schema, environment, and expression fixtures used across benchmark classes. Mirrors the
/// source/target assembly-descriptor scenario documented as the representative Core workload in
/// <c>CelExternalConsumerSampleTests</c> so benchmark results reflect real usage rather than
/// synthetic worst/best cases.
/// </summary>
internal static class BenchmarkFixtures
{
    /// <summary>A short predicate exercising member access, equality, and a built-in call.</summary>
    public const string RepresentativePredicateSource =
        "source.role == 'service' && target.namespace.startsWith('Example.')";

    /// <summary>A longer predicate exercising more operators and both objects' members.</summary>
    public const string ComplexPredicateSource =
        "source.role == 'service' && target.namespace.startsWith('Example.') && " +
        "source.name.contains('Api') && target.name != source.name && " +
        "source.tags.size() > 0 && source.metadata.containsKey('owner')";

    /// <summary>A predicate that fails to bind (undeclared member) — for diagnostic-overhead benchmarks.</summary>
    public const string UnboundMemberPredicateSource = "source.doesNotExist == 'x'";

    public static CelObjectSchema BuildAssemblyObjectSchema()
    {
        var builder = CelObjectSchema.CreateBuilder("assembly");
        builder.AddMember("role", CelType.String);
        builder.AddMember("namespace", CelType.String);
        builder.AddMember("name", CelType.String);
        builder.AddMember("tags", CelType.ListOf(CelType.String));
        builder.AddMember("metadata", CelType.MapOf(CelType.String));
        return builder.Build();
    }

    public static CelContextSchema BuildSourceTargetSchema(
        out CelVariable source,
        out CelVariable target)
    {
        var builder = CelContextSchema.CreateBuilder("assembly-predicate-v1");
        source = builder.AddVariable("source", CelType.ObjectOf("assembly"));
        target = builder.AddVariable("target", CelType.ObjectOf("assembly"));
        return builder.Build();
    }

    public static CelEnvironment BuildEnvironment()
    {
        var schema = BuildSourceTargetSchema(out _, out _);
        var objectSchema = BuildAssemblyObjectSchema();
        return CelEnvironment.CreateBuilder(CelProfile.V1)
            .WithContextSchema(schema)
            .WithObjectSchema(objectSchema)
            .Build();
    }

    public static CelObjectValue BuildAssemblyObject(string role, string @namespace, string name, bool serviceLike)
    {
        var members = new Dictionary<string, CelValue>
        {
            ["role"] = CelValue.String(role),
            ["namespace"] = CelValue.String(@namespace),
            ["name"] = CelValue.String(name),
            ["tags"] = CelValue.List(serviceLike
                ? [CelValue.String("http"), CelValue.String("public")]
                : []),
            ["metadata"] = CelValue.Map(serviceLike
                ? new Dictionary<string, CelValue> { ["owner"] = CelValue.String("platform-team") }
                : new Dictionary<string, CelValue>()),
        };
        return new CelObjectValue("assembly", members);
    }

    /// <summary>
    /// Builds a matching source/target evaluation context using stable variable handles — the
    /// fast path recommended for high-volume evaluation loops.
    /// </summary>
    public static CelEvaluationContext BuildMatchingContext(CelEnvironment environment, CelVariable source, CelVariable target) =>
        environment.CreateEvaluationContextBuilder()
            .Set(source, CelValue.Object(BuildAssemblyObject("service", "Example.Services", "Example.Services.Api", serviceLike: true)))
            .Set(target, CelValue.Object(BuildAssemblyObject("domain", "Example.Domain", "Example.Domain.Model", serviceLike: false)))
            .Build();

    /// <summary>
    /// Builds the same context using the ergonomic name-based <c>Set(string, CelValue)</c>
    /// overload, for comparison against <see cref="BuildMatchingContext"/>.
    /// </summary>
    public static CelEvaluationContext BuildMatchingContextByName(CelEnvironment environment) =>
        environment.CreateEvaluationContextBuilder()
            .Set("source", CelValue.Object(BuildAssemblyObject("service", "Example.Services", "Example.Services.Api", serviceLike: true)))
            .Set("target", CelValue.Object(BuildAssemblyObject("domain", "Example.Domain", "Example.Domain.Model", serviceLike: false)))
            .Build();

    /// <summary>A context built against a structurally different (mismatched) schema, for schema-rejection benchmarks.</summary>
    public static (CelEnvironment Environment, CelEvaluationContext Context) BuildMismatchedSchemaContext()
    {
        var otherSchemaBuilder = CelContextSchema.CreateBuilder("other-schema-v1");
        var onlySource = otherSchemaBuilder.AddVariable("source", CelType.ObjectOf("assembly"));
        var otherSchema = otherSchemaBuilder.Build();
        var otherEnvironment = CelEnvironment.CreateBuilder(CelProfile.V1)
            .WithContextSchema(otherSchema)
            .WithObjectSchema(BuildAssemblyObjectSchema())
            .Build();
        var context = otherEnvironment.CreateEvaluationContextBuilder()
            .Set(onlySource, CelValue.Object(BuildAssemblyObject("service", "Example.Services", "Example.Services.Api", serviceLike: true)))
            .Build();
        return (otherEnvironment, context);
    }
}
