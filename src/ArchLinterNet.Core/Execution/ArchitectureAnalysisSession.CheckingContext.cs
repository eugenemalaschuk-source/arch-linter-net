using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Families;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Resolution;
using ArchLinterNet.Core.Scanning;

namespace ArchLinterNet.Core.Execution;

// Checker logic for the two contextual contract families (context_dependencies, context_allow_only).
// Mirrors CheckContract/CheckAllowOnlyContract in ArchitectureAnalysisSession.Checking.cs, but scans
// by direct ArchitectureContextSelector match against discovered role/metadata instead of resolved
// layers.<name> membership. See
// openspec/changes/add-contextual-dependency-contracts/design.md Decision 4.
public sealed partial class ArchitectureAnalysisSession
{
    public List<ArchitectureViolation> CheckContextDependencyContract(ArchitectureContextDependencyContract contract)
    {
        if (!IsContractSelected(contract.Id))
        {
            return new List<ArchitectureViolation>();
        }

        List<ArchitectureViolation> violations = new();
        ArchitectureContractExecutionContext executionContext = CreateExecutionContext(contract, contract.IgnoredViolations);

        foreach (Type sourceType in FindContextSelectorMatchingTypes(contract.Source))
        {
            CollectContextDependencyViolations(contract, sourceType, executionContext, violations);
        }

        executionContext.CollectUnmatchedIgnores(_unmatchedIgnoredViolations);
        return violations;
    }

    private void CollectContextDependencyViolations(
        ArchitectureContextDependencyContract contract,
        Type sourceType,
        ArchitectureContractExecutionContext executionContext,
        List<ArchitectureViolation> violations)
    {
        if (!RoleIndex.TryGetRole(sourceType, out ArchitectureTypeClassificationResult sourceDescriptor))
        {
            return;
        }

        string sourceFullName = ArchitectureTypeNames.SafeFullName(sourceType);

        // Scan references once per source type, not once per forbidden selector: GetReferencedTypes
        // does not itself deduplicate (it walks interfaces/base type/fields/properties/methods/
        // constructors independently), and a target matching more than one forbidden selector must
        // still produce exactly one finding per source/target pair, not one per matching selector.
        foreach (Type referencedType in ArchitectureReferenceScanner.GetReferencedTypes(sourceType).Distinct())
        {
            if (IsExcludedFromContextMatch(referencedType, contract.Exclude, sourceDescriptor, sourceType))
            {
                continue;
            }

            ArchitectureContextSelector? matchedSelector = contract.Forbidden.FirstOrDefault(selector =>
                ArchitectureContextSelectorMatcher.Matches(
                    selector, referencedType, RoleIndex, sourceDescriptor, ExpressionFacts, sourceType));

            if (matchedSelector == null)
            {
                continue;
            }

            string targetFullName = ArchitectureTypeNames.SafeFullName(referencedType);
            if (string.IsNullOrEmpty(targetFullName)
                || executionContext.IsIgnored(sourceFullName, targetFullName))
            {
                continue;
            }

            RoleIndex.TryGetRole(referencedType, out ArchitectureTypeClassificationResult targetDescriptor);

            violations.Add(new ArchitectureViolation(
                contract.Name, contract.Id, sourceFullName,
                DescribeContextSelector(matchedSelector),
                new[] { targetFullName })
            {
                Payload = new ContextDependencyPayload(
                    SourceRole: sourceDescriptor.Role,
                    SourceMetadata: sourceDescriptor.Metadata,
                    TargetRole: targetDescriptor.Role,
                    TargetMetadata: targetDescriptor.Metadata,
                    MatchedSelector: "forbidden")
            });
        }
    }

    public List<ArchitectureViolation> CheckContextAllowOnlyContract(ArchitectureContextAllowOnlyContract contract)
    {
        if (!IsContractSelected(contract.Id))
        {
            return new List<ArchitectureViolation>();
        }

        List<ArchitectureViolation> violations = new();
        ArchitectureContractExecutionContext executionContext = CreateExecutionContext(contract, contract.IgnoredViolations);

        foreach (Type sourceType in FindContextSelectorMatchingTypes(contract.Source))
        {
            CollectContextAllowOnlyViolations(contract, sourceType, executionContext, violations);
        }

        executionContext.CollectUnmatchedIgnores(_unmatchedIgnoredViolations);
        return violations;
    }

    private void CollectContextAllowOnlyViolations(
        ArchitectureContextAllowOnlyContract contract,
        Type sourceType,
        ArchitectureContractExecutionContext executionContext,
        List<ArchitectureViolation> violations)
    {
        if (!RoleIndex.TryGetRole(sourceType, out ArchitectureTypeClassificationResult sourceDescriptor))
        {
            return;
        }

        string sourceFullName = ArchitectureTypeNames.SafeFullName(sourceType);

        // GetReferencedTypes does not itself deduplicate, so a target referenced via more than one
        // member (field, property, method signature, etc.) must be collapsed before evaluation —
        // otherwise it would produce one violation per occurrence instead of one per source/target pair.
        foreach (Type referencedType in ArchitectureReferenceScanner.GetReferencedTypes(sourceType).Distinct())
        {
            if (!IsContextAllowOnlyCandidateViolation(
                    contract, referencedType, sourceDescriptor, sourceType,
                    out ArchitectureTypeClassificationResult targetDescriptor, out string? nearMissWhen))
            {
                continue;
            }

            string targetFullName = ArchitectureTypeNames.SafeFullName(referencedType);
            if (string.IsNullOrEmpty(targetFullName)
                || executionContext.IsIgnored(sourceFullName, targetFullName))
            {
                continue;
            }

            string[] evidence = nearMissWhen == null
                ? new[] { targetFullName }
                : new[] { targetFullName, nearMissWhen };

            violations.Add(new ArchitectureViolation(
                contract.Name, contract.Id, sourceFullName, "outside allowed context selectors", evidence)
            {
                Payload = new ContextAllowOnlyPayload(
                    SourceRole: sourceDescriptor.Role,
                    SourceMetadata: sourceDescriptor.Metadata,
                    TargetRole: targetDescriptor.Role,
                    TargetMetadata: targetDescriptor.Metadata,
                    MatchedSelector: "none")
            });
        }
    }

    // nearMissWhen is set when no allowed selector matched, but at least one allowed selector's
    // literal role/metadata criteria matched and only failed because its `when` evaluated false —
    // surfaced as extra diagnostic evidence per the contextual-allow-only-contracts delta spec's
    // "Diagnostic identifies a participating when expression" scenario.
    private bool IsContextAllowOnlyCandidateViolation(
        ArchitectureContextAllowOnlyContract contract,
        Type referencedType,
        ArchitectureTypeClassificationResult sourceDescriptor,
        Type sourceType,
        out ArchitectureTypeClassificationResult targetDescriptor,
        out string? nearMissWhen)
    {
        targetDescriptor = default!;
        nearMissWhen = null;

        if (IsExcludedFromContextMatch(referencedType, contract.Exclude, sourceDescriptor, sourceType))
        {
            return false;
        }

        bool allowed = contract.Allowed.Any(selector =>
            ArchitectureContextSelectorMatcher.Matches(
                selector, referencedType, RoleIndex, sourceDescriptor, ExpressionFacts, sourceType));

        if (allowed)
        {
            return false;
        }

        ArchitectureContextSelector? nearMissSelector = contract.Allowed.FirstOrDefault(selector =>
            !string.IsNullOrEmpty(selector.When)
            && ArchitectureContextSelectorMatcher.MatchesLiteral(selector, referencedType, RoleIndex, sourceDescriptor));
        if (nearMissSelector != null)
        {
            nearMissWhen = $"when: {nearMissSelector.When} (evaluated false for this target)";
        }

        // Only role-classified referenced types are meaningful candidates for a contextual
        // allow-only violation — an unclassified type (framework/BCL types, primitives, etc.)
        // cannot match any selector and reporting it would be unrelated noise, mirroring how
        // CheckAllowOnlyContract only considers references already inside a declared layer.
        return RoleIndex.TryGetRole(referencedType, out targetDescriptor);
    }

    private IEnumerable<Type> FindContextSelectorMatchingTypes(ArchitectureContextSelector selector)
    {
        return RoleIndex.ClassifiedTypes().Where(type =>
            ArchitectureContextSelectorMatcher.Matches(
                selector, type, RoleIndex, sourceDescriptor: null, ExpressionFacts, sourceType: null));
    }

    // sourceType is optional: the contextual dependency/allow-only families always supply it (their
    // exclude selectors are an approved `when` location and need the real source Type to build a
    // ContextualTargetEnvironment). Port-boundary's own exclude selectors reuse this same shape but
    // structurally never carry a compiled `when` (see ArchitectureContextSelector's own doc comment),
    // so its call site omits sourceType — the `when`-evaluation branch is provably unreachable there.
    private bool IsExcludedFromContextMatch(
        Type candidateType,
        IReadOnlyList<ArchitectureContextSelector> excludeSelectors,
        ArchitectureTypeClassificationResult sourceDescriptor,
        Type? sourceType = null)
    {
        return excludeSelectors.Any(selector =>
            ArchitectureContextSelectorMatcher.Matches(
                selector, candidateType, RoleIndex, sourceDescriptor, ExpressionFacts, sourceType));
    }

    private void RegisterContextualConsumers(
        ArchitectureContextSelector source,
        IEnumerable<ArchitectureContextSelector> targetSelectors,
        IEnumerable<ArchitectureContextSelector> excludeSelectors)
    {
        RegisterContextualConsumer(source);

        foreach (ArchitectureContextSelector selector in targetSelectors)
        {
            RegisterContextualConsumer(source, selector);
        }

        foreach (ArchitectureContextSelector selector in excludeSelectors)
        {
            RegisterContextualConsumer(source, selector);
        }
    }

    private static string DescribeContextSelector(ArchitectureContextSelector selector)
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
}
