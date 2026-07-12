using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Families;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;
using ArchLinterNet.Core.Resolution;
using ArchLinterNet.Core.Scanning;

namespace ArchLinterNet.Core.Execution;

public sealed partial class ArchitectureAnalysisSession
{
    private ArchitectureCoverageSummary BuildSemanticRoleCoverageSummary(ArchitectureCoverageContract contract)
    {
        List<ArchitectureCoverageSummaryExcludedItem> excludedItems = new();
        List<ArchitectureCoverageSummaryEvidenceItem> uncoveredItems = new();
        List<ArchitectureCoverageSummaryEvidenceItem> staleItems = new();
        List<ArchitectureCoverageSummaryEvidenceItem> unknownItems = new();
        List<ArchitectureCoverageSummaryEvidenceItem> coveredItems = new();
        Type[] types = TypeIndex.AllTypes()
            .OrderBy(type => ArchitectureTypeNames.SafeFullName(type), StringComparer.Ordinal)
            .ToArray();

        foreach (Type type in types)
        {
            if (!IsSemanticCoverageTypeInScope(contract, type))
            {
                continue;
            }

            if (!RoleIndex.TryGetRole(type, out ArchitectureTypeClassificationResult descriptor))
            {
                uncoveredItems.Add(new ArchitectureCoverageSummaryEvidenceItem(
                    ArchitectureTypeNames.SafeFullName(type), "unclassified semantic fact"));
                continue;
            }

            ArchitectureCoverageExclusion? exclusion = contract.Exclude.FirstOrDefault(
                candidate => MatchesSemanticExclusion(candidate, descriptor));
            if (exclusion != null)
            {
                excludedItems.Add(new ArchitectureCoverageSummaryExcludedItem(
                    ArchitectureTypeNames.SafeFullName(type), exclusion.Reason));
                continue;
            }

            List<ArchitectureCoverageSummaryEvidenceItem> target = IsSemanticFactGoverned(descriptor)
                ? coveredItems
                : uncoveredItems;
            target.Add(new ArchitectureCoverageSummaryEvidenceItem(
                ArchitectureTypeNames.SafeFullName(type), DescribeSemanticFact(descriptor)));
        }

        foreach (ArchitectureLayer layer in Document.Layers.Values
                     .Where(layer => layer.Selector != null)
                     .OrderBy(layer => ArchitectureLayerResolver.DescribeLayer(layer), StringComparer.Ordinal))
        {
            if (!types.Any(type => RoleIndex.TryGetRole(type, out _) && MatchesLayer(layer, type)))
            {
                staleItems.Add(new ArchitectureCoverageSummaryEvidenceItem(
                    ArchitectureLayerResolver.DescribeLayer(layer), "semantic selector matched no classified type"));
            }
        }

        foreach (ArchitectureContextualConsumerReference consumer in RegisteredContextualConsumers
                     .OrderBy(consumer => consumer.Role, StringComparer.Ordinal)
                     .ThenBy(consumer => consumer.MetadataKey, StringComparer.Ordinal))
        {
            if (!types.Any(type => RoleIndex.TryGetRole(type, out ArchitectureTypeClassificationResult descriptor)
                                   && string.Equals(descriptor.Role, consumer.Role, StringComparison.Ordinal)
                                   && (consumer.MetadataKey.Length == 0 || descriptor.Metadata.ContainsKey(consumer.MetadataKey))))
            {
                staleItems.Add(new ArchitectureCoverageSummaryEvidenceItem(
                    DescribeConsumer(consumer), "contextual semantic selector matched no classified type"));
            }
        }

        foreach (ArchitectureClassificationConflict conflict in RoleIndex.Conflicts
                     .OrderBy(conflict => conflict.Subject, StringComparer.Ordinal))
        {
            unknownItems.Add(new ArchitectureCoverageSummaryEvidenceItem(
                conflict.Subject, $"classification conflict: {conflict.WinningRole} vs {conflict.DiscardedRole}"));
        }

        return new ArchitectureCoverageSummary(
            contract.Name, contract.Id, contract.Scope,
            new ArchitectureCoverageSummaryCounts(
                coveredItems.Count, excludedItems.Count, uncoveredItems.Count, staleItems.Count, unknownItems.Count),
            excludedItems, uncoveredItems, staleItems, unknownItems, coveredItems);
    }

    private List<ArchitectureViolation> CheckSemanticRoleCoverageContract(ArchitectureCoverageContract contract)
    {
        List<ArchitectureViolation> findings = new();
        Type[] types = TypeIndex.AllTypes()
            .OrderBy(type => ArchitectureTypeNames.SafeFullName(type), StringComparer.Ordinal)
            .ToArray();
        ArchitectureContractExecutionContext executionContext = CreateExecutionContext(contract, contract.IgnoredViolations);

        foreach (Type type in types)
        {
            if (!IsSemanticCoverageTypeInScope(contract, type))
            {
                continue;
            }

            string subject = ArchitectureTypeNames.SafeFullName(type);
            if (!RoleIndex.TryGetRole(type, out ArchitectureTypeClassificationResult descriptor))
            {
                if (!executionContext.IsIgnored(subject, "unclassified semantic fact"))
                {
                    findings.Add(new ArchitectureViolation(contract.Name, contract.Id, subject,
                        "unclassified semantic fact", new[] { subject }));
                }
                continue;
            }

            if (contract.Exclude.Any(exclusion => MatchesSemanticExclusion(exclusion, descriptor))
                || IsSemanticFactGoverned(descriptor))
            {
                continue;
            }

            if (!executionContext.IsIgnored(subject, "uncovered semantic role"))
            {
                findings.Add(new ArchitectureViolation(contract.Name, contract.Id, subject,
                    "uncovered semantic role", new[] { DescribeSemanticFact(descriptor) }));
            }
        }

        executionContext.CollectUnmatchedIgnores(_unmatchedIgnoredViolations);
        return findings;
    }

    private bool IsSemanticCoverageTypeInScope(ArchitectureCoverageContract contract, Type type)
    {
        return contract.Roots.Count == 0 || contract.Roots.Any(root =>
            MatchesNamespaceRoot(root, ArchitectureTypeNames.SafeNamespace(type)));
    }

    private bool IsSemanticFactGoverned(ArchitectureTypeClassificationResult descriptor)
    {
        return Document.Layers.Values.Any(layer => layer.Selector != null
                                                   && MatchesSemanticSelector(layer.Selector, descriptor))
               || RegisteredContextualConsumers.Any(consumer =>
                   string.Equals(consumer.Role, descriptor.Role, StringComparison.Ordinal)
                   && (consumer.MetadataKey.Length == 0 || descriptor.Metadata.ContainsKey(consumer.MetadataKey)));
    }

    private static bool MatchesSemanticSelector(
        ArchitectureLayerSelector selector,
        ArchitectureTypeClassificationResult descriptor)
    {
        return string.Equals(selector.Role, descriptor.Role, StringComparison.Ordinal)
               && selector.Metadata.All(entry => descriptor.Metadata.TryGetValue(entry.Key, out object? actual)
                                                 && ArchitectureMetadataValueComparer.ValuesEqual(actual, entry.Value));
    }

    private static bool MatchesSemanticExclusion(
        ArchitectureCoverageExclusion exclusion,
        ArchitectureTypeClassificationResult descriptor)
    {
        return string.Equals(exclusion.Role, descriptor.Role, StringComparison.Ordinal)
               && exclusion.Metadata.All(entry => descriptor.Metadata.TryGetValue(entry.Key, out object? actual)
                                                  && ArchitectureMetadataValueComparer.ValuesEqual(actual, entry.Value));
    }

    private static string DescribeSemanticFact(ArchitectureTypeClassificationResult descriptor)
    {
        string metadata = descriptor.Metadata.Count == 0
            ? string.Empty
            : $" metadata={string.Join(",", descriptor.Metadata.OrderBy(entry => entry.Key, StringComparer.Ordinal).Select(entry => $"{entry.Key}={entry.Value}"))}";
        return $"role={descriptor.Role}{metadata}";
    }

    private static string DescribeConsumer(ArchitectureContextualConsumerReference consumer)
    {
        return consumer.MetadataKey.Length == 0
            ? $"role:{consumer.Role}"
            : $"role:{consumer.Role} metadata:{consumer.MetadataKey}";
    }
}
