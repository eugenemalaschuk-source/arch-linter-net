using ArchLinterNet.Core.Contracts.Abstractions;
using ArchLinterNet.Core.Contracts.PolicyImports;
using ArchLinterNet.Core.Model;

namespace ArchLinterNet.Core.Contracts;

// Applies source and source-set subtraction against the immutable pre-subtraction snapshot. That
// preserves correct stale-exclusion evidence when multiple exclusions name the same source.
internal static class ArchitectureSourceSetExclusionApplicator
{
    internal static List<ArchitectureExpandedContractExclusion> Apply<TContract>(
        ArchitectureContractDocument document,
        ArchitectureSourceSetExpander.SourceSetResolver resolver,
        string group,
        TContract contract,
        ArchitecturePolicySourceLocation? contractLocation,
        ArchitectureSourceSetInclusionResolver.SourceSelectionState state,
        HashSet<string> includedSnapshot)
        where TContract : IArchitectureSourceExpandableContract
    {
        List<ArchitectureExpandedContractExclusion> exclusions = new();

        for (int index = 0; index < contract.ExcludedSources.Count; index++)
        {
            string source = contract.ExcludedSources[index];
            if (string.IsNullOrWhiteSpace(source))
            {
                continue;
            }

            resolver.ValidateExplicitSource(contract.Name, group, contract.SourceKind, source);
            bool matched = includedSnapshot.Contains(source);
            state.Selectors.Remove(source);
            exclusions.Add(new ArchitectureExpandedContractExclusion(source, null, source, matched)
            {
                PolicyLocation = ArchitectureSourceSetExpander.ExclusionLocation(
                    document, contractLocation, "exclude_sources", index)
            });
        }

        for (int index = 0; index < contract.ExcludedSourceSets.Count; index++)
        {
            string setName = contract.ExcludedSourceSets[index];
            ArchitectureSourceSetResolution resolution =
                resolver.Resolve(contract.Name, group, contract.SourceKind, setName);
            ArchitecturePolicySourceLocation? exclusionLocation =
                ArchitectureSourceSetExpander.ExclusionLocation(
                    document, contractLocation, "exclude_source_sets", index);

            if (resolution.ResolvedSources.Count == 0)
            {
                exclusions.Add(new ArchitectureExpandedContractExclusion(null, resolution.Name, null, false)
                {
                    PolicyLocation = exclusionLocation,
                    OptionalEmpty = true,
                    OptionalReason = resolution.Reason
                });
                continue;
            }

            foreach (string source in resolution.ResolvedSources)
            {
                bool matched = includedSnapshot.Contains(source);
                state.Selectors.Remove(source);
                exclusions.Add(new ArchitectureExpandedContractExclusion(
                    source,
                    resolution.Name,
                    resolver.SelectorFor(resolution.Name, source),
                    matched)
                {
                    PolicyLocation = exclusionLocation
                });
            }
        }

        return exclusions;
    }

    internal static void ValidateSelectorCounts<TContract>(
        TContract contract,
        string group,
        ArchitectureSourceSetInclusionResolver.SourceSelectionState state,
        int includedCountBeforeExclusions)
        where TContract : IArchitectureSourceExpandableContract
    {
        if (state.Selectors.Count == 0 && includedCountBeforeExclusions == 0 && state.OptionalReasons.Count == 0)
        {
            throw new InvalidOperationException(
                $"Contract '{contract.Name}' in '{group}' resolved no sources from its " +
                "'sources'/'source_sets' declaration. Declare at least one usable source, or mark " +
                "the referenced set 'optional: true' with a reason if the absence is intentional.");
        }

        if (state.Selectors.Count > ArchitectureSourceSetExpander.MaxInstancesPerContract)
        {
            throw new InvalidOperationException(
                $"Contract '{contract.Name}' in '{group}' expands to {state.Selectors.Count} sources, " +
                $"which exceeds the supported limit of {ArchitectureSourceSetExpander.MaxInstancesPerContract}. " +
                "Narrow the declared globs or split the contract.");
        }
    }
}
