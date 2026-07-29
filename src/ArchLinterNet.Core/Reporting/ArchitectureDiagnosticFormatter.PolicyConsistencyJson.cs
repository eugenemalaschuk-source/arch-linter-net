using ArchLinterNet.Core.Model;

namespace ArchLinterNet.Core.Reporting;

public sealed partial class ArchitectureDiagnosticFormatter
{
    private static Dictionary<string, object?> ToPolicyConsistencyJsonObject(PolicyConsistencyDiagnostic finding)
    {
        var obj = new Dictionary<string, object?>
        {
            ["schema_version"] = ArchitectureFinding.CurrentSchemaVersion,
            ["kind"] = "policy_consistency",
            ["canonical_identity"] = ArchitectureFindingMapper.FromDiagnostic(finding).CanonicalIdentity,
            ["mode"] = null,
            ["severity"] = null,
            ["message_code"] = "policy_consistency",
            ["policy_origin"] = finding.PolicyLocation is null ? null : FormatPolicyLocationForJson(finding.PolicyLocation),
            ["source_location"] = null,
            ["baseline_state"] = null,
            ["check_kind"] = finding.CheckKind,
            ["contract"] = finding.ContractName,
            ["contract_id"] = finding.ContractId,
            ["reason"] = finding.Reason,
            ["conflicting_contract_ids"] = finding.ConflictingContractIds.ToArray(),
            ["conflicting_contract_names"] = finding.ConflictingContractNames.ToArray(),
            ["layers"] = finding.Layers.ToArray(),
            ["details"] = new Dictionary<string, object?>
            {
                ["detail_kind"] = "policy_consistency",
                ["check_kind"] = finding.CheckKind,
                ["reason"] = finding.Reason,
                ["conflicting_contract_ids"] = finding.ConflictingContractIds.ToArray(),
                ["conflicting_contract_names"] = finding.ConflictingContractNames.ToArray(),
                ["layers"] = finding.Layers.ToArray(),
                ["representative_type"] = finding.RepresentativeType,
            }
        };

        if (finding.RepresentativeType != null) obj["representative_type"] = finding.RepresentativeType;
        ApplyPolicyLocationFields(finding, obj);
        return obj;
    }
}
