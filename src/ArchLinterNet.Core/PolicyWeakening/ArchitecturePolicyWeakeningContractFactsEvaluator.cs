using ArchLinterNet.Core.PolicyContext;
using static ArchLinterNet.Core.PolicyWeakening.ArchitecturePolicyWeakeningComparisonSupport;

namespace ArchLinterNet.Core.PolicyWeakening;

internal static class ArchitecturePolicyWeakeningContractFactsEvaluator
{
    private static readonly StringComparer _comparer = StringComparer.Ordinal;

    internal static void Evaluate(
        ArchitecturePolicyContextExport baseline,
        ArchitecturePolicyContextExport current,
        string severity,
        ICollection<ArchitecturePolicyWeakeningFinding> findings)
    {
        IReadOnlyDictionary<string, ArchitecturePolicyContextContract> baseContracts = ContractMap(baseline, null, "base");
        IReadOnlyDictionary<string, ArchitecturePolicyContextContract> currentContracts = ContractMap(current, null, "current");
        foreach ((string key, ArchitecturePolicyContextContract baseContract) in baseContracts)
        {
            if (!currentContracts.TryGetValue(key, out ArchitecturePolicyContextContract? currentContract)
                || !string.Equals(baseContract.Mode, currentContract.Mode, StringComparison.Ordinal))
            {
                continue;
            }

            EvaluateInventoryAndFlagFacts(baseContract, currentContract, severity, findings);
            EvaluateTypedFactEvidence(baseContract, currentContract, severity, findings);
            CompareOptionality(baseContract, currentContract, severity, findings);
        }
    }

    private static void EvaluateInventoryAndFlagFacts(
        ArchitecturePolicyContextContract baseContract,
        ArchitecturePolicyContextContract currentContract,
        string severity,
        ICollection<ArchitecturePolicyWeakeningFinding> findings)
    {
        IReadOnlyDictionary<string, ArchitecturePolicyContextContractFact> baseFacts = FactMap(baseContract);
        IReadOnlyDictionary<string, ArchitecturePolicyContextContractFact> currentFacts = FactMap(currentContract);
        foreach (string factName in baseFacts.Keys.Concat(currentFacts.Keys).Distinct(_comparer).OrderBy(value => value, _comparer))
        {
            baseFacts.TryGetValue(factName, out ArchitecturePolicyContextContractFact? baseFact);
            currentFacts.TryGetValue(factName, out ArchitecturePolicyContextContractFact? currentFact);
            var comparison = new FactComparison(factName, baseFact, currentFact, FactValuesOrEmpty(baseFact), FactValuesOrEmpty(currentFact));

            AddProhibitionRemovedInventoryFinding(comparison, baseContract, currentContract, severity, findings);
            AddPermissionBroadenedFinding(comparison, baseContract, currentContract, severity, findings);
            AddScopeNarrowedFinding(comparison, baseContract, currentContract, severity, findings);
            AddProhibitionRemovedFlagFinding(comparison, baseContract, currentContract, severity, findings);
        }
    }

    private static void AddProhibitionRemovedInventoryFinding(
        FactComparison comparison,
        ArchitecturePolicyContextContract baseContract,
        ArchitecturePolicyContextContract currentContract,
        string severity,
        ICollection<ArchitecturePolicyWeakeningFinding> findings)
    {
        if (!IsSupportedProhibitionInventory(comparison.FactName, comparison.BaseFact, comparison.CurrentFact))
        {
            return;
        }

        string[] removed = comparison.BaseValues.Except(comparison.CurrentValues, _comparer).OrderBy(value => value, _comparer).ToArray();
        if (removed.Length == 0)
        {
            return;
        }

        findings.Add(CreateFinding(
            "prohibition_removed",
            ControlIdentity(baseContract) + ":" + comparison.FactName,
            "semantic",
            severity,
            removed,
            comparison.CurrentValues,
            baseContract.Provenance,
            currentContract.Provenance,
            Array.Empty<string>(),
            currentContract.Reason ?? baseContract.Reason));
    }

    private static void AddPermissionBroadenedFinding(
        FactComparison comparison,
        ArchitecturePolicyContextContract baseContract,
        ArchitecturePolicyContextContract currentContract,
        string severity,
        ICollection<ArchitecturePolicyWeakeningFinding> findings)
    {
        if (!IsSupportedPermissionInventory(comparison.FactName, comparison.BaseFact, comparison.CurrentFact))
        {
            return;
        }

        string[] added = comparison.CurrentValues.Except(comparison.BaseValues, _comparer).OrderBy(value => value, _comparer).ToArray();
        if (added.Length == 0)
        {
            return;
        }

        findings.Add(CreateFinding(
            "permission_broadened",
            ControlIdentity(baseContract) + ":" + comparison.FactName,
            "semantic",
            severity,
            comparison.BaseValues,
            added,
            baseContract.Provenance,
            currentContract.Provenance,
            Array.Empty<string>(),
            currentContract.Reason ?? baseContract.Reason));
    }

    private static void AddScopeNarrowedFinding(
        FactComparison comparison,
        ArchitecturePolicyContextContract baseContract,
        ArchitecturePolicyContextContract currentContract,
        string severity,
        ICollection<ArchitecturePolicyWeakeningFinding> findings)
    {
        if (!IsSupportedScopeInventory(comparison.FactName, comparison.BaseFact, comparison.CurrentFact))
        {
            return;
        }

        string[] removed = comparison.BaseValues.Except(comparison.CurrentValues, _comparer).OrderBy(value => value, _comparer).ToArray();
        if (removed.Length == 0)
        {
            return;
        }

        findings.Add(CreateFinding(
            "scope_inventory_narrowed",
            ControlIdentity(baseContract) + ":" + comparison.FactName,
            "semantic",
            severity,
            removed,
            comparison.CurrentValues,
            baseContract.Provenance,
            currentContract.Provenance,
            Array.Empty<string>(),
            currentContract.Reason ?? baseContract.Reason));
    }

    private static void AddProhibitionRemovedFlagFinding(
        FactComparison comparison,
        ArchitecturePolicyContextContract baseContract,
        ArchitecturePolicyContextContract currentContract,
        string severity,
        ICollection<ArchitecturePolicyWeakeningFinding> findings)
    {
        if (!TryGetSupportedProhibitionFlag(comparison.FactName, comparison.BaseFact, comparison.CurrentFact, out bool baselineFlag, out bool currentFlag)
            || !baselineFlag || currentFlag)
        {
            return;
        }

        findings.Add(CreateFinding(
            "prohibition_removed",
            ControlIdentity(baseContract) + ":" + comparison.FactName,
            "semantic",
            severity,
            ["true"],
            ["false"],
            baseContract.Provenance,
            currentContract.Provenance,
            Array.Empty<string>(),
            currentContract.Reason ?? baseContract.Reason));
    }

    private sealed record FactComparison(
        string FactName,
        ArchitecturePolicyContextContractFact? BaseFact,
        ArchitecturePolicyContextContractFact? CurrentFact,
        IReadOnlyList<string> BaseValues,
        IReadOnlyList<string> CurrentValues);

    private static void EvaluateTypedFactEvidence(
        ArchitecturePolicyContextContract baseContract,
        ArchitecturePolicyContextContract currentContract,
        string severity,
        ICollection<ArchitecturePolicyWeakeningFinding> findings)
    {
        IReadOnlyDictionary<string, ArchitecturePolicyContextContractFact> baseFacts = FactMap(baseContract);
        IReadOnlyDictionary<string, ArchitecturePolicyContextContractFact> currentFacts = FactMap(currentContract);
        IReadOnlyDictionary<string, string> baseFactEvidence = FactEvidenceMap(baseContract);
        IReadOnlyDictionary<string, string> currentFactEvidence = FactEvidenceMap(currentContract);
        foreach (string factName in baseFactEvidence.Keys.Concat(currentFactEvidence.Keys).Distinct(_comparer).OrderBy(value => value, _comparer))
        {
            baseFactEvidence.TryGetValue(factName, out string? baseEvidence);
            currentFactEvidence.TryGetValue(factName, out string? currentEvidence);
            baseFacts.TryGetValue(factName, out ArchitecturePolicyContextContractFact? baseFact);
            currentFacts.TryGetValue(factName, out ArchitecturePolicyContextContractFact? currentFact);
            if (IsKnownDirectionalFact(factName, baseFact, currentFact)
                || string.Equals(baseEvidence, currentEvidence, StringComparison.Ordinal))
            {
                continue;
            }

            findings.Add(CreateFinding(
                "typed_fact_impact_not_proven",
                ControlIdentity(baseContract) + ":" + factName,
                "impact_not_proven",
                severity,
                baseEvidence is null ? Array.Empty<string>() : [baseEvidence],
                currentEvidence is null ? Array.Empty<string>() : [currentEvidence],
                baseContract.Provenance,
                currentContract.Provenance,
                Array.Empty<string>(),
                "The changed typed fact has no supported directional weakening rule."));
        }
    }

    private static void CompareOptionality(
        ArchitecturePolicyContextContract baseline,
        ArchitecturePolicyContextContract current,
        string severity,
        ICollection<ArchitecturePolicyWeakeningFinding> findings)
    {
        IReadOnlyDictionary<string, bool> baseLayers = OptionalLayerMap(baseline);
        IReadOnlyDictionary<string, bool> currentLayers = OptionalLayerMap(current);
        foreach ((string layer, bool wasOptional) in baseLayers)
        {
            if (!wasOptional && currentLayers.TryGetValue(layer, out bool isOptional) && isOptional)
            {
                findings.Add(CreateFinding(
                    "required_layer_made_optional",
                    ControlIdentity(baseline) + ":layer:" + layer,
                    "semantic",
                    severity,
                    ["optional=false"],
                    ["optional=true"],
                    baseline.Provenance,
                    current.Provenance,
                    Array.Empty<string>(),
                    current.Reason ?? baseline.Reason));
            }
        }

        foreach (string optionalInput in OptionalInputKeys(current).Except(OptionalInputKeys(baseline), _comparer))
        {
            findings.Add(CreateFinding(
                "required_input_made_optional",
                ControlIdentity(baseline) + ":optional_input:" + optionalInput,
                "semantic",
                severity,
                Array.Empty<string>(),
                [optionalInput],
                baseline.Provenance,
                current.Provenance,
                Array.Empty<string>(),
                current.Reason ?? baseline.Reason));
        }
    }
}
