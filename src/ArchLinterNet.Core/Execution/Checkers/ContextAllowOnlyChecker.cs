using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Families;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Resolution;
using ArchLinterNet.Core.Scanning;

namespace ArchLinterNet.Core.Execution.Checkers;

// Checker for the context_allow_only family — the allow-only counterpart of
// ContextDependencyChecker, matching on discovered role/metadata rather than declared layers.
internal static class ContextAllowOnlyChecker
{
    public static List<ArchitectureViolation> Check(
        ArchitectureContextAllowOnlyContract contract,
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
        ArchitectureContextAllowOnlyContract contract,
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

        // GetReferencedTypes does not itself deduplicate, so a target referenced via more than one
        // member (field, property, method signature, etc.) must be collapsed before evaluation —
        // otherwise it would produce one violation per occurrence instead of one per source/target pair.
        foreach (Type referencedType in ArchitectureReferenceScanner.GetReferencedTypes(sourceType).Distinct())
        {
            if (!IsCandidateViolation(
                    contract, referencedType, context, sourceDescriptor, sourceType,
                    out ArchitectureTypeClassificationResult targetDescriptor,
                    out IReadOnlyList<ArchitectureContextSelector> nearMissSelectors))
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

            string[] evidence = nearMissSelectors.Count == 0
                ? new[] { targetFullName }
                : new[] { targetFullName }.Concat(
                    nearMissSelectors.Select(s => $"when: {s.When} (evaluated false for this target)"))
                    .ToArray();

            // Exclude selectors that matched literally but whose `when` returned false - reaching
            // here proves all exclude selectors that matched role/metadata must have had when=false,
            // because IsCandidateViolation called IsExcludedFromContextMatch first.
            IReadOnlyList<ArchitectureContextSelector> notMatchedExclude = contract.Exclude
                .Where(s => !string.IsNullOrEmpty(s.When)
                    && ArchitectureContextSelectorMatcher.MatchesLiteral(
                        s, referencedType, context.RoleIndex, sourceDescriptor))
                .ToList();

            List<ExpressionParticipation> whenExpressions = new();
            ContextualCheckerSupport.AddWhenExpression(
                whenExpressions, contract.Name, contract.Source, "source", ExpressionParticipationResult.Matched);
            foreach (ArchitectureContextSelector nearMiss in nearMissSelectors)
            {
                ContextualCheckerSupport.AddWhenExpression(
                    whenExpressions, contract.Name, nearMiss, "allowed", ExpressionParticipationResult.NotMatched);
            }
            foreach (ArchitectureContextSelector s in notMatchedExclude)
                ContextualCheckerSupport.AddWhenExpression(
                    whenExpressions, contract.Name, s, "exclude", ExpressionParticipationResult.NotMatched);

            violations.Add(new ArchitectureViolation(
                contract.Name, contract.Id, sourceFullName, "outside allowed context selectors", evidence)
            {
                Payload = new ContextAllowOnlyPayload(
                    SourceRole: sourceDescriptor.Role,
                    SourceMetadata: sourceDescriptor.Metadata,
                    TargetRole: targetDescriptor.Role,
                    TargetMetadata: targetDescriptor.Metadata,
                    MatchedSelector: "none")
                {
                    WhenExpressions = whenExpressions.Count == 0 ? null : whenExpressions,
                }
            });
        }
    }

    // nearMissSelectors are all allowed[*] selectors whose literal role/metadata criteria matched
    // but whose `when` predicate evaluated false — each is surfaced as a separate evidence item and
    // ExpressionParticipation entry per the contextual-allow-only-contracts delta spec's
    // "Diagnostic identifies a participating when expression" scenario.
    private static bool IsCandidateViolation(
        ArchitectureContextAllowOnlyContract contract,
        Type referencedType,
        ArchitectureCheckerContext context,
        ArchitectureTypeClassificationResult sourceDescriptor,
        Type sourceType,
        out ArchitectureTypeClassificationResult targetDescriptor,
        out IReadOnlyList<ArchitectureContextSelector> nearMissSelectors)
    {
        targetDescriptor = default!;
        nearMissSelectors = Array.Empty<ArchitectureContextSelector>();

        if (context.IsExcludedFromContextMatch(referencedType, contract.Exclude, sourceDescriptor, sourceType))
        {
            return false;
        }

        bool allowed = contract.Allowed.Any(selector =>
            ArchitectureContextSelectorMatcher.Matches(
                selector, referencedType, context.RoleIndex, sourceDescriptor, context.ExpressionFacts, sourceType));

        if (allowed)
        {
            return false;
        }

        nearMissSelectors = contract.Allowed.Where(selector =>
            !string.IsNullOrEmpty(selector.When)
            && ArchitectureContextSelectorMatcher.MatchesLiteral(
                selector, referencedType, context.RoleIndex, sourceDescriptor))
            .ToList();

        // Only role-classified referenced types are meaningful candidates for a contextual
        // allow-only violation — an unclassified type (framework/BCL types, primitives, etc.)
        // cannot match any selector and reporting it would be unrelated noise, mirroring how
        // AllowOnlyChecker only considers references already inside a declared layer.
        return context.RoleIndex.TryGetRole(referencedType, out targetDescriptor);
    }
}
