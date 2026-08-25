using ArchLinterNet.Core.PolicyContext;
using static ArchLinterNet.Core.PolicyWeakening.ArchitecturePolicyWeakeningComparisonSupport;

namespace ArchLinterNet.Core.PolicyWeakening;

internal static class ArchitecturePolicyWeakeningStaticScopeEvaluator
{
    private const string SemanticClassification = "semantic";

    private static readonly StringComparer _comparer = StringComparer.Ordinal;

    internal static void Evaluate(
        ArchitecturePolicyContextExport baseline,
        ArchitecturePolicyContextExport current,
        ICollection<ArchitecturePolicyWeakeningFinding> findings)
    {
        Dictionary<string, ArchitecturePolicyContextSourceSet> currentSets = current.SourceSets
            .ToDictionary(sourceSet => sourceSet.Name, _comparer);
        foreach (ArchitecturePolicyContextSourceSet baseSet in baseline.SourceSets)
        {
            currentSets.TryGetValue(baseSet.Name, out ArchitecturePolicyContextSourceSet? currentSet);
            if (!baseSet.Optional && currentSet?.Optional == true)
            {
                findings.Add(CreateFinding(
                    new PolicyWeakeningControlContext(
                        "source_set_made_optional", "source_set:" + baseSet.Name, SemanticClassification, current.Guardrails.PolicyWeakening),
                    ["required"],
                    ["optional"],
                    baseSet.Provenance,
                    currentSet.Provenance,
                    Array.Empty<string>(),
                    currentSet.Reason));
            }

            IReadOnlyList<string> currentSources = currentSet?.ResolvedSources ?? Array.Empty<string>();
            foreach (string source in baseSet.ResolvedSources.Except(currentSources, _comparer).OrderBy(value => value, _comparer))
            {
                findings.Add(CreateFinding(
                    new PolicyWeakeningControlContext(
                        "source_set_member_removed", "source_set:" + baseSet.Name, SemanticClassification, current.Guardrails.PolicyWeakening),
                    [source],
                    Array.Empty<string>(),
                    baseSet.Provenance,
                    currentSet?.Provenance,
                    Array.Empty<string>(),
                    currentSet?.Reason ?? baseSet.Reason));
            }
        }

        IReadOnlyDictionary<string, ArchitecturePolicyContextSourceExpansion> baseExpansions = ExpansionMap(baseline, "base");
        IReadOnlyDictionary<string, ArchitecturePolicyContextSourceExpansion> currentExpansions = ExpansionMap(current, "current");
        IReadOnlyDictionary<string, ArchitecturePolicyContextContract> currentContracts = ContractMap(current, null, "current");
        foreach ((string key, ArchitecturePolicyContextSourceExpansion baseExpansion) in baseExpansions)
        {
            if (!currentExpansions.TryGetValue(key, out ArchitecturePolicyContextSourceExpansion? currentExpansion))
            {
                continue;
            }

            if (!baseExpansion.OptionalEmpty && currentExpansion.OptionalEmpty)
            {
                findings.Add(CreateFinding(
                    new PolicyWeakeningControlContext(
                        "source_expansion_made_empty_tolerant",
                        "source_expansion:" + baseExpansion.AuthoredContractId,
                        SemanticClassification,
                        current.Guardrails.PolicyWeakening),
                    ["required"],
                    ["optional_empty"],
                    baseExpansion.Provenance,
                    currentExpansion.Provenance,
                    Array.Empty<string>(),
                    currentExpansion.OptionalReason));
            }

            HashSet<string> baseExclusions = baseExpansion.Exclusions
                .Where(exclusion => exclusion.Matched)
                .Select(ExpansionExclusionKey)
                .ToHashSet(_comparer);
            foreach (ArchitecturePolicyContextExpandedExclusion exclusion in currentExpansion.Exclusions
                         .Where(exclusion => exclusion.Matched && !baseExclusions.Contains(ExpansionExclusionKey(exclusion)))
                         .OrderBy(ExpansionExclusionKey, _comparer))
            {
                currentContracts.TryGetValue(ContractKeyFromExpansion(currentExpansion), out ArchitecturePolicyContextContract? contract);
                findings.Add(CreateFinding(
                    new PolicyWeakeningControlContext(
                        "source_exclusion_added",
                        "source_expansion:" + currentExpansion.AuthoredContractId,
                        SemanticClassification,
                        current.Guardrails.PolicyWeakening),
                    Array.Empty<string>(),
                    [ExpansionExclusionKey(exclusion)],
                    null,
                    exclusion.Provenance ?? currentExpansion.Provenance,
                    Array.Empty<string>(),
                    exclusion.OptionalReason ?? contract?.Reason));
            }

            HashSet<string> currentInstances = currentExpansion.Instances.Select(ExpandedInstanceKey).ToHashSet(_comparer);
            foreach (ArchitecturePolicyContextExpandedInstance instance in baseExpansion.Instances
                         .Where(instance => !currentInstances.Contains(ExpandedInstanceKey(instance)))
                         .OrderBy(ExpandedInstanceKey, _comparer))
            {
                findings.Add(CreateFinding(
                    new PolicyWeakeningControlContext(
                        "effective_source_removed",
                        "source_expansion:" + baseExpansion.AuthoredContractId,
                        SemanticClassification,
                        current.Guardrails.PolicyWeakening),
                    [ExpandedInstanceKey(instance)],
                    Array.Empty<string>(),
                    instance.Provenance ?? baseExpansion.Provenance,
                    currentExpansion.Provenance,
                    Array.Empty<string>(),
                    currentExpansion.OptionalReason));
            }
        }
    }
}
