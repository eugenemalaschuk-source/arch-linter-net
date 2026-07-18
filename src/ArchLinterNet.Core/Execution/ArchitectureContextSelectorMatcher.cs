using System.Collections;
using System.Text.RegularExpressions;
using ArchLinterNet.CEL.Evaluation;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Execution.Expressions;
using ArchLinterNet.Core.Scanning;

namespace ArchLinterNet.Core.Execution;

// Matches a candidate type against an ArchitectureContextSelector's role + metadata-operator
// vocabulary. Distinct from ArchitectureLayerTypeMatcher, which stays pinned to exact/AND-only
// matching for layers.<name>.selector — see
// openspec/changes/add-contextual-dependency-contracts/design.md Decision 1 and Decision 2.
//
// Metadata value grammar, checked in this fixed order:
//   1. YAML sequence                                    -> in (matches any listed entry)
//   2. literal string "*"                                -> any (matches any resolved value, key must be present)
//   3. string matching ^!\{source\.metadata\.<key>\}$    -> not-equal-to-source (resolved against the
//      current source type's own metadata; only meaningful on forbidden/allowed/exclude selectors)
//   4. anything else                                     -> exact literal
internal static partial class ArchitectureContextSelectorMatcher
{
    [GeneratedRegex(@"^!\{source\.metadata\.([A-Za-z0-9_]+)\}$", RegexOptions.CultureInvariant)]
    private static partial Regex NotEqualToSourcePattern();

    // sourceDescriptor is the current contract evaluation's source type's own resolved role/metadata,
    // used to resolve not-equal-to-source constraints. Pass null when matching the contract's own
    // `source` selector itself (which has no other source to reference).
    //
    // This overload only supports literal role/metadata matching. It is used by call sites where a
    // `when` expression can never be present structurally — port-boundary/adapter-binding selectors,
    // which reuse ArchitectureContextSelector's shape but have their raw YAML `when` key rejected at
    // policy-load time (see ArchitecturePolicyDocumentLoader's contextual selector key validators).
    // The guard below turns a violation of that structural invariant into a clear error instead of
    // silently ignoring a compiled predicate.
    public static bool Matches(
        ArchitectureContextSelector selector,
        Type candidateType,
        ArchitectureRoleIndex roleIndex,
        ArchitectureTypeClassificationResult? sourceDescriptor)
    {
        if (selector.CompiledWhen != null)
        {
            throw new InvalidOperationException(
                "ArchitectureContextSelector unexpectedly compiled a 'when' predicate in a context that " +
                "does not support expression evaluation. This indicates a policy-loading validation gap.");
        }

        return MatchesLiteral(selector, candidateType, roleIndex, sourceDescriptor);
    }

    // The `when`-aware overload used by the contextual dependency/allow-only contract families and
    // semantic coverage, where `when` is an approved location. sourceType is the actual candidate
    // type behind sourceDescriptor, needed to build full subject facts for the `source` side of a
    // contextual target/exclude predicate; pass both null together when selector is the contract's
    // own `source` selector (candidateType is itself becoming `source`, evaluated under
    // ContextualSourceEnvironment rather than ContextualTargetEnvironment).
    public static bool Matches(
        ArchitectureContextSelector selector,
        Type candidateType,
        ArchitectureRoleIndex roleIndex,
        ArchitectureTypeClassificationResult? sourceDescriptor,
        ArchitectureExpressionFactService expressionFacts,
        Type? sourceType)
    {
        bool matchesLiteral = MatchesLiteral(selector, candidateType, roleIndex, sourceDescriptor);
        if (!matchesLiteral || selector.CompiledWhen == null)
        {
            return matchesLiteral;
        }

        string description = $"Contextual selector (role: {selector.Role})";
        if (sourceDescriptor == null || sourceType == null)
        {
            CelEvaluationContext sourceContext = ArchitectureExpressionContextFactory.CreateContextualSourceContext(
                expressionFacts.BuildSubjectFacts(candidateType));
            return ArchitectureExpressionFactService.Evaluate(selector.CompiledWhen, sourceContext, description);
        }

        CelEvaluationContext targetContext = ArchitectureExpressionContextFactory.CreateContextualTargetContext(
            expressionFacts.BuildSubjectFacts(sourceType),
            expressionFacts.BuildSubjectFacts(candidateType),
            ArchitectureExpressionFactService.BuildDependencyFacts());
        return ArchitectureExpressionFactService.Evaluate(selector.CompiledWhen, targetContext, description);
    }

    // Exposed internally (not just used by the Matches overloads above) so callers that need to
    // report *why* a selector didn't fully match — e.g. a near-miss "when" evaluated false — can
    // check literal role/metadata matching without evaluating any expression.
    internal static bool MatchesLiteral(
        ArchitectureContextSelector selector,
        Type candidateType,
        ArchitectureRoleIndex roleIndex,
        ArchitectureTypeClassificationResult? sourceDescriptor)
    {
        ArgumentNullException.ThrowIfNull(selector);
        ArgumentNullException.ThrowIfNull(candidateType);
        ArgumentNullException.ThrowIfNull(roleIndex);

        if (!roleIndex.TryGetRole(candidateType, out ArchitectureTypeClassificationResult descriptor)
            || !string.Equals(descriptor.Role, selector.Role, StringComparison.Ordinal))
        {
            return false;
        }

        return selector.Metadata.All(entry =>
            MatchesMetadataConstraint(entry.Key, entry.Value, descriptor, sourceDescriptor));
    }

    private static bool MatchesMetadataConstraint(
        string key,
        object constraintValue,
        ArchitectureTypeClassificationResult descriptor,
        ArchitectureTypeClassificationResult? sourceDescriptor)
    {
        if (!descriptor.Metadata.TryGetValue(key, out object? actual))
        {
            return false;
        }

        if (constraintValue is IEnumerable sequence and not string)
        {
            return MatchesAnyListedValue(actual, sequence);
        }

        if (constraintValue is string stringValue)
        {
            bool? specialFormResult = MatchesStringOperator(actual, stringValue, sourceDescriptor);
            if (specialFormResult.HasValue)
            {
                return specialFormResult.Value;
            }
        }

        return ArchitectureMetadataValueComparer.ValuesEqual(actual, constraintValue);
    }

    // The "in" operator: matches if the resolved value equals any listed candidate.
    private static bool MatchesAnyListedValue(object actual, IEnumerable candidates)
    {
        foreach (object item in candidates)
        {
            if (ArchitectureMetadataValueComparer.ValuesEqual(actual, item))
            {
                return true;
            }
        }

        return false;
    }

    // Recognizes the "any" ("*") and "not-equal-to-source" string special forms. Returns null when
    // stringValue is neither, so the caller falls through to plain exact-literal comparison.
    private static bool? MatchesStringOperator(
        object actual, string stringValue, ArchitectureTypeClassificationResult? sourceDescriptor)
    {
        if (stringValue == "*")
        {
            return true;
        }

        Match match = NotEqualToSourcePattern().Match(stringValue);
        if (!match.Success)
        {
            return null;
        }

        string sourceKey = match.Groups[1].Value;
        if (sourceDescriptor == null || !sourceDescriptor.Metadata.TryGetValue(sourceKey, out object? sourceValue))
        {
            return false;
        }

        return !ArchitectureMetadataValueComparer.ValuesEqual(actual, sourceValue);
    }
}
