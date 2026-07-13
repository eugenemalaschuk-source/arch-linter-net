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
                    ArchitectureTypeNames.SafeFullName(type), exclusion.Reason, DescribeSemanticFact(descriptor)));
                continue;
            }

            string? governance = DescribeSemanticGovernance(type);
            (governance == null ? uncoveredItems : coveredItems).Add(new ArchitectureCoverageSummaryEvidenceItem(
                ArchitectureTypeNames.SafeFullName(type), governance == null
                    ? DescribeSemanticFact(descriptor)
                    : $"{DescribeSemanticFact(descriptor)}; governed by {governance}"));
        }

        staleItems.AddRange(GetSemanticStaleItems(types));
        unknownItems.AddRange(GetSemanticUnknownItems());

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
                || DescribeSemanticGovernance(type) != null)
            {
                continue;
            }

            if (!executionContext.IsIgnored(subject, "uncovered semantic role"))
            {
                findings.Add(new ArchitectureViolation(contract.Name, contract.Id, subject,
                    "uncovered semantic role", new[] { DescribeSemanticFact(descriptor) }));
            }
        }

        foreach (ArchitectureCoverageSummaryEvidenceItem stale in GetSemanticStaleItems(types))
        {
            AddSemanticDiagnosticFinding(findings, executionContext, contract, stale, "stale semantic selector");
        }

        foreach (ArchitectureCoverageSummaryEvidenceItem unknown in GetSemanticUnknownItems())
        {
            string violation = unknown.Evidence.StartsWith("classification conflict:", StringComparison.Ordinal)
                ? "classification conflict"
                : "classification metadata failure";
            AddSemanticDiagnosticFinding(findings, executionContext, contract, unknown, violation);
        }

        executionContext.CollectUnmatchedIgnores(_unmatchedIgnoredViolations);
        return findings;
    }

    private bool IsSemanticCoverageTypeInScope(ArchitectureCoverageContract contract, Type type)
    {
        return contract.Roots.Count == 0 || contract.Roots.Any(root =>
            MatchesNamespaceRoot(root, ArchitectureTypeNames.SafeNamespace(type)));
    }

    private void AddSemanticDiagnosticFinding(
        List<ArchitectureViolation> findings,
        ArchitectureContractExecutionContext executionContext,
        ArchitectureCoverageContract contract,
        ArchitectureCoverageSummaryEvidenceItem item,
        string violation)
    {
        if (!executionContext.IsIgnored(item.Item, violation))
        {
            findings.Add(new ArchitectureViolation(contract.Name, contract.Id, item.Item, violation, new[] { item.Evidence }));
        }
    }

    private string? DescribeSemanticGovernance(Type type)
    {
        ArchitectureLayer? layer = Document.Layers.Values
            .Where(candidate => candidate.Selector != null && MatchesLayer(candidate, type))
            .OrderBy(ArchitectureLayerResolver.DescribeLayer, StringComparer.Ordinal)
            .FirstOrDefault();
        if (layer != null)
        {
            return $"layer {ArchitectureLayerResolver.DescribeLayer(layer)}";
        }

        ArchitectureContextualConsumerReference? consumer = RegisteredContextualConsumers
            .Where(candidate => MatchesContextualConsumer(candidate, type))
            .OrderBy(candidate => candidate.Description, StringComparer.Ordinal)
            .FirstOrDefault();
        return consumer == null ? null : $"contextual consumer {DescribeConsumer(consumer)}";
    }

    private List<ArchitectureCoverageSummaryEvidenceItem> GetSemanticStaleItems(IEnumerable<Type> types)
    {
        List<ArchitectureCoverageSummaryEvidenceItem> items = new();
        foreach (ArchitectureLayer layer in Document.Layers.Values
                     .Where(layer => layer.Selector != null && !layer.External)
                     .OrderBy(ArchitectureLayerResolver.DescribeLayer, StringComparer.Ordinal))
        {
            if (!types.Any(type => RoleIndex.TryGetRole(type, out _) && MatchesLayer(layer, type)))
                items.Add(new ArchitectureCoverageSummaryEvidenceItem(ArchitectureLayerResolver.DescribeLayer(layer), "semantic selector matched no classified type"));
        }
        foreach (ArchitectureContextualConsumerReference consumer in RegisteredContextualConsumers
                     .OrderBy(consumer => consumer.Description, StringComparer.Ordinal))
        {
            if (!types.Any(type => MatchesContextualConsumer(consumer, type)))
                items.Add(new ArchitectureCoverageSummaryEvidenceItem(DescribeConsumer(consumer), "contextual semantic selector matched no classified type"));
        }
        return items;
    }

    private List<ArchitectureCoverageSummaryEvidenceItem> GetSemanticUnknownItems()
    {
        List<ArchitectureCoverageSummaryEvidenceItem> items = new();
        items.AddRange(RoleIndex.Conflicts
            .OrderBy(conflict => conflict.Subject, StringComparer.Ordinal)
            .ThenBy(conflict => conflict.Source)
            .ThenBy(conflict => conflict.WinningRole, StringComparer.Ordinal)
            .ThenBy(conflict => conflict.DiscardedRole, StringComparer.Ordinal)
            .ThenBy(conflict => conflict.MetadataDetail, StringComparer.Ordinal)
            .Select(conflict => new ArchitectureCoverageSummaryEvidenceItem(conflict.Subject,
                $"classification conflict: source={conflict.Source}; winning={conflict.WinningRole}; discarded={conflict.DiscardedRole}; metadata={conflict.MetadataDetail ?? "<none>"}")));
        items.AddRange(RoleIndex.MetadataFailures
            .OrderBy(failure => failure.Subject, StringComparer.Ordinal)
            .ThenBy(failure => failure.Source)
            .ThenBy(failure => failure.MetadataKey, StringComparer.Ordinal)
            .ThenBy(failure => failure.Reason, StringComparer.Ordinal)
            .Select(failure => new ArchitectureCoverageSummaryEvidenceItem(failure.Subject,
                $"metadata failure: source={failure.Source}; key={failure.MetadataKey}; reason={failure.Reason}")));
        return items;
    }

    private bool MatchesContextualConsumer(ArchitectureContextualConsumerReference consumer, Type type)
    {
        ArchitectureContextSelector selector = CreateContextualSelector(consumer.Role, consumer.Metadata);
        if (consumer.SourceRole == null)
        {
            return ArchitectureContextSelectorMatcher.Matches(selector, type, RoleIndex, sourceDescriptor: null);
        }

        ArchitectureContextSelector sourceSelector = CreateContextualSelector(
            consumer.SourceRole,
            consumer.SourceMetadata!);
        return RoleIndex.ClassifiedTypes().Any(sourceType =>
            RoleIndex.TryGetRole(sourceType, out ArchitectureTypeClassificationResult sourceDescriptor)
            && ArchitectureContextSelectorMatcher.Matches(sourceSelector, sourceType, RoleIndex, sourceDescriptor: null)
            && ArchitectureContextSelectorMatcher.Matches(selector, type, RoleIndex, sourceDescriptor));
    }

    private static ArchitectureContextSelector CreateContextualSelector(
        string role,
        IReadOnlyDictionary<string, object> metadata)
    {
        return new ArchitectureContextSelector
        {
            Role = role,
            Metadata = new Dictionary<string, object>(metadata, StringComparer.Ordinal)
        };
    }

    private static bool MatchesSemanticExclusion(
        ArchitectureCoverageExclusion exclusion,
        ArchitectureTypeClassificationResult descriptor)
    {
        return exclusion.Metadata != null
               && string.Equals(exclusion.Role, descriptor.Role, StringComparison.Ordinal)
               && exclusion.Metadata.All(entry => descriptor.Metadata.TryGetValue(entry.Key, out object? actual)
                                                  && ArchitectureMetadataValueComparer.ValuesEqual(actual, entry.Value));
    }

    private static string DescribeSemanticFact(ArchitectureTypeClassificationResult descriptor)
    {
        string metadata = descriptor.Metadata.Count == 0
            ? string.Empty
            : $" metadata={string.Join(",", descriptor.Metadata.OrderBy(entry => entry.Key, StringComparer.Ordinal).Select(entry => $"{entry.Key}={FormatSemanticMetadataValue(entry.Value)}"))}";
        return $"role={descriptor.Role}{metadata}";
    }

    private static string FormatSemanticMetadataValue(object? value)
    {
        if (value is System.Collections.IEnumerable sequence and not string)
            return $"[{string.Join(",", sequence.Cast<object?>().Select(FormatSemanticMetadataValue))}]";
        return value switch
        {
            null => "null",
            IFormattable formattable => formattable.ToString(null, System.Globalization.CultureInfo.InvariantCulture),
            _ => value.ToString() ?? string.Empty
        };
    }

    private static string DescribeConsumer(ArchitectureContextualConsumerReference consumer)
    {
        return consumer.Description;
    }
}
