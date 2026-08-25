using ArchLinterNet.Core.PolicyContext;
using static ArchLinterNet.Core.PolicyWeakening.ArchitecturePolicyWeakeningComparisonSupport;

namespace ArchLinterNet.Core.PolicyWeakening;

internal static class ArchitecturePolicyWeakeningEnforcementEvaluator
{
    internal static void Evaluate(
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
                    new PolicyWeakeningControlContext("strict_to_audit", ControlIdentity(baseContract), "semantic", severity),
                    ["strict"],
                    ["audit"],
                    baseContract.Provenance,
                    auditContract.Provenance,
                    Array.Empty<string>(),
                    auditContract.Reason ?? baseContract.Reason));
                continue;
            }

            findings.Add(CreateFinding(
                new PolicyWeakeningControlContext("strict_control_removed", ControlIdentity(baseContract), "semantic", severity),
                ["strict"],
                Array.Empty<string>(),
                baseContract.Provenance,
                null,
                Array.Empty<string>(),
                baseContract.Reason));
        }
    }
}
