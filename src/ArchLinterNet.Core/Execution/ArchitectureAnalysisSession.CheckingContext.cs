using ArchLinterNet.Core.Contracts;
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

        RegisterContextualConsumer(contract.Source);
        foreach (ArchitectureContextSelector forbiddenSelector in contract.Forbidden)
        {
            RegisterContextualConsumer(forbiddenSelector);
        }

        foreach (ArchitectureContextSelector excludeSelector in contract.Exclude)
        {
            RegisterContextualConsumer(excludeSelector);
        }

        List<ArchitectureViolation> violations = new();
        ArchitectureContractExecutionContext executionContext = CreateExecutionContext(contract, contract.IgnoredViolations);

        foreach (Type sourceType in FindContextSelectorMatchingTypes(contract.Source))
        {
            if (!RoleIndex.TryGetRole(sourceType, out ArchitectureTypeClassificationResult sourceDescriptor))
            {
                continue;
            }

            string sourceFullName = ArchitectureTypeNames.SafeFullName(sourceType);

            // Scan references once per source type, not once per forbidden selector: GetReferencedTypes
            // does not itself deduplicate (it walks interfaces/base type/fields/properties/methods/
            // constructors independently), and a target matching more than one forbidden selector must
            // still produce exactly one finding per source/target pair, not one per matching selector.
            foreach (Type referencedType in ArchitectureReferenceScanner.GetReferencedTypes(sourceType).Distinct())
            {
                if (IsExcludedFromContextMatch(referencedType, contract.Exclude, sourceDescriptor))
                {
                    continue;
                }

                ArchitectureContextSelector? matchedSelector = contract.Forbidden.FirstOrDefault(selector =>
                    ArchitectureContextSelectorMatcher.Matches(selector, referencedType, RoleIndex, sourceDescriptor));

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

        executionContext.CollectUnmatchedIgnores(_unmatchedIgnoredViolations);
        return violations;
    }

    public List<ArchitectureViolation> CheckContextAllowOnlyContract(ArchitectureContextAllowOnlyContract contract)
    {
        if (!IsContractSelected(contract.Id))
        {
            return new List<ArchitectureViolation>();
        }

        RegisterContextualConsumer(contract.Source);
        foreach (ArchitectureContextSelector allowedSelector in contract.Allowed)
        {
            RegisterContextualConsumer(allowedSelector);
        }

        foreach (ArchitectureContextSelector excludeSelector in contract.Exclude)
        {
            RegisterContextualConsumer(excludeSelector);
        }

        List<ArchitectureViolation> violations = new();
        ArchitectureContractExecutionContext executionContext = CreateExecutionContext(contract, contract.IgnoredViolations);

        foreach (Type sourceType in FindContextSelectorMatchingTypes(contract.Source))
        {
            if (!RoleIndex.TryGetRole(sourceType, out ArchitectureTypeClassificationResult sourceDescriptor))
            {
                continue;
            }

            string sourceFullName = ArchitectureTypeNames.SafeFullName(sourceType);

            // GetReferencedTypes does not itself deduplicate, so a target referenced via more than one
            // member (field, property, method signature, etc.) must be collapsed before evaluation —
            // otherwise it would produce one violation per occurrence instead of one per source/target pair.
            foreach (Type referencedType in ArchitectureReferenceScanner.GetReferencedTypes(sourceType).Distinct())
            {
                if (IsExcludedFromContextMatch(referencedType, contract.Exclude, sourceDescriptor))
                {
                    continue;
                }

                bool allowed = contract.Allowed.Any(selector =>
                    ArchitectureContextSelectorMatcher.Matches(selector, referencedType, RoleIndex, sourceDescriptor));

                if (allowed)
                {
                    continue;
                }

                // Only role-classified referenced types are meaningful candidates for a contextual
                // allow-only violation — an unclassified type (framework/BCL types, primitives, etc.)
                // cannot match any selector and reporting it would be unrelated noise, mirroring how
                // CheckAllowOnlyContract only considers references already inside a declared layer.
                if (!RoleIndex.TryGetRole(referencedType, out ArchitectureTypeClassificationResult targetDescriptor))
                {
                    continue;
                }

                string targetFullName = ArchitectureTypeNames.SafeFullName(referencedType);
                if (string.IsNullOrEmpty(targetFullName)
                    || executionContext.IsIgnored(sourceFullName, targetFullName))
                {
                    continue;
                }

                violations.Add(new ArchitectureViolation(
                    contract.Name, contract.Id, sourceFullName, "outside allowed context selectors",
                    new[] { targetFullName })
                {
                    Payload = new ContextAllowOnlyPayload(
                        SourceRole: sourceDescriptor.Role,
                        SourceMetadata: sourceDescriptor.Metadata,
                        TargetRole: targetDescriptor.Role,
                        TargetMetadata: targetDescriptor.Metadata)
                });
            }
        }

        executionContext.CollectUnmatchedIgnores(_unmatchedIgnoredViolations);
        return violations;
    }

    private IEnumerable<Type> FindContextSelectorMatchingTypes(ArchitectureContextSelector selector)
    {
        foreach (Type type in RoleIndex.ClassifiedTypes())
        {
            if (ArchitectureContextSelectorMatcher.Matches(selector, type, RoleIndex, sourceDescriptor: null))
            {
                yield return type;
            }
        }
    }

    private bool IsExcludedFromContextMatch(
        Type candidateType,
        IReadOnlyList<ArchitectureContextSelector> excludeSelectors,
        ArchitectureTypeClassificationResult sourceDescriptor)
    {
        return excludeSelectors.Any(selector =>
            ArchitectureContextSelectorMatcher.Matches(selector, candidateType, RoleIndex, sourceDescriptor));
    }

    private static string DescribeContextSelector(ArchitectureContextSelector selector)
    {
        if (selector.Metadata.Count == 0)
        {
            return $"role:{selector.Role}";
        }

        string metadataDescription = string.Join(", ", selector.Metadata
            .OrderBy(entry => entry.Key, StringComparer.Ordinal)
            .Select(entry => $"{entry.Key}={entry.Value}"));

        return $"role:{selector.Role} ({metadataDescription})";
    }
}
