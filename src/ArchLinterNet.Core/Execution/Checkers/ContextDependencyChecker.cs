using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Families;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Resolution;
using ArchLinterNet.Core.Scanning;

namespace ArchLinterNet.Core.Execution.Checkers;

// Checker for the context_dependencies family. Mirrors DependencyChecker, but scans by direct
// ArchitectureContextSelector match against discovered role/metadata instead of resolved
// layers.<name> membership. See
// openspec/changes/add-contextual-dependency-contracts/design.md Decision 4.
internal static class ContextDependencyChecker
{
    public static List<ArchitectureViolation> Check(
        ArchitectureContextDependencyContract contract,
        ArchitectureCheckerContext context,
        ArchitectureContractExecutionContext executionContext)
    {
        List<ArchitectureViolation> violations = new();

        foreach (Type sourceType in context.FindContextSelectorMatchingTypes(contract.Source))
        {
            CollectViolations(contract, sourceType, context, executionContext, violations);
        }

        return violations;
    }

    private static void CollectViolations(
        ArchitectureContextDependencyContract contract,
        Type sourceType,
        ArchitectureCheckerContext context,
        ArchitectureContractExecutionContext executionContext,
        List<ArchitectureViolation> violations)
    {
        if (!context.RoleIndex.TryGetRole(sourceType, out ArchitectureTypeClassificationResult sourceDescriptor))
        {
            return;
        }

        string sourceFullName = ArchitectureTypeNames.SafeFullName(sourceType);
        string sourceAssembly = ArchitectureTypeNames.SafeAssemblyName(sourceType) ?? string.Empty;

        // Scan references once per source type, not once per forbidden selector: GetReferencedTypes
        // does not itself deduplicate (it walks interfaces/base type/fields/properties/methods/
        // constructors independently), and a target matching more than one forbidden selector must
        // still produce exactly one finding per source/target pair, not one per matching selector.
        foreach (Type referencedType in ArchitectureReferenceScanner.GetReferencedTypes(sourceType).Distinct())
        {
            if (context.IsExcludedFromContextMatch(referencedType, contract.Exclude, sourceDescriptor, sourceType))
            {
                continue;
            }

            // Iterate in order and stop at the first full match, so only selectors that were
            // actually evaluated before the winner can be marked NotMatched. Selectors after the
            // winning one are never reached by Matches() and must not appear as NotMatched.
            ArchitectureContextSelector? matchedSelector = null;
            List<ArchitectureContextSelector> notMatchedForbidden = new();
            foreach (ArchitectureContextSelector candidate in contract.Forbidden)
            {
                if (ArchitectureContextSelectorMatcher.Matches(
                        candidate, referencedType, context.RoleIndex, sourceDescriptor, context.ExpressionFacts, sourceType))
                {
                    matchedSelector = candidate;
                    break;
                }
                if (!string.IsNullOrEmpty(candidate.When)
                    && ArchitectureContextSelectorMatcher.MatchesLiteral(
                        candidate, referencedType, context.RoleIndex, sourceDescriptor))
                {
                    notMatchedForbidden.Add(candidate);
                }
            }

            if (matchedSelector == null)
            {
                continue;
            }

            string targetFullName = ArchitectureTypeNames.SafeFullName(referencedType);
            if (string.IsNullOrEmpty(targetFullName)
                || executionContext.IsIgnored(
                    sourceFullName,
                    targetFullName,
                    sourceAssembly: sourceAssembly,
                    targetAssembly: ArchitectureTypeNames.SafeAssemblyName(referencedType),
                    targetType: targetFullName,
                    targetMember: targetFullName))
            {
                continue;
            }

            context.RoleIndex.TryGetRole(referencedType, out ArchitectureTypeClassificationResult targetDescriptor);

            // Exclude selectors that matched literally but whose `when` returned false - because
            // IsExcludedFromContextMatch returned false, reaching here proves all exclude selectors
            // that matched role/metadata must have had when=false.
            IReadOnlyList<ArchitectureContextSelector> notMatchedExclude = contract.Exclude
                .Where(s => !string.IsNullOrEmpty(s.When)
                    && ArchitectureContextSelectorMatcher.MatchesLiteral(
                        s, referencedType, context.RoleIndex, sourceDescriptor))
                .ToList();

            List<ExpressionParticipation> whenExpressions = new();
            // contract.Source.When already evaluated true for this sourceType - FindContextSelectorMatchingTypes
            // filtered by it before this method was ever called for this candidate.
            ContextualCheckerSupport.AddWhenExpression(
                whenExpressions, contract.Name, contract.Source, "source", ExpressionParticipationResult.Matched);
            foreach (ArchitectureContextSelector s in notMatchedForbidden)
                ContextualCheckerSupport.AddWhenExpression(
                    whenExpressions, contract.Name, s, "forbidden", ExpressionParticipationResult.NotMatched);
            ContextualCheckerSupport.AddWhenExpression(
                whenExpressions, contract.Name, matchedSelector, "forbidden", ExpressionParticipationResult.Matched);
            foreach (ArchitectureContextSelector s in notMatchedExclude)
                ContextualCheckerSupport.AddWhenExpression(
                    whenExpressions, contract.Name, s, "exclude", ExpressionParticipationResult.NotMatched);

            violations.Add(new ArchitectureViolation(
                contract.Name, contract.Id, sourceFullName,
                ContextualCheckerSupport.DescribeSelector(matchedSelector),
                new[] { targetFullName })
            {
                Payload = new ContextDependencyPayload(
                    SourceRole: sourceDescriptor.Role,
                    SourceMetadata: sourceDescriptor.Metadata,
                    TargetRole: targetDescriptor.Role,
                    TargetMetadata: targetDescriptor.Metadata,
                    MatchedSelector: "forbidden")
                {
                    WhenExpressions = whenExpressions.Count == 0 ? null : whenExpressions,
                }
            });
        }
    }
}
