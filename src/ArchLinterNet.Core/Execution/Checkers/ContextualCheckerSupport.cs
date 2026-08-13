using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Model;

namespace ArchLinterNet.Core.Execution.Checkers;

// Shared diagnostic helpers for the selector-driven families (context_dependency,
// context_allow_only, port_boundary), which describe the same ArchitectureContextSelector shape in
// their findings.
internal static class ContextualCheckerSupport
{
    public static string DescribeSelector(ArchitectureContextSelector selector)
    {
        string whenSuffix = string.IsNullOrEmpty(selector.When) ? string.Empty : $", when: {selector.When}";

        if (selector.Metadata.Count == 0)
        {
            return $"role:{selector.Role}{whenSuffix}";
        }

        string metadataDescription = string.Join(", ", selector.Metadata
            .OrderBy(entry => entry.Key, StringComparer.Ordinal)
            .Select(entry => $"{entry.Key}={entry.Value}"));

        return $"role:{selector.Role} ({metadataDescription}){whenSuffix}";
    }

    // Built from data ArchitectureContextSelector already carries (raw source text, resolved YAML
    // location) rather than re-deriving anything - see ArchitectureContextSelector's own doc comment
    // for why WhenLocation/WhenContractName live on the selector itself. A no-op when the selector (or
    // the selector reference itself, e.g. no near-miss) has no `when` - callers can call this
    // unconditionally for every selector that might participate.
    public static void AddWhenExpression(
        List<ExpressionParticipation> whenExpressions,
        string contractName,
        ArchitectureContextSelector? selector,
        string location,
        ExpressionParticipationResult result)
    {
        if (selector == null || string.IsNullOrEmpty(selector.When))
        {
            return;
        }

        whenExpressions.Add(new ExpressionParticipation(
            selector.WhenContractName ?? contractName, location, selector.When, selector.WhenLocation?.YamlPath, result)
        {
            PolicySourcePath = selector.WhenLocation?.SourcePath,
            PolicySourceLine = selector.WhenLocation?.Line,
            PolicySourceColumn = selector.WhenLocation?.Column,
        });
    }
}
