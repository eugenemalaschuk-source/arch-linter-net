using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;
using static ArchLinterNet.Core.Validation.ArchitectureHealthReportDebtEvidenceWriter;
using static ArchLinterNet.Core.Validation.ArchitectureHealthReportFindingEvidenceWriter;

namespace ArchLinterNet.Core.Validation;

internal static class ArchitectureHealthReportReceiptEvidenceWriter
{
    internal static JsonObject BuildPolicyInventory(ArchitecturePolicyInventory inventory)
    {
        ArchitecturePolicyInventoryRules rules = inventory.Rules;
        ArchitecturePolicyInventoryIgnoreDebt debt = inventory.IgnoreDebt;
        return new JsonObject
        {
            ["schema"] = inventory.SchemaId,
            ["effective_rule_count"] = inventory.EffectiveRuleCount,
            ["rules"] = new JsonObject
            {
                ["strict"] = rules.Strict,
                ["audit"] = rules.Audit,
                ["coverage"] = rules.Coverage,
            },
            ["ignore_debt"] = new JsonObject
            {
                ["total"] = debt.Total,
                ["active"] = debt.Active,
                ["stale"] = debt.Stale,
                ["expired"] = debt.Expired,
                ["metadata_incomplete"] = debt.MetadataIncomplete,
                ["invalid"] = debt.Invalid,
            },
            ["waivers"] = BuildWaivers(inventory.Waivers),
        };
    }

    internal static JsonObject BuildWaiverLifecycle(ArchitectureWaiverLifecycleAssessment assessment) =>
        new()
        {
            ["profile"] = assessment.Profile,
            ["blocking_states"] = ToStringArray(assessment.BlockingStates),
            ["records"] = BuildWaivers(assessment.Records),
        };

    private static JsonArray BuildWaivers(IEnumerable<ArchitectureWaiverLifecycleRecord> waivers)
    {
        var result = new JsonArray();
        foreach (ArchitectureWaiverLifecycleRecord waiver in waivers
            .OrderBy(item => item.Id, StringComparer.Ordinal)
            .ThenBy(item => item.ContractGroup, StringComparer.Ordinal))
        {
            result.Add(new JsonObject
            {
                ["id"] = waiver.Id,
                ["state"] = waiver.State,
                ["contract"] = waiver.ContractName,
                ["contract_id"] = waiver.ContractId,
                ["contract_group"] = waiver.ContractGroup,
                ["source_type"] = waiver.SourceType,
                ["forbidden_reference"] = waiver.ForbiddenReference,
                ["target_fingerprint"] = waiver.TargetFingerprint,
                ["reason"] = waiver.Reason,
                ["owner"] = waiver.Owner,
                ["issue"] = waiver.Issue,
                ["introduced"] = FormatDate(waiver.Introduced),
                ["expires"] = FormatDate(waiver.Expires),
                ["evaluation_date"] = waiver.EvaluationDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                ["matches_governed_finding"] = waiver.MatchesGovernedFinding,
                ["policy_location"] = waiver.PolicyLocation is null
                    ? null
                    : JsonSerializer.SerializeToNode(
                        ArchitectureDiagnosticFormatter.FormatPolicyLocationForJson(waiver.PolicyLocation)),
            });
        }

        return result;
    }

    internal static JsonObject BuildApplicability(ArchitectureAssessmentCompletionEvidence completion)
    {
        var result = new JsonObject
        {
            ["state"] = CompletionToken(completion.State),
            ["summary"] = new JsonObject
            {
                ["required"] = completion.RequiredCount,
                ["required_evaluable"] = completion.RequiredEvaluableCount,
                ["required_unassessable"] = completion.RequiredUnassessableCount,
            },
            ["reasons"] = BuildApplicabilityReasons(completion.Reasons),
        };

        var controls = new JsonArray();
        foreach (ArchitectureApplicabilityAssessment control in completion.Controls
            .OrderBy(item => item.ControlIdentity, StringComparer.Ordinal))
        {
            controls.Add(BuildApplicabilityControl(control));
        }

        result["controls"] = controls;
        return result;
    }

    private static JsonObject BuildApplicabilityControl(ArchitectureApplicabilityAssessment control)
    {
        var result = new JsonObject
        {
            ["control_identity"] = control.ControlIdentity,
            ["membership"] = control.Membership is { } membership
                ? ArchitectureApplicabilityWireNames.MembershipToken(membership)
                : null,
            ["state"] = control.State is { } state
                ? ArchitectureApplicabilityWireNames.StateToken(state)
                : "unassessable",
            ["integrity_valid"] = control.IsIntegrityValid,
            ["integrity_reasons"] = BuildApplicabilityReasons(control.IntegrityReasons),
        };

        if (control.Expected is not null)
        {
            result["expected"] = new JsonObject
            {
                ["control_identity"] = control.Expected.ControlIdentity,
                ["family"] = control.Expected.Family,
                ["membership"] = ArchitectureApplicabilityWireNames.MembershipToken(control.Expected.Membership),
                ["provenance"] = BuildApplicabilityProvenance(control.Expected.Provenance),
            };
        }

        if (control.Record is not null)
        {
            result["record"] = BuildApplicabilityRecord(control.Record);
        }

        return result;
    }

    private static JsonObject BuildApplicabilityRecord(ArchitectureApplicabilityRecord record)
    {
        var result = new JsonObject
        {
            ["control_identity"] = record.ControlIdentity,
            ["family"] = record.Family,
            ["state"] = ArchitectureApplicabilityWireNames.StateToken(record.State),
            ["reasons"] = BuildApplicabilityReasons(record.Reasons),
            ["provenance"] = BuildApplicabilityProvenance(record.Provenance),
        };
        if (record.TopologyEvidence is not null)
        {
            result["topology_evidence"] = BuildTopologyEvidence(record.TopologyEvidence);
        }

        if (record.MetricEvidence is not null)
        {
            result["metric_evidence"] = BuildMetricEvidence(record.MetricEvidence);
        }

        return result;
    }

    private static JsonArray BuildApplicabilityReasons(IEnumerable<ArchitectureApplicabilityReason> reasons)
    {
        var result = new JsonArray();
        foreach (ArchitectureApplicabilityReason reason in reasons
            .OrderBy(item => item.Provenance.ControlIdentity, StringComparer.Ordinal)
            .ThenBy(item => item.Code, StringComparer.Ordinal)
            .ThenBy(item => item.Provenance.Family, StringComparer.Ordinal)
            .ThenBy(item => item.Provenance.PolicyIdentity, StringComparer.Ordinal))
        {
            result.Add(new JsonObject
            {
                ["code"] = reason.Code,
                ["provenance"] = BuildApplicabilityProvenance(reason.Provenance),
            });
        }

        return result;
    }

    private static JsonObject BuildApplicabilityProvenance(ArchitectureApplicabilityProvenance provenance) =>
        new()
        {
            ["family"] = provenance.Family,
            ["control_identity"] = provenance.ControlIdentity,
            ["policy_identity"] = provenance.PolicyIdentity,
        };

    private static JsonObject BuildTopologyEvidence(ArchitectureTopologyMappingEvidence evidence)
    {
        var subjects = new JsonArray();
        foreach (ArchitectureTopologySubjectEvidence subject in evidence.Subjects)
        {
            subjects.Add(new JsonObject
            {
                ["identity"] = subject.Identity,
                ["project"] = subject.Project,
                ["assembly"] = subject.Assembly,
                ["subject"] = subject.Subject,
                ["disposition"] = subject.Disposition,
                ["node_ids"] = ToStringArray(subject.NodeIds),
                ["reviewed_out_of_scope_id"] = subject.ReviewedOutOfScopeId,
            });
        }

        var relationships = new JsonArray();
        foreach (ArchitectureTopologyRelationEvidence relation in evidence.Relationships)
        {
            relationships.Add(new JsonObject
            {
                ["source_node"] = relation.SourceNode,
                ["target_node"] = relation.TargetNode,
                ["witness"] = relation.Witness,
                ["is_allowed"] = relation.IsAllowed,
            });
        }

        var staleEdges = new JsonArray();
        foreach (ArchitectureTopologyStaleEdgeEvidence edge in evidence.StaleEdges)
        {
            staleEdges.Add(new JsonObject
            {
                ["source_node"] = edge.SourceNode,
                ["target_node"] = edge.TargetNode,
            });
        }

        return new JsonObject
        {
            ["mode"] = evidence.Mode,
            ["subject_kind"] = evidence.SubjectKind,
            ["declared_component_count"] = evidence.DeclaredComponentCount,
            ["counts"] = new JsonObject
            {
                ["observed"] = evidence.ObservedSubjectCount,
                ["mapped"] = evidence.MappedSubjectCount,
                ["reviewed_out_of_scope"] = evidence.ReviewedOutOfScopeSubjectCount,
                ["unmapped"] = evidence.UnmappedSubjectCount,
                ["ambiguous"] = evidence.AmbiguousSubjectCount,
            },
            ["subjects"] = subjects,
            ["relationships"] = relationships,
            ["stale_nodes"] = ToStringArray(evidence.StaleNodes),
            ["stale_edges"] = staleEdges,
        };
    }

    private static JsonObject BuildMetricEvidence(ArchitectureMetricEvidence evidence) =>
        new()
        {
            ["metric_id"] = evidence.MetricId,
            ["kind"] = evidence.Kind,
            ["native_subject"] = evidence.NativeSubject,
            ["unit"] = evidence.Unit,
            ["effective_scope"] = evidence.EffectiveScope,
            ["value"] = evidence.Value,
            ["contributors"] = evidence.Contributors is null ? null : ToStringArray(evidence.Contributors),
        };
}
