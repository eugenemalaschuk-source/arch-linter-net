using ArchLinterNet.Core.PolicyContext;
using static ArchLinterNet.Core.PolicyWeakening.ArchitecturePolicyWeakeningComparisonSupport;

namespace ArchLinterNet.Core.PolicyWeakening;

/// <summary>Compares effective policy contexts without loading policy files or evaluating architecture.</summary>
public static class ArchitecturePolicyWeakeningComparer
{
    private static readonly StringComparer _comparer = StringComparer.Ordinal;

    /// <summary>Compares separately loaded base and current effective policy contexts.</summary>
    public static ArchitecturePolicyWeakeningResult Compare(ArchitecturePolicyWeakeningRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.BaseContext);
        ArgumentNullException.ThrowIfNull(request.CurrentContext);
        ArchitecturePolicyWeakeningFormatter.ValidateComparableContexts(request.BaseContext, request.CurrentContext);

        IReadOnlyDictionary<string, ArchitecturePolicyContextContract> baseStrict = ContractMap(request.BaseContext, "strict", "base");
        IReadOnlyDictionary<string, ArchitecturePolicyContextContract> currentStrict = ContractMap(request.CurrentContext, "strict", "current");
        IReadOnlyDictionary<string, ArchitecturePolicyContextContract> currentAudit = ControlMap(request.CurrentContext, "audit");
        List<ArchitecturePolicyWeakeningFinding> findings = new();

        CompareEnforcement(baseStrict, currentStrict, currentAudit, request.CurrentContext.Guardrails.PolicyWeakening, findings);
        CompareAnalysisScope(request.BaseContext, request.CurrentContext, findings);
        CompareStaticScope(request.BaseContext, request.CurrentContext, findings);
        CompareContractFacts(request.BaseContext, request.CurrentContext, request.CurrentContext.Guardrails.PolicyWeakening, findings);
        CompareExceptions(request.BaseContext, request.CurrentContext, request.CurrentContext.Guardrails.PolicyWeakening, findings);
        CompareSelectors(request, findings);

        return new ArchitecturePolicyWeakeningResult(
            ArchitecturePolicyWeakeningResult.CurrentSchemaVersion,
            ArchitecturePolicyWeakeningResult.ResultKind,
            request.CurrentContext.Policy.Name,
            request.CurrentContext.Policy.Version,
            request.CurrentContext.Guardrails.PolicyWeakening,
            findings
                .GroupBy(finding => finding.Identity, _comparer)
                .Select(group => group.First())
                .OrderBy(finding => finding.Kind, _comparer)
                .ThenBy(finding => finding.ControlIdentity, _comparer)
                .ThenBy(finding => finding.Identity, _comparer)
                .ToArray());
    }

    private static void CompareEnforcement(
        IReadOnlyDictionary<string, ArchitecturePolicyContextContract> baseline,
        IReadOnlyDictionary<string, ArchitecturePolicyContextContract> currentStrict,
        IReadOnlyDictionary<string, ArchitecturePolicyContextContract> currentAudit,
        string severity,
        ICollection<ArchitecturePolicyWeakeningFinding> findings)
    {
        foreach ((string key, ArchitecturePolicyContextContract baseContract) in baseline)
        {
            if (currentStrict.ContainsKey(key))
            {
                continue;
            }

            if (currentAudit.TryGetValue(ControlKey(baseContract), out ArchitecturePolicyContextContract? auditContract))
            {
                findings.Add(CreateFinding(
                    "strict_to_audit",
                    ControlIdentity(baseContract),
                    "semantic",
                    severity,
                    ["strict"],
                    ["audit"],
                    baseContract.Provenance,
                    auditContract.Provenance,
                    Array.Empty<string>(),
                    auditContract.Reason ?? baseContract.Reason));
                continue;
            }

            findings.Add(CreateFinding(
                "strict_control_removed",
                ControlIdentity(baseContract),
                "semantic",
                severity,
                ["strict"],
                Array.Empty<string>(),
                baseContract.Provenance,
                null,
                Array.Empty<string>(),
                baseContract.Reason));
        }
    }

    private static void CompareStaticScope(
        ArchitecturePolicyContextExport baseline,
        ArchitecturePolicyContextExport current,
        ICollection<ArchitecturePolicyWeakeningFinding> findings)
    {
        IReadOnlyDictionary<string, ArchitecturePolicyContextSourceSet> currentSets = current.SourceSets
            .ToDictionary(sourceSet => sourceSet.Name, _comparer);
        foreach (ArchitecturePolicyContextSourceSet baseSet in baseline.SourceSets)
        {
            currentSets.TryGetValue(baseSet.Name, out ArchitecturePolicyContextSourceSet? currentSet);
            if (!baseSet.Optional && currentSet?.Optional == true)
            {
                findings.Add(CreateFinding(
                    "source_set_made_optional",
                    "source_set:" + baseSet.Name,
                    "semantic",
                    current.Guardrails.PolicyWeakening,
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
                    "source_set_member_removed",
                    "source_set:" + baseSet.Name,
                    "semantic",
                    current.Guardrails.PolicyWeakening,
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
                    "source_expansion_made_empty_tolerant",
                    "source_expansion:" + baseExpansion.AuthoredContractId,
                    "semantic",
                    current.Guardrails.PolicyWeakening,
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
                    "source_exclusion_added",
                    "source_expansion:" + currentExpansion.AuthoredContractId,
                    "semantic",
                    current.Guardrails.PolicyWeakening,
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
                    "effective_source_removed",
                    "source_expansion:" + baseExpansion.AuthoredContractId,
                    "semantic",
                    current.Guardrails.PolicyWeakening,
                    [ExpandedInstanceKey(instance)],
                    Array.Empty<string>(),
                    instance.Provenance ?? baseExpansion.Provenance,
                    currentExpansion.Provenance,
                    Array.Empty<string>(),
                    currentExpansion.OptionalReason));
            }
        }
    }

    private static void CompareAnalysisScope(
        ArchitecturePolicyContextExport baseline,
        ArchitecturePolicyContextExport current,
        ICollection<ArchitecturePolicyWeakeningFinding> findings)
    {
        CompareBoundedAnalysisChange("target_assemblies", baseline.Analysis.TargetAssemblies, current.Analysis.TargetAssemblies);
        CompareBoundedAnalysisChange("projects", baseline.Analysis.Projects, current.Analysis.Projects);
        CompareBoundedAnalysisChange("source_roots", baseline.Analysis.SourceRoots, current.Analysis.SourceRoots);
        CompareProjectGlobChange("project_include", baseline.Analysis.ProjectInclude, current.Analysis.ProjectInclude);
        CompareProjectGlobChange("project_exclude", baseline.Analysis.ProjectExclude, current.Analysis.ProjectExclude);

        void CompareBoundedAnalysisChange(string name, IReadOnlyList<string> baseValues, IReadOnlyList<string> currentValues)
        {
            if (baseValues.OrderBy(value => value, _comparer).SequenceEqual(currentValues.OrderBy(value => value, _comparer), _comparer))
            {
                return;
            }

            findings.Add(CreateFinding(
                "analysis_" + name + "_impact_not_proven",
                "analysis:" + name,
                "impact_not_proven",
                current.Guardrails.PolicyWeakening,
                baseValues,
                currentValues,
                null,
                null,
                Array.Empty<string>(),
                "Analysis inputs may be expanded by project discovery or scanner defaults; context artifacts do not prove their effective analysed membership."));
        }

        void CompareProjectGlobChange(string name, IReadOnlyList<string> baseValues, IReadOnlyList<string> currentValues)
        {
            if (baseValues.OrderBy(value => value, _comparer).SequenceEqual(currentValues.OrderBy(value => value, _comparer), _comparer))
            {
                return;
            }

            findings.Add(CreateFinding(
                "analysis_" + name + "_impact_not_proven",
                "analysis:" + name,
                "impact_not_proven",
                current.Guardrails.PolicyWeakening,
                baseValues,
                currentValues,
                null,
                null,
                Array.Empty<string>(),
                "Project include/exclude values are globs; context artifacts do not prove their matched project sets."));
        }
    }

    private static void CompareContractFacts(
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

            IReadOnlyDictionary<string, ArchitecturePolicyContextContractFact> baseFacts = FactMap(baseContract);
            IReadOnlyDictionary<string, ArchitecturePolicyContextContractFact> currentFacts = FactMap(currentContract);
            IReadOnlyDictionary<string, string> baseFactEvidence = FactEvidenceMap(baseContract);
            IReadOnlyDictionary<string, string> currentFactEvidence = FactEvidenceMap(currentContract);
            foreach (string factName in baseFacts.Keys.Concat(currentFacts.Keys).Distinct(_comparer).OrderBy(value => value, _comparer))
            {
                baseFacts.TryGetValue(factName, out ArchitecturePolicyContextContractFact? baseFact);
                currentFacts.TryGetValue(factName, out ArchitecturePolicyContextContractFact? currentFact);
                IReadOnlyList<string> baseValues = FactValuesOrEmpty(baseFact);
                IReadOnlyList<string> currentValues = FactValuesOrEmpty(currentFact);
                if (IsSupportedProhibitionInventory(factName, baseFact, currentFact))
                {
                    string[] removed = baseValues.Except(currentValues, _comparer).OrderBy(value => value, _comparer).ToArray();
                    if (removed.Length > 0)
                    {
                        findings.Add(CreateFinding(
                            "prohibition_removed",
                            ControlIdentity(baseContract) + ":" + factName,
                            "semantic",
                            severity,
                            removed,
                            currentValues,
                            baseContract.Provenance,
                            currentContract.Provenance,
                            Array.Empty<string>(),
                            currentContract.Reason ?? baseContract.Reason));
                    }
                }

                if (IsSupportedPermissionInventory(factName, baseFact, currentFact))
                {
                    string[] added = currentValues.Except(baseValues, _comparer).OrderBy(value => value, _comparer).ToArray();
                    if (added.Length > 0)
                    {
                        findings.Add(CreateFinding(
                            "permission_broadened",
                            ControlIdentity(baseContract) + ":" + factName,
                            "semantic",
                            severity,
                            baseValues,
                            added,
                            baseContract.Provenance,
                            currentContract.Provenance,
                            Array.Empty<string>(),
                            currentContract.Reason ?? baseContract.Reason));
                    }
                }

                if (IsSupportedScopeInventory(factName, baseFact, currentFact))
                {
                    string[] removed = baseValues.Except(currentValues, _comparer).OrderBy(value => value, _comparer).ToArray();
                    if (removed.Length > 0)
                    {
                        findings.Add(CreateFinding(
                            "scope_inventory_narrowed",
                            ControlIdentity(baseContract) + ":" + factName,
                            "semantic",
                            severity,
                            removed,
                            currentValues,
                            baseContract.Provenance,
                            currentContract.Provenance,
                            Array.Empty<string>(),
                            currentContract.Reason ?? baseContract.Reason));
                    }
                }

                if (TryGetSupportedProhibitionFlag(factName, baseFact, currentFact, out bool baselineFlag, out bool currentFlag)
                    && baselineFlag && !currentFlag)
                {
                    findings.Add(CreateFinding(
                        "prohibition_removed",
                        ControlIdentity(baseContract) + ":" + factName,
                        "semantic",
                        severity,
                        ["true"],
                        ["false"],
                        baseContract.Provenance,
                        currentContract.Provenance,
                        Array.Empty<string>(),
                        currentContract.Reason ?? baseContract.Reason));
                }
            }

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

            CompareOptionality(baseContract, currentContract, severity, findings);
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

    private static void CompareExceptions(
        ArchitecturePolicyContextExport baseline,
        ArchitecturePolicyContextExport current,
        string severity,
        ICollection<ArchitecturePolicyWeakeningFinding> findings)
    {
        HashSet<string> baseExceptions = baseline.Exceptions.Select(ExceptionKey).ToHashSet(_comparer);
        foreach (ArchitecturePolicyContextException exceptionItem in current.Exceptions
                     .Where(item => !baseExceptions.Contains(ExceptionKey(item)))
                     .OrderBy(ExceptionKey, _comparer))
        {
            if (IsUniversalException(exceptionItem))
            {
                findings.Add(CreateFinding(
                    "universal_exception_added",
                    ExceptionControl(exceptionItem),
                    "semantic",
                    severity,
                    Array.Empty<string>(),
                    [exceptionItem.Details],
                    null,
                    null,
                    Array.Empty<string>(),
                    exceptionItem.Reason));
            }
            else if (IsBroadExceptionName(exceptionItem))
            {
                findings.Add(CreateFinding(
                    "broad_exception_impact_not_proven",
                    ExceptionControl(exceptionItem),
                    "impact_not_proven",
                    severity,
                    Array.Empty<string>(),
                    [exceptionItem.Details],
                    null,
                    null,
                    Array.Empty<string>(),
                    exceptionItem.Reason));
            }
        }
    }

    private static void CompareSelectors(
        ArchitecturePolicyWeakeningRequest request,
        ICollection<ArchitecturePolicyWeakeningFinding> findings)
    {
        IReadOnlyDictionary<string, ArchitecturePolicyContextContract> baseContracts = ContractMap(request.BaseContext, null, "base");
        IReadOnlyDictionary<string, ArchitecturePolicyContextContract> currentContracts = ContractMap(request.CurrentContext, null, "current");
        foreach ((string key, ArchitecturePolicyContextContract baseContract) in baseContracts)
        {
            if (!currentContracts.TryGetValue(key, out ArchitecturePolicyContextContract? currentContract)
                || !string.Equals(baseContract.Mode, currentContract.Mode, StringComparison.Ordinal)
                || !HasFactDependentSelectorChange(baseContract, currentContract))
            {
                continue;
            }

            string[] baseValues = SelectorEvidence(baseContract);
            string[] currentValues = SelectorEvidence(currentContract);
            bool hasBaseMembership = ArchitecturePolicyWeakeningFormatter.TryGetMembership(
                request.BaseMembership, request.BaseContext, baseContract.Family, baseContract.Id, out IReadOnlyList<string> baseSubjects);
            bool hasCurrentMembership = ArchitecturePolicyWeakeningFormatter.TryGetMembership(
                request.CurrentMembership, request.CurrentContext, currentContract.Family, currentContract.Id, out IReadOnlyList<string> currentSubjects);
            if (hasBaseMembership && hasCurrentMembership)
            {
                string[] removedSubjects = baseSubjects.Except(currentSubjects, _comparer).OrderBy(subject => subject, _comparer).ToArray();
                if (removedSubjects.Length == 0)
                {
                    continue;
                }

                findings.Add(CreateFinding(
                    "selector_scope_reduced",
                    ControlIdentity(baseContract),
                    "semantic",
                    request.CurrentContext.Guardrails.PolicyWeakening,
                    baseValues,
                    currentValues,
                    baseContract.Provenance,
                    currentContract.Provenance,
                    removedSubjects,
                    currentContract.Reason ?? baseContract.Reason));
                continue;
            }

            findings.Add(CreateFinding(
                "selector_impact_not_proven",
                ControlIdentity(baseContract),
                "impact_not_proven",
                request.CurrentContext.Guardrails.PolicyWeakening,
                baseValues,
                currentValues,
                baseContract.Provenance,
                currentContract.Provenance,
                Array.Empty<string>(),
                currentContract.Reason ?? baseContract.Reason));
        }
    }

}
