using ArchLinterNet.Core.Contracts.Families;
using ArchLinterNet.Core.Contracts.PolicyImports;
using ArchLinterNet.Core.Model;

namespace ArchLinterNet.Core.Contracts;

internal static partial class ArchitectureSourceSetExpander
{
    // Effective selectors are unique; inclusions deliberately retain every authored reference.
    private sealed class SourceSelectionState
    {
        public Dictionary<string, (string? SetName, string Selector)> Selectors { get; } = new(StringComparer.Ordinal);
        public Dictionary<string, ArchitecturePolicySourceLocation?> InstanceLocations { get; } = new(StringComparer.Ordinal);
        public Dictionary<string, ArchitecturePolicySourceLocation?> SourceSetReferenceLocations { get; } = new(StringComparer.Ordinal);
        public List<string> OptionalReasons { get; } = new();
        public List<ArchitectureExpandedContractInstance> Inclusions { get; } = new();
    }

    private static SourceSelectionState ResolveIncludedSources<TContract>(
        ArchitectureContractDocument document,
        SourceSetResolver resolver,
        string group,
        TContract contract,
        string authoredId,
        ArchitecturePolicySourceLocation? contractLocation)
        where TContract : IArchitectureSourceExpandableContract
    {
        SourceSelectionState state = new();
        for (int index = 0; index < contract.Sources.Count; index++)
        {
            string source = contract.Sources[index];
            if (string.IsNullOrWhiteSpace(source)) continue;

            resolver.ValidateExplicitSource(contract.Name, group, contract.SourceKind, source);
            ArchitecturePolicySourceLocation? location = ExclusionLocation(document, contractLocation, "sources", index);
            state.Inclusions.Add(CreateExpandedInstance(authoredId, source, null, source, location, contractLocation, null));
            if (state.Selectors.TryAdd(source, (null, source))) state.InstanceLocations[source] = location;
        }

        for (int index = 0; index < contract.SourceSets.Count; index++)
        {
            ArchitectureSourceSetResolution resolution = resolver.Resolve(
                contract.Name, group, contract.SourceKind, contract.SourceSets[index]);
            ArchitecturePolicySourceLocation? referenceLocation = ExclusionLocation(
                document, contractLocation, "source_sets", index);
            if (resolution.ResolvedSources.Count == 0)
            {
                state.OptionalReasons.Add(resolution.Reason);
                state.Inclusions.Add(new ArchitectureExpandedContractInstance(authoredId, null, resolution.Name, null)
                {
                    PolicyLocation = referenceLocation,
                    AuthoredContractPolicyLocation = contractLocation,
                    SourceSetReferencePolicyLocation = referenceLocation,
                    OptionalEmpty = true,
                    OptionalReason = resolution.Reason
                });
                continue;
            }

            foreach (string source in resolution.ResolvedSources)
            {
                string selector = resolver.SelectorFor(resolution.Name, source);
                state.Inclusions.Add(CreateExpandedInstance(authoredId, source, resolution.Name, selector,
                    resolver.LocationFor(resolution.Name, source), contractLocation, referenceLocation));
                if (!state.Selectors.TryAdd(source, (resolution.Name, selector))) continue;

                state.InstanceLocations[source] = resolver.LocationFor(resolution.Name, source);
                state.SourceSetReferenceLocations[source] = referenceLocation;
            }
        }

        return state;
    }
}
