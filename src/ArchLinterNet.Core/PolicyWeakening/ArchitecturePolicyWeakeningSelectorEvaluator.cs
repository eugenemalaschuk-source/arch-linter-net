using ArchLinterNet.Core.PolicyContext;
using static ArchLinterNet.Core.PolicyWeakening.ArchitecturePolicyWeakeningComparisonSupport;

namespace ArchLinterNet.Core.PolicyWeakening;

internal static class ArchitecturePolicyWeakeningSelectorEvaluator
{
    private static readonly StringComparer _comparer = StringComparer.Ordinal;

    internal static void Evaluate(
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
            bool hasBaseMembership = ArchitecturePolicyWeakeningContextSupport.TryGetMembership(
                request.BaseMembership, request.BaseContext, baseContract.Family, baseContract.Id, out IReadOnlyList<string> baseSubjects);
            bool hasCurrentMembership = ArchitecturePolicyWeakeningContextSupport.TryGetMembership(
                request.CurrentMembership, request.CurrentContext, currentContract.Family, currentContract.Id, out IReadOnlyList<string> currentSubjects);
            if (hasBaseMembership && hasCurrentMembership)
            {
                string[] removedSubjects = baseSubjects.Except(currentSubjects, _comparer).OrderBy(subject => subject, _comparer).ToArray();
                if (removedSubjects.Length == 0)
                {
                    continue;
                }

                findings.Add(CreateFinding(
                    new PolicyWeakeningControlContext(
                        "selector_scope_reduced", ControlIdentity(baseContract), "semantic", request.CurrentContext.Guardrails.PolicyWeakening),
                    baseValues,
                    currentValues,
                    baseContract.Provenance,
                    currentContract.Provenance,
                    removedSubjects,
                    currentContract.Reason ?? baseContract.Reason));
                continue;
            }

            findings.Add(CreateFinding(
                new PolicyWeakeningControlContext(
                    "selector_impact_not_proven", ControlIdentity(baseContract), "impact_not_proven", request.CurrentContext.Guardrails.PolicyWeakening),
                baseValues,
                currentValues,
                baseContract.Provenance,
                currentContract.Provenance,
                Array.Empty<string>(),
                currentContract.Reason ?? baseContract.Reason));
        }
    }
}
