using ArchLinterNet.CEL.Evaluation;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Execution.Expressions;
using ArchLinterNet.Core.Scanning;

namespace ArchLinterNet.Core.Execution;

internal static class ArchitectureLayerTypeMatcher
{
    public static bool Matches(
        ArchitectureLayer layer, Type type, ArchitectureRoleIndex roleIndex, ArchitectureExpressionFactService expressionFacts)
    {
        if (!string.IsNullOrWhiteSpace(layer.Namespace)
            && !Resolution.ArchitectureLayerResolver.MatchesNamespace(
                layer, ArchitectureTypeNames.SafeNamespace(type)))
        {
            return false;
        }

        if (layer.Selector == null)
        {
            return !string.IsNullOrWhiteSpace(layer.Namespace);
        }

        if (!roleIndex.TryGetRole(type, out ArchitectureTypeClassificationResult descriptor)
            || !string.Equals(descriptor.Role, layer.Selector.Role, StringComparison.Ordinal))
        {
            return false;
        }

        bool matchesLiteral = layer.Selector.Metadata.All(entry =>
            descriptor.Metadata.TryGetValue(entry.Key, out object? actual)
            && ArchitectureMetadataValueComparer.ValuesEqual(actual, entry.Value));
        if (!matchesLiteral || layer.Selector.CompiledWhen == null)
        {
            return matchesLiteral;
        }

        CelEvaluationContext context = ArchitectureExpressionContextFactory.CreateSelectorContext(
            expressionFacts.BuildSubjectFacts(type));
        string description =
            $"Layer selector at '{layer.Selector.WhenLocation?.YamlPath}' (role: {layer.Selector.Role}, " +
            $"when: {layer.Selector.When}) for type '{ArchitectureTypeNames.SafeFullName(type)}'";
        return ArchitectureExpressionFactService.Evaluate(
            layer.Selector.CompiledWhen, context, description, layer.Selector.WhenLocation);
    }
}
