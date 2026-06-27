using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;
using ArchLinterNet.Core.Resolution;

namespace ArchLinterNet.Core.Execution;

public sealed partial class ArchitectureContractRunner
{
    public ArchitectureCoverageSummary BuildCoverageSummary(ArchitectureCoverageContract contract)
    {
        if (!IsContractSelected(contract.Id))
        {
            return new ArchitectureCoverageSummary(
                contract.Name,
                contract.Id,
                contract.Scope,
                new ArchitectureCoverageSummaryCounts(0, 0, 0, 0, 0),
                Array.Empty<ArchitectureCoverageSummaryExcludedItem>(),
                Array.Empty<ArchitectureCoverageSummaryUncoveredItem>());
        }

        if (string.Equals(contract.Scope, "rule_input", StringComparison.Ordinal))
        {
            return BuildRuleInputCoverageSummary(contract);
        }

        return BuildNamespaceCoverageSummary(contract);
    }

    private ArchitectureCoverageSummary BuildNamespaceCoverageSummary(ArchitectureCoverageContract contract)
    {
        ArchitectureCoverageInventory inventory = _session.BuildCoverageInventory(_document);

        int covered = 0;
        List<ArchitectureCoverageSummaryExcludedItem> excludedItems = new();
        List<ArchitectureCoverageSummaryUncoveredItem> uncoveredItems = new();

        foreach (ArchitectureCoverageNamespaceEntry entry in inventory.Namespaces
                     .Where(entry => contract.Roots.Any(root => MatchesNamespaceRoot(root, entry.Namespace)))
                     .OrderBy(entry => entry.Namespace, StringComparer.Ordinal))
        {
            ArchitectureCoverageExclusion? matchedExclusion = contract.Exclude
                .FirstOrDefault(exclusion => MatchesNamespaceExclusion(exclusion, entry.Namespace));

            if (matchedExclusion != null)
            {
                excludedItems.Add(new ArchitectureCoverageSummaryExcludedItem(entry.Namespace, matchedExclusion.Reason));
                continue;
            }

            if (IsCoveredByDeclaredLayers(inventory, entry.Namespace) || IsCoveredByExpandedTemplates(inventory, entry.Namespace))
            {
                covered++;
                continue;
            }

            uncoveredItems.Add(new ArchitectureCoverageSummaryUncoveredItem(entry.Namespace, entry.RepresentativeType));
        }

        return new ArchitectureCoverageSummary(
            contract.Name,
            contract.Id,
            contract.Scope,
            new ArchitectureCoverageSummaryCounts(covered, excludedItems.Count, uncoveredItems.Count, 0, 0),
            excludedItems,
            uncoveredItems);
    }

    private ArchitectureCoverageSummary BuildRuleInputCoverageSummary(ArchitectureCoverageContract contract)
    {
        ArchitectureCoverageInventory inventory = _session.BuildCoverageInventory(_document);

        Dictionary<string, ArchitectureContractDescriptor> descriptorsById = BuildAllDescriptors()
            .Where(descriptor => !string.IsNullOrEmpty(descriptor.Id))
            .GroupBy(descriptor => descriptor.Id!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        int covered = 0;
        int stale = 0;
        int unknown = 0;
        List<ArchitectureCoverageSummaryExcludedItem> excludedItems = new();
        List<ArchitectureCoverageSummaryUncoveredItem> uncoveredItems = new();

        foreach (string referencedContractId in contract.ContractIds.OrderBy(id => id, StringComparer.Ordinal))
        {
            ArchitectureCoverageExclusion? matchedExclusion = contract.Exclude
                .FirstOrDefault(exclusion =>
                    !string.IsNullOrWhiteSpace(exclusion.ContractId)
                    && string.Equals(exclusion.ContractId, referencedContractId, StringComparison.OrdinalIgnoreCase));

            if (matchedExclusion != null)
            {
                excludedItems.Add(new ArchitectureCoverageSummaryExcludedItem(referencedContractId, matchedExclusion.Reason));
                continue;
            }

            if (!descriptorsById.TryGetValue(referencedContractId, out ArchitectureContractDescriptor? descriptor))
            {
                continue;
            }

            IReadOnlyList<string> referencedLayerNames = GetReferencedLayerNames(descriptor.Contract)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToList();

            foreach (string layerName in referencedLayerNames)
            {
                if (!_document.Layers.TryGetValue(layerName, out ArchitectureLayer? layer))
                {
                    unknown++;
                    uncoveredItems.Add(new ArchitectureCoverageSummaryUncoveredItem(referencedContractId, layerName));
                    continue;
                }

                bool matchesAnyCode = inventory.Namespaces.Any(entry =>
                    ArchitectureLayerResolver.MatchesNamespace(layer, entry.Namespace));

                if (!matchesAnyCode)
                {
                    stale++;
                    uncoveredItems.Add(new ArchitectureCoverageSummaryUncoveredItem(referencedContractId, layerName));
                    continue;
                }

                covered++;
            }
        }

        return new ArchitectureCoverageSummary(
            contract.Name,
            contract.Id,
            contract.Scope,
            new ArchitectureCoverageSummaryCounts(covered, excludedItems.Count, 0, stale, unknown),
            excludedItems,
            uncoveredItems);
    }

    public List<ArchitectureViolation> CheckCoverageContract(ArchitectureCoverageContract contract)
    {
        if (!IsContractSelected(contract.Id))
        {
            return new List<ArchitectureViolation>();
        }

        if (string.Equals(contract.Scope, "rule_input", StringComparison.Ordinal))
        {
            return CheckRuleInputCoverageContract(contract);
        }

        if (!string.Equals(contract.Scope, "namespace", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Coverage contract '{contract.Name}' declares unsupported scope '{contract.Scope}'. " +
                "Only scopes 'namespace' and 'rule_input' are implemented right now.");
        }

        ArchitectureCoverageInventory inventory = _session.BuildCoverageInventory(_document);

        return inventory.Namespaces
            .Where(entry => contract.Roots.Any(root => MatchesNamespaceRoot(root, entry.Namespace)))
            .Where(entry => !contract.Exclude.Any(exclusion => MatchesNamespaceExclusion(exclusion, entry.Namespace)))
            .Where(entry => !IsCoveredByDeclaredLayers(inventory, entry.Namespace))
            .Where(entry => !IsCoveredByExpandedTemplates(inventory, entry.Namespace))
            .OrderBy(entry => entry.Namespace, StringComparer.Ordinal)
            .Select(entry => new ArchitectureViolation(
                contract.Name,
                contract.Id,
                entry.Namespace,
                "uncovered namespace",
                new[] { entry.RepresentativeType }))
            .ToList();
    }

    private List<ArchitectureViolation> CheckRuleInputCoverageContract(ArchitectureCoverageContract contract)
    {
        ArchitectureCoverageInventory inventory = _session.BuildCoverageInventory(_document);

        HashSet<string> excludedContractIds = new(
            contract.Exclude
                .Where(exclusion => !string.IsNullOrWhiteSpace(exclusion.ContractId))
                .Select(exclusion => exclusion.ContractId),
            StringComparer.OrdinalIgnoreCase);

        Dictionary<string, ArchitectureContractDescriptor> descriptorsById = BuildAllDescriptors()
            .Where(descriptor => !string.IsNullOrEmpty(descriptor.Id))
            .GroupBy(descriptor => descriptor.Id!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        List<ArchitectureViolation> findings = new();

        foreach (string referencedContractId in contract.ContractIds)
        {
            if (excludedContractIds.Contains(referencedContractId))
            {
                continue;
            }

            if (!descriptorsById.TryGetValue(referencedContractId, out ArchitectureContractDescriptor? descriptor))
            {
                continue;
            }

            IReadOnlyList<string> referencedLayerNames = GetReferencedLayerNames(descriptor.Contract)
                .Distinct(StringComparer.Ordinal)
                .ToList();

            foreach (string layerName in referencedLayerNames)
            {
                if (!_document.Layers.TryGetValue(layerName, out ArchitectureLayer? layer))
                {
                    findings.Add(new ArchitectureViolation(
                        contract.Name,
                        contract.Id,
                        referencedContractId,
                        "unresolved",
                        new[] { layerName }));
                    continue;
                }

                bool matchesAnyCode = inventory.Namespaces.Any(entry =>
                    ArchitectureLayerResolver.MatchesNamespace(layer, entry.Namespace));

                if (!matchesAnyCode)
                {
                    findings.Add(new ArchitectureViolation(
                        contract.Name,
                        contract.Id,
                        referencedContractId,
                        "empty-input",
                        new[] { layerName }));
                }
            }
        }

        return findings
            .OrderBy(f => f.SourceType, StringComparer.Ordinal)
            .ThenBy(f => f.ForbiddenReferences.First(), StringComparer.Ordinal)
            .ToList();
    }

    private static bool IsCoveredByDeclaredLayers(ArchitectureCoverageInventory inventory, string namespaceName)
    {
        return inventory.DeclaredLayers.Any(layerEntry =>
            ArchitectureLayerResolver.MatchesNamespace(layerEntry.Layer, namespaceName));
    }

    private static bool IsCoveredByExpandedTemplates(ArchitectureCoverageInventory inventory, string namespaceName)
    {
        foreach (ArchitectureLayerContract expandedTemplate in inventory.ExpandedLayerTemplates)
        {
            foreach (string layerNamespace in expandedTemplate.Layers)
            {
                if (ArchitectureLayerResolver.MatchesNamespace(
                        new ArchitectureLayer { Namespace = layerNamespace },
                        namespaceName))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool MatchesNamespaceRoot(ArchitectureCoverageRoot root, string namespaceName)
    {
        if (string.IsNullOrWhiteSpace(root.Namespace))
        {
            return false;
        }

        return ArchitectureLayerResolver.MatchesNamespace(
            new ArchitectureLayer
            {
                Namespace = root.Namespace,
                NamespaceSuffix = root.NamespaceSuffix
            },
            namespaceName);
    }

    private static bool MatchesNamespaceExclusion(ArchitectureCoverageExclusion exclusion, string namespaceName)
    {
        if (!string.IsNullOrWhiteSpace(exclusion.Namespace))
        {
            return ArchitectureLayerResolver.MatchesNamespace(
                new ArchitectureLayer
                {
                    Namespace = exclusion.Namespace,
                    NamespaceSuffix = exclusion.NamespaceSuffix
                },
                namespaceName);
        }

        if (!string.IsNullOrWhiteSpace(exclusion.NamespaceSuffix))
        {
            return string.Equals(namespaceName, exclusion.NamespaceSuffix, StringComparison.Ordinal)
                   || namespaceName.EndsWith("." + exclusion.NamespaceSuffix, StringComparison.Ordinal);
        }

        return false;
    }
}
