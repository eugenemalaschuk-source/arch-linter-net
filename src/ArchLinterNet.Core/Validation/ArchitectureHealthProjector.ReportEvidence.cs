using System.Text.Json;
using System.Text.Json.Nodes;
using ArchLinterNet.Core.Model;
using static ArchLinterNet.Core.Validation.ArchitectureHealthProjector;
using static ArchLinterNet.Core.Validation.ArchitectureHealthReportDebtEvidenceWriter;
using static ArchLinterNet.Core.Validation.ArchitectureHealthReportFindingEvidenceWriter;
using static ArchLinterNet.Core.Validation.ArchitectureHealthReportReceiptEvidenceWriter;

namespace ArchLinterNet.Core.Validation;

/// <summary>
/// Additive report-evidence projection for one already-completed Architecture Health outcome.
/// The envelope is intentionally internal: the serialized schema is the artifact contract, while
/// the public API remains the existing Health outcome and formatter overload.
/// </summary>
internal sealed record ArchitectureHealthReportEvidenceEnvelope(
    int SchemaVersion,
    string Kind,
    string Gate,
    string Health,
    string? ExecutionId,
    string ConditionSet,
    IReadOnlyList<ArchitectureHealthValidationOutcome> ValidationOutcomes,
    ArchitectureDebtGateOutcome DebtGate);

internal static class ArchitectureHealthReportEvidenceWriter
{
    private const int ReportEvidenceSchemaVersion = 2;
    private const string ReportEvidenceKind = "architecture-health-report-evidence";

    /// <summary>
    /// Renders the complete architecture-health/v1 artifact, including additive canonical evidence
    /// from the same immutable validation and debt-gate receipts as <paramref name="outcome"/>.
    /// </summary>
    internal static string Format(ArchitectureHealthOutcome outcome)
    {
        ArgumentNullException.ThrowIfNull(outcome);
        JsonNode? summaryNode = JsonNode.Parse(ArchitectureHealthProjector.FormatAsJson(outcome.Summary));
        if (summaryNode is not JsonObject summary)
        {
            throw new InvalidOperationException("Architecture Health summary must be a JSON object.");
        }

        if (string.IsNullOrWhiteSpace(outcome.ExecutionContext))
        {
            return summary.ToJsonString();
        }

        ArchitectureHealthReportEvidenceEnvelope evidence = new(
            ReportEvidenceSchemaVersion,
            ReportEvidenceKind,
            WireName(outcome.Summary.Gate),
            WireName(outcome.Summary.Health),
            outcome.ExecutionContext,
            outcome.ConditionSetName,
            outcome.ValidationOutcomes.OrderBy(item => item.Mode, StringComparer.Ordinal).ToArray(),
            outcome.DebtGate);
        summary["report_evidence"] = BuildReportEvidence(evidence);
        return summary.ToJsonString();
    }

    private static JsonObject BuildReportEvidence(ArchitectureHealthReportEvidenceEnvelope evidence)
    {
        var result = new JsonObject
        {
            ["schema_version"] = evidence.SchemaVersion,
            ["kind"] = evidence.Kind,
            ["gate"] = evidence.Gate,
            ["health"] = evidence.Health,
        };

        if (!string.IsNullOrWhiteSpace(evidence.ExecutionId))
        {
            result["execution_context"] = new JsonObject
            {
                ["execution_id"] = evidence.ExecutionId,
                ["condition_set"] = evidence.ConditionSet,
            };
        }

        var outcomes = new JsonArray();
        foreach (ArchitectureHealthValidationOutcome outcome in evidence.ValidationOutcomes)
        {
            outcomes.Add(BuildValidationEvidence(outcome));
        }

        result["validation_outcomes"] = outcomes;
        result["debt_gate"] = BuildDebtGateEvidence(evidence.DebtGate);
        return result;
    }

    private static JsonObject BuildValidationEvidence(ArchitectureHealthValidationOutcome receipt)
    {
        ValidationOutcome outcome = receipt.Outcome;
        var result = new JsonObject
        {
            ["mode"] = receipt.Mode,
            ["availability"] = BuildAvailability(outcome),
            ["findings"] = BuildFindings(outcome, receipt.Mode),
            ["provenance"] = BuildProvenance(outcome),
        };

        if (outcome.PolicyInventory is not null)
        {
            result["policy_inventory"] = BuildPolicyInventory(outcome.PolicyInventory);
        }

        if (outcome.WaiverLifecycleAssessment is not null)
        {
            result["waiver_lifecycle"] = BuildWaiverLifecycle(outcome.WaiverLifecycleAssessment);
        }

        if (outcome.AssessmentCompletionEvidence is not null)
        {
            result["applicability"] = BuildApplicability(outcome.AssessmentCompletionEvidence);
        }

        if (HasExternalEvidence(outcome))
        {
            result["external_evidence"] = BuildExternalEvidence(outcome, receipt.Mode);
        }

        return result;
    }

    private static JsonObject BuildAvailability(ValidationOutcome outcome)
    {
        bool hasTopology = outcome.AssessmentCompletionEvidence?.Controls.Any(control =>
            control.Record?.TopologyEvidence is not null) == true;
        bool hasExternal = HasExternalEvidence(outcome);
        return new JsonObject
        {
            ["policy_inventory"] = outcome.PolicyInventory is null ? "unavailable" : "available",
            ["waiver_lifecycle"] = outcome.WaiverLifecycleAssessment is null ? "unavailable" : "available",
            ["applicability"] = outcome.AssessmentCompletionEvidence is null ? "unavailable" : "available",
            ["topology"] = hasTopology ? "available" : "not_configured",
            ["external_evidence"] = hasExternal ? "available" : "not_configured",
            ["findings"] = "available",
        };
    }
}
