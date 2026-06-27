using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Resolution;

namespace ArchLinterNet.Core.Execution;

public sealed partial class ArchitectureContractRunner
{
    public List<ArchitectureViolation> CheckCoverageContract(ArchitectureCoverageContract contract)
    {
        if (!IsContractSelected(contract.Id))
        {
            return new List<ArchitectureViolation>();
        }

        if (!string.Equals(contract.Scope, "namespace", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Coverage contract '{contract.Name}' declares unsupported scope '{contract.Scope}'. " +
                "Only scope 'namespace' is implemented right now.");
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
