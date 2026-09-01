using System.Text;
using System.Text.Json;
using ArchLinterNet.Core.Model;
using static ArchLinterNet.Core.Validation.ArchitectureHealthProjectionHelpers;

namespace ArchLinterNet.Core.Validation;

/// <summary>
/// Projects immutable results from existing governance authorities into architecture-health/v1.
/// It deliberately owns no policy loading, scanning, trust validation, lifecycle comparison, or
/// applicability evaluation.
/// </summary>
public static partial class ArchitectureHealthProjector
{
    private const string CurrentEvaluation = "current_evaluation";
    private const string Applicability = "applicability";
    private const string Topology = "topology";
    private const string Metrics = "metrics";
    private const string ExternalEvidence = "external_evidence";
    private const string PolicyInventory = "policy_inventory";
    private const string ReviewedFindingDebt = "reviewed_finding_debt";
    private const string NewArchitectureDebt = "new_architecture_debt";
    private const string WaiverDebt = "waiver_debt";
    private const string PolicyWeakening = "policy_weakening";
    private const string History = "history";
    private const string TopologyFamily = "declared_topology";
    private const string MetricsFamily = "metrics";
    private const string MetricBudgetsFamily = "metric_budgets";
    private const string ExternalEvidenceFamily = "external_diagnostics";

    /// <summary>Creates one deterministic health summary from canonical validation and debt-gate receipts.</summary>
    public static ArchitectureHealthSummary Project(
        IReadOnlyList<ArchitectureHealthValidationOutcome> validationOutcomes,
        ArchitectureDebtGateOutcome debtGate)
    {
        ArgumentNullException.ThrowIfNull(validationOutcomes);
        ArgumentNullException.ThrowIfNull(debtGate);
        if (validationOutcomes.Count == 0)
        {
            throw new ArgumentException("At least one validation outcome is required.", nameof(validationOutcomes));
        }

        ArchitectureHealthValidationOutcome[] orderedOutcomes = validationOutcomes
            .OrderBy(outcome => outcome.Mode, StringComparer.Ordinal)
            .ToArray();
        List<ArchitectureHealthDimension> dimensions =
        [
            ProjectCurrentEvaluation(orderedOutcomes),
            ProjectApplicability(orderedOutcomes),
            ArchitectureHealthReceiptProjector.ProjectAuditEvidence(orderedOutcomes),
            ArchitectureHealthReceiptProjector.ProjectCoverage(orderedOutcomes),
            ProjectTopology(orderedOutcomes),
            ProjectMetrics(orderedOutcomes),
            ProjectExternalEvidence(orderedOutcomes),
            ProjectPolicyInventory(orderedOutcomes),
            ProjectReviewedFindingDebt(debtGate),
            ProjectNewDebt(debtGate),
            ProjectWaiverDebt(orderedOutcomes),
            ProjectPolicyWeakening(debtGate),
            new ArchitectureHealthDimension(History, ArchitectureHealthDimensionState.NotConfigured, Array.Empty<ArchitectureHealthReason>()),
        ];

        ArchitectureHealthGate gate = ResolveGate(dimensions, debtGate);
        ArchitectureHealthState health = ResolveHealth(dimensions);
        return new ArchitectureHealthSummary(
            ArchitectureHealthSummary.CurrentSchemaId,
            gate,
            health,
            dimensions);
    }

    /// <summary>Renders the canonical summary for a human reader without recomputing dimensions.</summary>
    public static string FormatAsHuman(ArchitectureHealthSummary summary)
    {
        ArgumentNullException.ThrowIfNull(summary);
        var builder = new StringBuilder();
        builder.AppendLine("Architecture Health");
        builder.AppendLine($"Gate: {WireName(summary.Gate)}");
        builder.AppendLine($"Health: {WireName(summary.Health)}");
        builder.AppendLine("Dimensions:");
        foreach (ArchitectureHealthDimension dimension in summary.Dimensions)
        {
            builder.AppendLine($"- {dimension.Name}: {WireName(dimension.State)}");
            foreach (ArchitectureHealthReason reason in dimension.Reasons)
            {
                string source = string.IsNullOrEmpty(reason.Source) ? string.Empty : $" ({reason.Source})";
                string provenance = FormatProvenance(reason);
                builder.AppendLine($"  - {reason.Code}{source}{provenance}");
            }
        }

        return builder.ToString().TrimEnd();
    }

    /// <summary>Renders the canonical summary as the architecture-health/v1 JSON document.</summary>
    public static string FormatAsJson(ArchitectureHealthSummary summary)
    {
        ArgumentNullException.ThrowIfNull(summary);
        return JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["schema_id"] = summary.SchemaId,
            ["gate"] = WireName(summary.Gate),
            ["health"] = WireName(summary.Health),
            ["dimensions"] = summary.Dimensions.Select(dimension => new Dictionary<string, object?>
            {
                ["name"] = dimension.Name,
                ["state"] = WireName(dimension.State),
                ["reasons"] = dimension.Reasons.Select(reason => new Dictionary<string, object?>
                {
                    ["code"] = reason.Code,
                    ["source"] = reason.Source,
                    ["family"] = reason.Family,
                    ["control_identity"] = reason.ControlIdentity,
                    ["policy_identity"] = reason.PolicyIdentity,
                    ["evidence_identity"] = reason.EvidenceIdentity,
                }).ToArray(),
            }).ToArray(),
        });
    }

    private static ArchitectureHealthDimension ProjectCurrentEvaluation(
        IReadOnlyList<ArchitectureHealthValidationOutcome> outcomes)
    {
        if (outcomes.Any(outcome => outcome.Outcome.PreflightBlocked))
        {
            return Dimension(CurrentEvaluation, ArchitectureHealthDimensionState.Unassessable, "build_state_preflight");
        }

        bool strictFailed = outcomes.Any(outcome => string.Equals(outcome.Mode, "strict", StringComparison.Ordinal)
            && !outcome.Outcome.Passed);
        return strictFailed
            ? Dimension(CurrentEvaluation, ArchitectureHealthDimensionState.Fail, "strict_validation_failed")
            : Dimension(CurrentEvaluation, ArchitectureHealthDimensionState.Pass);
    }

    private static ArchitectureHealthDimension ProjectApplicability(
        IReadOnlyList<ArchitectureHealthValidationOutcome> outcomes)
    {
        ArchitectureAssessmentCompletionEvidence[] completion = outcomes
            .Select(outcome => outcome.Outcome.AssessmentCompletionEvidence)
            .Where(evidence => evidence is not null)
            .Cast<ArchitectureAssessmentCompletionEvidence>()
            .ToArray();
        if (completion.Length == 0)
        {
            return Dimension(Applicability, ArchitectureHealthDimensionState.NotConfigured);
        }

        if (completion.Any(evidence => evidence.State == ArchitectureAssessmentCompletionState.Unassessable))
        {
            return Dimension(Applicability, ArchitectureHealthDimensionState.Unassessable,
                completion.SelectMany(evidence => evidence.Reasons).Select(reason => Reason(Applicability, reason)));
        }

        return completion.Any(evidence => evidence.State == ArchitectureAssessmentCompletionState.Fail)
            ? Dimension(Applicability, ArchitectureHealthDimensionState.Fail, "applicability_failed")
            : Dimension(Applicability, ArchitectureHealthDimensionState.Pass);
    }

    private static ArchitectureHealthDimension ProjectTopology(
        IReadOnlyList<ArchitectureHealthValidationOutcome> outcomes) =>
        ProjectAuthorityFamily(
            Topology,
            [TopologyFamily],
            outcomes,
            outcome => outcome.Outcome.Violations
                .Where(violation => string.Equals(violation.ContractId, "declared-topology", StringComparison.Ordinal))
                .Select(violation => new ArchitectureHealthAuthorityFinding(
                    outcome.Mode,
                    Reason(
                        "topology_violation",
                        Topology,
                        TopologyFamily,
                        violation.ContractId ?? "declared-topology",
                        PolicyIdentity(violation.PolicyLocation),
                        EvidenceIdentity(violation)))));

    private static ArchitectureHealthDimension ProjectMetrics(
        IReadOnlyList<ArchitectureHealthValidationOutcome> outcomes) =>
        ProjectAuthorityFamily(
            Metrics,
            [MetricsFamily, MetricBudgetsFamily],
            outcomes,
            outcome => outcome.Outcome.Violations
                .Where(violation => violation.Payload is MetricBudgetPayload)
                .Select(violation =>
                {
                    var payload = (MetricBudgetPayload)violation.Payload!;
                    return new ArchitectureHealthAuthorityFinding(
                        outcome.Mode,
                        Reason(
                            "metric_budget_breach",
                            Metrics,
                            MetricBudgetsFamily,
                            violation.ContractId ?? payload.BudgetId,
                            PolicyIdentity(violation.PolicyLocation),
                            EvidenceIdentity(violation)));
                }));

    private static ArchitectureHealthDimension ProjectExternalEvidence(
        IReadOnlyList<ArchitectureHealthValidationOutcome> outcomes) =>
        ProjectAuthorityFamily(
            ExternalEvidence,
            [ExternalEvidenceFamily],
            outcomes,
            outcome => outcome.Outcome.ImportedDiagnosticFindings
                .Select(finding =>
                {
                    string evidenceIdentity = finding.Details is ImportedExternalDiagnostic imported
                        ? imported.SelectedCanonicalIdentity
                        : finding.CanonicalIdentity;
                    return new ArchitectureHealthAuthorityFinding(
                        finding.Mode ?? outcome.Mode,
                        Reason(
                            "imported_external_diagnostic",
                            ExternalEvidence,
                            ExternalEvidenceFamily,
                            finding.ContractId ?? string.Empty,
                            PolicyIdentity(finding.PolicyLocation),
                            evidenceIdentity));
                }));

    private static ArchitectureHealthDimension ProjectAuthorityFamily(
        string dimensionName,
        IReadOnlyCollection<string> families,
        IReadOnlyList<ArchitectureHealthValidationOutcome> outcomes,
        Func<ArchitectureHealthValidationOutcome, IEnumerable<ArchitectureHealthAuthorityFinding>> findingsSelector)
    {
        ArchitectureApplicabilityRecord[] records = outcomes
            .SelectMany(outcome => outcome.Outcome.ApplicabilityRecords)
            .Where(record => families.Contains(record.Family, StringComparer.Ordinal))
            .ToArray();
        ArchitectureHealthAuthorityFinding[] findings = outcomes
            .SelectMany(findingsSelector)
            .ToArray();
        if (records.Length == 0 && findings.Length == 0)
        {
            return Dimension(dimensionName, ArchitectureHealthDimensionState.NotConfigured);
        }

        if (records.Any(record => record.State == ArchitectureApplicabilityRecordState.Unassessable))
        {
            return Dimension(dimensionName, ArchitectureHealthDimensionState.Unassessable,
                records.SelectMany(record => record.Reasons).Select(reason => Reason(dimensionName, reason)));
        }

        if (findings.Any(finding => string.Equals(finding.AnalysisMode, "strict", StringComparison.Ordinal)))
        {
            return Dimension(dimensionName, ArchitectureHealthDimensionState.Fail,
                findings.Where(finding => string.Equals(finding.AnalysisMode, "strict", StringComparison.Ordinal))
                    .Select(finding => finding.Reason));
        }

        if (findings.Length > 0)
        {
            return Dimension(dimensionName, ArchitectureHealthDimensionState.Degrading,
                findings.Select(finding => finding.Reason));
        }

        return records.All(record => record.State == ArchitectureApplicabilityRecordState.NotApplicable)
            ? Dimension(dimensionName, ArchitectureHealthDimensionState.NotApplicable)
            : Dimension(dimensionName, ArchitectureHealthDimensionState.Pass);
    }

    private static ArchitectureHealthDimension ProjectPolicyInventory(
        IReadOnlyList<ArchitectureHealthValidationOutcome> outcomes)
    {
        return outcomes.Any(outcome => outcome.Outcome.PolicyInventory is null)
            ? Dimension(PolicyInventory, ArchitectureHealthDimensionState.Unassessable, "missing_policy_inventory")
            : Dimension(PolicyInventory, ArchitectureHealthDimensionState.Pass);
    }

    private static ArchitectureHealthDimension ProjectReviewedFindingDebt(ArchitectureDebtGateOutcome debtGate)
    {
        if (!debtGate.PersistentDebt.Succeeded)
        {
            return Dimension(ReviewedFindingDebt, ArchitectureHealthDimensionState.Unassessable, "baseline_verification_incomplete");
        }

        return debtGate.PersistentDebt.Frozen.Count > 0
            ? Dimension(ReviewedFindingDebt, ArchitectureHealthDimensionState.Debt, "reviewed_finding_debt")
            : Dimension(ReviewedFindingDebt, ArchitectureHealthDimensionState.Pass);
    }

    private static ArchitectureHealthDimension ProjectNewDebt(ArchitectureDebtGateOutcome debtGate)
    {
        if (!debtGate.PersistentDebt.Succeeded
            || debtGate.PersistentDebt.ConfigurationErrors.Count > 0
            || debtGate.PersistentDebt.Ambiguous.Count > 0)
        {
            return Dimension(NewArchitectureDebt, ArchitectureHealthDimensionState.Unassessable, "baseline_verification_untrusted");
        }

        if (debtGate.PersistentDebt.New.Count > 0)
        {
            return Dimension(NewArchitectureDebt, ArchitectureHealthDimensionState.Degrading, "new_baseline_debt");
        }

        // A resolved entry still makes the baseline receipt out of sync and therefore keeps the
        // gate failing until maintenance prunes it. It is an improvement to the architecture,
        // however, not new debt or a degradation of Health.
        return debtGate.PersistentDebt.Resolved.Count > 0
            ? Dimension(NewArchitectureDebt, ArchitectureHealthDimensionState.Pass, "resolved_baseline_hygiene")
            : Dimension(NewArchitectureDebt, ArchitectureHealthDimensionState.Pass);
    }

    private static ArchitectureHealthDimension ProjectWaiverDebt(
        IReadOnlyList<ArchitectureHealthValidationOutcome> outcomes)
    {
        ArchitectureWaiverLifecycleAssessment[] assessments = outcomes
            .Select(outcome => outcome.Outcome.WaiverLifecycleAssessment)
            .Where(assessment => assessment is not null)
            .Cast<ArchitectureWaiverLifecycleAssessment>()
            .ToArray();
        if (assessments.Length == 0)
        {
            return Dimension(WaiverDebt, ArchitectureHealthDimensionState.Unassessable, "missing_waiver_lifecycle_receipt");
        }

        ArchitectureWaiverLifecycleRecord[] records = assessments
            .SelectMany(assessment => assessment.Records)
            .OrderBy(record => record.Id, StringComparer.Ordinal)
            .ThenBy(record => record.ContractGroup, StringComparer.Ordinal)
            .ToArray();
        ArchitectureWaiverLifecycleRecord[] blocking = assessments
            .SelectMany(assessment => assessment.Records.Where(record =>
                assessment.BlockingStates.Contains(record.State, StringComparer.Ordinal)))
            .ToArray();
        if (blocking.Length > 0)
        {
            return Dimension(WaiverDebt, ArchitectureHealthDimensionState.Fail,
                blocking.Select(record => WaiverReason(record, "blocking_waiver_lifecycle")));
        }

        ArchitectureWaiverLifecycleRecord[] nonActive = records
            .Where(record => !string.Equals(record.State, "active", StringComparison.Ordinal))
            .ToArray();
        if (nonActive.Length > 0)
        {
            return Dimension(WaiverDebt, ArchitectureHealthDimensionState.Degrading,
                nonActive.Select(record => WaiverReason(record, "waiver_lifecycle_attention")));
        }

        return records.Length > 0
            ? Dimension(WaiverDebt, ArchitectureHealthDimensionState.Debt,
                records.Select(record => WaiverReason(record, "active_waiver_debt")))
            : Dimension(WaiverDebt, ArchitectureHealthDimensionState.Pass);
    }

    private static ArchitectureHealthDimension ProjectPolicyWeakening(ArchitectureDebtGateOutcome debtGate)
    {
        if (debtGate.PolicyWeakening is null)
        {
            return Dimension(PolicyWeakening, ArchitectureHealthDimensionState.NotConfigured);
        }

        return debtGate.PolicyWeakening.Findings.Count > 0
            ? Dimension(PolicyWeakening, ArchitectureHealthDimensionState.Degrading, "policy_weakening_detected")
            : Dimension(PolicyWeakening, ArchitectureHealthDimensionState.Pass);
    }

    private static ArchitectureHealthGate ResolveGate(
        IReadOnlyList<ArchitectureHealthDimension> dimensions,
        ArchitectureDebtGateOutcome debtGate)
    {
        if (dimensions.Any(dimension => dimension.State == ArchitectureHealthDimensionState.Unassessable)
            || !debtGate.Succeeded)
        {
            return ArchitectureHealthGate.Unassessable;
        }

        return dimensions.Any(dimension => dimension.State == ArchitectureHealthDimensionState.Fail)
            || !debtGate.Passed
            ? ArchitectureHealthGate.Fail
            : ArchitectureHealthGate.Pass;
    }

    private static ArchitectureHealthState ResolveHealth(IReadOnlyList<ArchitectureHealthDimension> dimensions)
    {
        if (dimensions.Any(dimension => dimension.State == ArchitectureHealthDimensionState.Unassessable))
        {
            return ArchitectureHealthState.Unassessable;
        }

        if (dimensions.Any(dimension => dimension.State == ArchitectureHealthDimensionState.Fail))
        {
            return ArchitectureHealthState.Failing;
        }

        if (dimensions.Any(dimension => dimension.State == ArchitectureHealthDimensionState.Degrading))
        {
            return ArchitectureHealthState.Degrading;
        }

        return dimensions.Any(dimension => dimension.State == ArchitectureHealthDimensionState.Debt)
            ? ArchitectureHealthState.Debt
            : ArchitectureHealthState.Healthy;
    }

    private static ArchitectureHealthReason WaiverReason(
        ArchitectureWaiverLifecycleRecord record,
        string code) =>
        Reason(
            code,
            WaiverDebt,
            "waiver",
            record.ContractId ?? record.Id,
            PolicyIdentity(record.PolicyLocation),
            record.Id) with
        {
            // The lifecycle state is the authoritative detail for this instance rather than a
            // synthetic aggregate. Keeping it in code makes the JSON receipt self-describing.
            Code = $"{code}:{record.State}",
        };

    private static string FormatProvenance(ArchitectureHealthReason reason)
    {
        string[] references =
        [
            FormatReference("family", reason.Family),
            FormatReference("control", reason.ControlIdentity),
            FormatReference("policy", reason.PolicyIdentity),
            FormatReference("evidence", reason.EvidenceIdentity),
        ];
        string joined = string.Join(", ", references.Where(reference => reference.Length > 0));
        return joined.Length == 0 ? string.Empty : $" [{joined}]";
    }

    private static string FormatReference(string name, string? value) => string.IsNullOrEmpty(value)
        ? string.Empty
        : $"{name}={value}";

    private sealed record ArchitectureHealthAuthorityFinding(string AnalysisMode, ArchitectureHealthReason Reason);

    private static string WireName(ArchitectureHealthGate value) => value switch
    {
        ArchitectureHealthGate.Pass => "pass",
        ArchitectureHealthGate.Fail => "fail",
        ArchitectureHealthGate.Unassessable => "unassessable",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, null),
    };

    private static string WireName(ArchitectureHealthState value) => value switch
    {
        ArchitectureHealthState.Healthy => "healthy",
        ArchitectureHealthState.Debt => "debt",
        ArchitectureHealthState.Degrading => "degrading",
        ArchitectureHealthState.Failing => "failing",
        ArchitectureHealthState.Unassessable => "unassessable",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, null),
    };

    private static string WireName(ArchitectureHealthDimensionState value) => value switch
    {
        ArchitectureHealthDimensionState.Pass => "pass",
        ArchitectureHealthDimensionState.Fail => "fail",
        ArchitectureHealthDimensionState.Debt => "debt",
        ArchitectureHealthDimensionState.Degrading => "degrading",
        ArchitectureHealthDimensionState.Unassessable => "unassessable",
        ArchitectureHealthDimensionState.NotConfigured => "not_configured",
        ArchitectureHealthDimensionState.NotApplicable => "not_applicable",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, null),
    };
}
