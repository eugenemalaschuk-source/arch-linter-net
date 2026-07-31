using ArchLinterNet.Core.Contracts.Families;
using ArchLinterNet.Core.Model;

namespace ArchLinterNet.Core.Contracts;

// Layer templates use the same typed expansion evidence for containers as source-scoped contracts.
// Split out of ArchitectureSourceSetExpander.cs to keep both files under the repository's
// file-size lint budget (make/lint.mk CS_SIZE_LINT_ERROR_LINES).
internal static partial class ArchitectureSourceSetExpander
{
    private static void RecordLayerTemplateContainerExclusions(
        ArchitectureContractDocument document,
        List<ArchitectureContractExpansion> expansions,
        string group,
        List<ArchitectureLayerTemplateContract> contracts)
    {
        foreach (ArchitectureLayerTemplateContract contract in contracts)
        {
            document.Provenance.SetValidationSubject(contract);
            string authoredId = contract.Id ?? ArchitecturePolicyDocumentLoader.NormalizeToContractId(contract.Name);
            ArchitecturePolicySourceLocation? contractLocation = document.Provenance.LocationFor(contract);

            HashSet<string> remaining = new(
                contract.Containers.Where(container => !string.IsNullOrWhiteSpace(container)), StringComparer.Ordinal);

            // A stable snapshot judged independently of the live `remaining` set, so two
            // overlapping/duplicate exclude_containers entries targeting the same container both
            // report matched instead of the second misreporting stale once the first already
            // removed it - same defect class already fixed for source exclusions above.
            HashSet<string> includedSnapshot = new(remaining, StringComparer.Ordinal);
            List<ArchitectureExpandedContractExclusion> exclusions = new();

            for (int index = 0; index < contract.ExcludeContainers.Count; index++)
            {
                string container = contract.ExcludeContainers[index];
                if (string.IsNullOrWhiteSpace(container))
                {
                    continue;
                }

                bool matched = includedSnapshot.Contains(container);
                remaining.Remove(container);
                exclusions.Add(new ArchitectureExpandedContractExclusion(container, null, container, matched)
                {
                    PolicyLocation = ExclusionLocation(document, contractLocation, "exclude_containers", index)
                });
            }

            Dictionary<string, ArchitecturePolicySourceLocation?> containerLocations = new(StringComparer.Ordinal);
            for (int index = 0; index < contract.Containers.Count; index++)
            {
                string container = contract.Containers[index];
                if (string.IsNullOrWhiteSpace(container))
                {
                    continue;
                }

                containerLocations.TryAdd(container, ExclusionLocation(document, contractLocation, "containers", index));
            }

            List<ArchitectureExpandedContractInstance> inclusions = includedSnapshot
                .OrderBy(container => container, StringComparer.Ordinal)
                .Select(container => CreateExpandedInstance(
                    $"{authoredId}/{ArchitecturePolicyDocumentLoader.NormalizeToContractId(container)}",
                    container, null, container, containerLocations.GetValueOrDefault(container), contractLocation, null))
                .ToList();
            List<ArchitectureExpandedContractInstance> instances = remaining
                .OrderBy(container => container, StringComparer.Ordinal)
                .Select(container => CreateExpandedInstance(
                    $"{authoredId}/{ArchitecturePolicyDocumentLoader.NormalizeToContractId(container)}",
                    container, null, container, containerLocations.GetValueOrDefault(container), contractLocation, null))
                .ToList();

            expansions.Add(new ArchitectureContractExpansion(
                group, authoredId, contract.Name, Array.Empty<string>(), instances)
            {
                Kind = ArchitectureContractExpansionKind.ContainerSet,
                PolicyLocation = contractLocation,
                Exclusions = exclusions,
                Inclusions = inclusions
            });
        }
    }
}
