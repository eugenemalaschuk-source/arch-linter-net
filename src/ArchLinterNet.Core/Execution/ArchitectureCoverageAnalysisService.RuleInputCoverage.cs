using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Families;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;
using ArchLinterNet.Core.Resolution;

namespace ArchLinterNet.Core.Execution;

internal sealed partial class ArchitectureCoverageAnalysisService
{
    internal ArchitectureCoverageSummary BuildRuleInputSummary(ArchitectureCoverageContract contract)
    {
        ArchitectureCoverageInventory inventory = BuildCoverageInventory(Document);

        Dictionary<string, ArchitectureContractDescriptor> descriptorsById = BuildAllDescriptors()
            .Where(descriptor => !string.IsNullOrEmpty(descriptor.Id))
            .GroupBy(descriptor => descriptor.Id!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        List<ArchitectureCoverageSummaryExcludedItem> excludedItems = new();
        List<ArchitectureCoverageSummaryEvidenceItem> staleItems = new();
        List<ArchitectureCoverageSummaryEvidenceItem> unknownItems = new();
        List<ArchitectureCoverageSummaryEvidenceItem> coveredItems = new();
        List<ArchitectureCoverageSummaryOptionalEmptyItem> optionalEmptyItems = new();

        foreach ((string authoredContractId, string referencedContractId) in ResolveReferencedContractIds(contract)
                     .OrderBy(pair => pair.ResolvedId, StringComparer.Ordinal))
        {
            ArchitectureCoverageExclusion? matchedExclusion = contract.Exclude
                .FirstOrDefault(exclusion => MatchesExcludedContractId(exclusion, authoredContractId, referencedContractId));

            if (matchedExclusion != null)
            {
                excludedItems.Add(new ArchitectureCoverageSummaryExcludedItem(referencedContractId, matchedExclusion.Reason));
                continue;
            }

            if (!descriptorsById.TryGetValue(referencedContractId, out ArchitectureContractDescriptor? descriptor))
            {
                continue;
            }

            IReadOnlyList<ArchitectureRuleInputReference> referencedInputs = ArchitectureRuleInputReferences.For(descriptor.Contract)
                .Distinct()
                .OrderBy(reference => reference.Input, StringComparer.Ordinal)
                .ThenBy(reference => reference.Layer, StringComparer.Ordinal)
                .ToList();

            foreach (ArchitectureRuleInputReference input in referencedInputs)
            {
                string layerName = input.Layer;
                ArchitectureLayer? layer = null;
                if (input.IsLayerReference && !Document.Layers.TryGetValue(layerName, out layer))
                {
                    unknownItems.Add(new ArchitectureCoverageSummaryEvidenceItem(referencedContractId, layerName));
                    continue;
                }

                bool matchesAnyCode = input.IsLayerReference
                    ? inventory.Namespaces.Any(entry => ArchitectureLayerResolver.MatchesNamespace(layer!, entry.Namespace))
                    : inventory.Namespaces.Any(entry => IsInsideModuleContainer(layerName, entry.Namespace));

                if (!matchesAnyCode)
                {
                    ArchitectureOptionalRuleInput? optionalInput = FindOptionalInput(contract, authoredContractId, referencedContractId, input);
                    if (optionalInput is not null)
                    {
                        optionalEmptyItems.Add(new ArchitectureCoverageSummaryOptionalEmptyItem(
                            referencedContractId + ":" + input.Input + ":" + layerName, optionalInput.Reason, layerName)
                        {
                            ContractId = referencedContractId,
                            Input = input.Input,
                            Layer = layerName,
                            PolicyLocation = optionalInput.PolicyLocation
                        });
                        continue;
                    }

                    staleItems.Add(new ArchitectureCoverageSummaryEvidenceItem(referencedContractId, layerName));
                    continue;
                }

                coveredItems.Add(new ArchitectureCoverageSummaryEvidenceItem($"{referencedContractId}:{layerName}", layerName));
            }
        }

        return new ArchitectureCoverageSummary(
            contract.Name,
            contract.Id,
            contract.Scope,
            new ArchitectureCoverageSummaryCounts(coveredItems.Count, excludedItems.Count, 0, staleItems.Count, unknownItems.Count)
            {
                OptionalEmpty = optionalEmptyItems.Count
            },
            excludedItems,
            Array.Empty<ArchitectureCoverageSummaryEvidenceItem>(),
            staleItems,
            unknownItems,
            coveredItems)
        {
            OptionalEmptyItems = optionalEmptyItems
        };
    }

    private List<ArchitectureViolation> CheckRuleInputCoverageContract(ArchitectureCoverageContract contract)
    {
        ArchitectureCoverageInventory inventory = BuildCoverageInventory(Document);

        HashSet<string> excludedContractIds = new(
            contract.Exclude
                .Where(exclusion => !string.IsNullOrWhiteSpace(exclusion.ContractId))
                .Select(exclusion => exclusion.ContractId),
            StringComparer.OrdinalIgnoreCase);

        Dictionary<string, ArchitectureContractDescriptor> descriptorsById = BuildAllDescriptors()
            .Where(descriptor => !string.IsNullOrEmpty(descriptor.Id))
            .GroupBy(descriptor => descriptor.Id!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        ArchitectureContractExecutionContext executionContext = CreateExecutionContext(contract, contract.IgnoredViolations);
        List<ArchitectureViolation> findings = new();

        foreach ((string authoredContractId, string referencedContractId) in ResolveReferencedContractIds(contract))
        {
            if (excludedContractIds.Contains(referencedContractId)
                || excludedContractIds.Contains(authoredContractId))
            {
                continue;
            }

            if (!descriptorsById.TryGetValue(referencedContractId, out ArchitectureContractDescriptor? descriptor))
            {
                continue;
            }

            AddRuleInputCoverageFindingsForContract(
                contract, authoredContractId, referencedContractId, descriptor, inventory, executionContext, findings);
        }

        _session.CollectUnmatchedIgnores(executionContext);

        return findings
            .OrderBy(f => f.SourceType, StringComparer.Ordinal)
            .ThenBy(f => f.ForbiddenReferences.First(), StringComparer.Ordinal)
            .ToList();
    }

    private void AddRuleInputCoverageFindingsForContract(
        ArchitectureCoverageContract contract,
        string authoredContractId,
        string referencedContractId,
        ArchitectureContractDescriptor descriptor,
        ArchitectureCoverageInventory inventory,
        ArchitectureContractExecutionContext executionContext,
        List<ArchitectureViolation> findings)
    {
        IReadOnlyList<ArchitectureRuleInputReference> referencedInputs = ArchitectureRuleInputReferences.For(descriptor.Contract)
            .Distinct()
            .ToList();

        foreach (ArchitectureRuleInputReference input in referencedInputs)
        {
            string layerName = input.Layer;
            ArchitectureLayer? layer = null;
            if (input.IsLayerReference && !Document.Layers.TryGetValue(layerName, out layer))
            {
                if (!executionContext.IsIgnored(
                        referencedContractId,
                        layerName,
                        targetType: layerName,
                        targetMember: layerName))
                {
                    findings.Add(new ArchitectureViolation(
                        contract.Name,
                        contract.Id,
                        referencedContractId,
                        "unresolved",
                        new[] { layerName }));
                }

                continue;
            }

            bool matchesAnyCode = input.IsLayerReference
                ? inventory.Namespaces.Any(entry => ArchitectureLayerResolver.MatchesNamespace(layer!, entry.Namespace))
                : inventory.Namespaces.Any(entry => IsInsideModuleContainer(layerName, entry.Namespace));

            if (!matchesAnyCode
                && FindOptionalInput(contract, authoredContractId, referencedContractId, input) is null
                && !executionContext.IsIgnored(
                    referencedContractId,
                    layerName,
                    targetType: layerName,
                    targetMember: layerName))
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

    // An optional_inputs entry names the id the author wrote, which for an expanded contract is the
    // authored id rather than the derived per-source instance id the rest of this pass works with.
    // Matching either keeps the declaration usable without weakening its exact input/layer identity.
    private static ArchitectureOptionalRuleInput? FindOptionalInput(
        ArchitectureCoverageContract contract,
        string authoredContractId,
        string resolvedContractId,
        ArchitectureRuleInputReference input)
    {
        return contract.OptionalInputs.FirstOrDefault(optional =>
            (string.Equals(optional.ContractId, resolvedContractId, StringComparison.OrdinalIgnoreCase)
                || string.Equals(optional.ContractId, authoredContractId, StringComparison.OrdinalIgnoreCase))
            && string.Equals(optional.Input, input.Input, StringComparison.Ordinal)
            && string.Equals(optional.Layer, input.Layer, StringComparison.Ordinal));
    }

    private static bool IsInsideModuleContainer(string container, string candidateNamespace)
    {
        return string.Equals(container, candidateNamespace, StringComparison.Ordinal)
            || candidateNamespace.StartsWith(container + ".", StringComparison.Ordinal);
    }
}
