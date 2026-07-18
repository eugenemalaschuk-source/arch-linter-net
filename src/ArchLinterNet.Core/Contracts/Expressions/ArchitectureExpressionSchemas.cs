using ArchLinterNet.CEL;
using ArchLinterNet.CEL.Profile;
using ArchLinterNet.CEL.Schema;

namespace ArchLinterNet.Core.Contracts.Expressions;

// Closed catalog per openspec/specs/cel-policy-model/spec.md: the member lists below are
// exhaustive for the first wave and must not gain/lose/retype a member without a reviewed spec
// change. These CelEnvironment instances are deterministic and stateless (same for every process
// run, like CelProfile.V1 itself) - not the "mutable cache shared across policy sessions" the
// integration story forbids, since no expression-specific data is ever stored on them.
//
// Contracts.Expressions and Execution.Expressions (ArchitectureExpressionContextFactory) must
// share these exact CelContextSchema/CelVariable instances: CelEvaluationContextBuilder.Set
// checks variable handles by reference equality against the schema a predicate was compiled
// against. Execution may depend on Contracts (not the reverse), so this single source of truth
// lives here.
internal static class ArchitectureExpressionSchemas
{
    private const string SubjectObjectTypeId = "ArchLinterNet.Core.Subject";
    private const string DependencyObjectTypeId = "ArchLinterNet.Core.Dependency";

    public static CelObjectSchema SubjectObjectSchema { get; }
    public static CelObjectSchema DependencyObjectSchema { get; }

    public static CelContextSchema SelectorSchema { get; }
    public static CelVariable SelectorSubjectVariable { get; }
    public static CelEnvironment SelectorEnvironment { get; }

    public static CelContextSchema ContextualSourceSchema { get; }
    public static CelVariable ContextualSourceVariable { get; }
    public static CelEnvironment ContextualSourceEnvironment { get; }

    public static CelContextSchema ContextualTargetSchema { get; }
    public static CelVariable ContextualTargetSourceVariable { get; }
    public static CelVariable ContextualTargetTargetVariable { get; }
    public static CelVariable ContextualTargetDependencyVariable { get; }
    public static CelEnvironment ContextualTargetEnvironment { get; }

    static ArchitectureExpressionSchemas()
    {
        SubjectObjectSchema = BuildSubjectObjectSchema();
        DependencyObjectSchema = BuildDependencyObjectSchema();

        CelType subjectType = CelType.ObjectOf(SubjectObjectTypeId);
        CelType dependencyType = CelType.ObjectOf(DependencyObjectTypeId);

        CelContextSchemaBuilder selectorBuilder = CelContextSchema.CreateBuilder("core.selector");
        SelectorSubjectVariable = selectorBuilder.AddVariable("subject", subjectType);
        SelectorSchema = selectorBuilder.Build();
        SelectorEnvironment = BuildEnvironment(SelectorSchema);

        CelContextSchemaBuilder contextualSourceBuilder =
            CelContextSchema.CreateBuilder("core.contextual_source");
        ContextualSourceVariable = contextualSourceBuilder.AddVariable("source", subjectType);
        ContextualSourceSchema = contextualSourceBuilder.Build();
        ContextualSourceEnvironment = BuildEnvironment(ContextualSourceSchema);

        CelContextSchemaBuilder contextualTargetBuilder =
            CelContextSchema.CreateBuilder("core.contextual_target");
        ContextualTargetSourceVariable = contextualTargetBuilder.AddVariable("source", subjectType);
        ContextualTargetTargetVariable = contextualTargetBuilder.AddVariable("target", subjectType);
        ContextualTargetDependencyVariable =
            contextualTargetBuilder.AddVariable("dependency", dependencyType);
        ContextualTargetSchema = contextualTargetBuilder.Build();
        ContextualTargetEnvironment = BuildEnvironment(ContextualTargetSchema);
    }

    private static CelEnvironment BuildEnvironment(CelContextSchema schema) =>
        CelEnvironment.CreateBuilder(CelProfile.V1)
            .WithContextSchema(schema)
            .WithObjectSchema(SubjectObjectSchema)
            .WithObjectSchema(DependencyObjectSchema)
            .Build();

    private static CelObjectSchema BuildSubjectObjectSchema()
    {
        CelObjectSchemaBuilder builder = CelObjectSchema.CreateBuilder(SubjectObjectTypeId);
        builder.AddMember("fullName", CelType.String);
        builder.AddMember("simpleName", CelType.String);
        builder.AddMember("namespace", CelType.String);
        builder.AddMember("assemblyName", CelType.String);
        builder.AddMember("projectName", CelType.String);
        builder.AddMember("role", CelType.String);
        builder.AddMember("metadataText", CelType.MapOf(CelType.String));
        builder.AddMember("metadataBool", CelType.MapOf(CelType.Bool));
        builder.AddMember("kind", CelType.String);
        builder.AddMember("isAbstract", CelType.Bool);
        builder.AddMember("isSealed", CelType.Bool);
        builder.AddMember("baseTypeNames", CelType.ListOf(CelType.String));
        builder.AddMember("interfaceTypeNames", CelType.ListOf(CelType.String));
        builder.AddMember("attributeTypeNames", CelType.ListOf(CelType.String));
        builder.AddMember("sourcePaths", CelType.ListOf(CelType.String));
        builder.AddMember("sourceDirectoryPrefixes", CelType.ListOf(CelType.String));
        return builder.Build();
    }

    private static CelObjectSchema BuildDependencyObjectSchema()
    {
        CelObjectSchemaBuilder builder = CelObjectSchema.CreateBuilder(DependencyObjectTypeId);
        builder.AddMember("kind", CelType.String);
        builder.AddMember("viaMethodBody", CelType.Bool);
        builder.AddMember("sourceMemberName", CelType.String);
        builder.AddMember("targetMemberName", CelType.String);
        return builder.Build();
    }
}
