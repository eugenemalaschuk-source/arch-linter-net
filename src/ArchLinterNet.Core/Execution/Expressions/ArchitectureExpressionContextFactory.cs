using ArchLinterNet.CEL.Evaluation;
using ArchLinterNet.CEL.Values;
using ArchLinterNet.Core.Contracts.Expressions;

namespace ArchLinterNet.Core.Execution.Expressions;

// Typed context factories: pure mapping from Core-owned architecture-fact DTOs into immutable
// ArchLinterNet.CEL values and evaluation contexts, using only the public ArchLinterNet.CEL API
// and the schemas ArchitectureExpressionSchemas declares. No CLR reflection, host object, or
// arbitrary string is ever exposed to CEL - only the documented, closed member lists. Not wired
// into any selector/contract matching yet; that integration is #164's responsibility. See
// openspec/changes/core-cel-integration/design.md Decision D5.
internal static class ArchitectureExpressionContextFactory
{
    public static CelValue CreateSubjectValue(ArchitectureExpressionSubjectFacts facts)
    {
        var members = new Dictionary<string, CelValue>(16, StringComparer.Ordinal)
        {
            ["fullName"] = CelValue.String(facts.FullName),
            ["simpleName"] = CelValue.String(facts.SimpleName),
            ["namespace"] = CelValue.String(facts.Namespace),
            ["assemblyName"] = CelValue.String(facts.AssemblyName),
            ["projectName"] = CelValue.String(facts.ProjectName),
            ["role"] = CelValue.String(facts.Role),
            ["metadataText"] = CelValue.Map(ToValueMap(facts.MetadataText, CelValue.String)),
            ["metadataBool"] = CelValue.Map(ToValueMap(facts.MetadataBool, CelValue.Bool)),
            ["kind"] = CelValue.String(facts.Kind),
            ["isAbstract"] = CelValue.Bool(facts.IsAbstract),
            ["isSealed"] = CelValue.Bool(facts.IsSealed),
            ["baseTypeNames"] = CelValue.List(ToValueList(facts.BaseTypeNames)),
            ["interfaceTypeNames"] = CelValue.List(ToValueList(facts.InterfaceTypeNames)),
            ["attributeTypeNames"] = CelValue.List(ToValueList(facts.AttributeTypeNames)),
            ["sourcePaths"] = CelValue.List(ToValueList(facts.SourcePaths)),
            ["sourceDirectoryPrefixes"] = CelValue.List(ToValueList(facts.SourceDirectoryPrefixes)),
        };
        return CelValue.Object(new CelObjectValue(ArchitectureExpressionSchemas.SubjectObjectSchema.ObjectTypeId, members));
    }

    public static CelValue CreateDependencyValue(ArchitectureExpressionDependencyFacts facts)
    {
        var members = new Dictionary<string, CelValue>(4, StringComparer.Ordinal)
        {
            ["kind"] = CelValue.String(facts.Kind),
            ["viaMethodBody"] = CelValue.Bool(facts.ViaMethodBody),
            ["sourceMemberName"] = CelValue.String(facts.SourceMemberName),
            ["targetMemberName"] = CelValue.String(facts.TargetMemberName),
        };
        return CelValue.Object(new CelObjectValue(ArchitectureExpressionSchemas.DependencyObjectSchema.ObjectTypeId, members));
    }

    public static CelEvaluationContext CreateSelectorContext(ArchitectureExpressionSubjectFacts subject)
    {
        return ArchitectureExpressionSchemas.SelectorEnvironment.CreateEvaluationContextBuilder()
            .Set(ArchitectureExpressionSchemas.SelectorSubjectVariable, CreateSubjectValue(subject))
            .Build();
    }

    public static CelEvaluationContext CreateContextualSourceContext(ArchitectureExpressionSubjectFacts source)
    {
        return ArchitectureExpressionSchemas.ContextualSourceEnvironment.CreateEvaluationContextBuilder()
            .Set(ArchitectureExpressionSchemas.ContextualSourceVariable, CreateSubjectValue(source))
            .Build();
    }

    public static CelEvaluationContext CreateContextualTargetContext(
        ArchitectureExpressionSubjectFacts source,
        ArchitectureExpressionSubjectFacts target,
        ArchitectureExpressionDependencyFacts dependency)
    {
        return ArchitectureExpressionSchemas.ContextualTargetEnvironment.CreateEvaluationContextBuilder()
            .Set(ArchitectureExpressionSchemas.ContextualTargetSourceVariable, CreateSubjectValue(source))
            .Set(ArchitectureExpressionSchemas.ContextualTargetTargetVariable, CreateSubjectValue(target))
            .Set(ArchitectureExpressionSchemas.ContextualTargetDependencyVariable, CreateDependencyValue(dependency))
            .Build();
    }

    private static IReadOnlyDictionary<string, CelValue> ToValueMap<T>(
        IReadOnlyDictionary<string, T> source, Func<T, CelValue> convert)
    {
        var result = new Dictionary<string, CelValue>(source.Count, StringComparer.Ordinal);
        foreach ((string key, T value) in source)
        {
            result[key] = convert(value);
        }

        return result;
    }

    private static IReadOnlyList<CelValue> ToValueList(IReadOnlyList<string> source)
    {
        var result = new List<CelValue>(source.Count);
        foreach (string value in source)
        {
            result.Add(CelValue.String(value));
        }

        return result;
    }
}
