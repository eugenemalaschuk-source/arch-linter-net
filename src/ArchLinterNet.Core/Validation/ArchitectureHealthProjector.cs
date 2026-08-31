using System.Text;
using System.Text.Json;
using ArchLinterNet.Core.Model;

namespace ArchLinterNet.Core.Validation;

/// <summary>
/// Projects immutable results from existing governance authorities into architecture-health/v1.
/// It deliberately owns no policy loading, scanning, trust validation, lifecycle comparison, or
/// applicability evaluation.
/// </summary>
public static class ArchitectureHealthProjector
{
    private const string CurrentEvaluation = "current_evaluation";
    private const string Applicability = "applicability";
    private const string Coverage = "coverage";
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
            ProjectCoverage(orderedOutcomes),
            ProjectFamily(Topology, TopologyFamily, orderedOutcomes),
            ProjectFamily(Metrics, MetricsFamily, orderedOutcomes, MetricBudgetsFamily),
            ProjectFamily(ExternalEvidence, ExternalEvidenceFamily, orderedOutcomes),
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
                builder.AppendLine($"  - {reason.Code}{source}");
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
                ["reasons"] = dimension.Reasons.Select(reason => new Dictionary<string, string>
                {
                    ["code"] = reason.Code,
                    ["source"] = reason.Source,
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
                completion.SelectMany(evidence => evidence.Reasons).Select(reason => reason.Code));
        }

        return completion.Any(evidence => evidence.State == ArchitectureAssessmentCompletionState.Fail)
            ? Dimension(Applicability, ArchitectureHealthDimensionState.Fail, "applicability_failed")
            : Dimension(Applicability, ArchitectureHealthDimensionState.Pass);
    }

    private static ArchitectureHealthDimension ProjectCoverage(
        IReadOnlyList<ArchitectureHealthValidationOutcome> outcomes)
    {
        ArchitectureHealthValidationOutcome[] configured = outcomes
            .Where(outcome => outcome.Outcome.CoverageSummaries.Count > 0
                || outcome.Outcome.CoverageFindings.Count > 0)
            .ToArray();
        if (configured.Length == 0)
        {
            return Dimension(Coverage, ArchitectureHealthDimensionState.NotConfigured);
        }

        if (configured.Any(outcome => outcome.Outcome.CoverageSummaries.Any(summary =>
            summary.Counts.Stale > 0 || summary.Counts.Unknown > 0)))
        {
            return Dimension(Coverage, ArchitectureHealthDimensionState.Unassessable, "coverage_incomplete");
        }

        return configured.Any(outcome => outcome.Outcome.CoverageFindings.Count > 0)
            ? Dimension(Coverage, ArchitectureHealthDimensionState.Fail, "coverage_failed")
            : Dimension(Coverage, ArchitectureHealthDimensionState.Pass);
    }

    private static ArchitectureHealthDimension ProjectFamily(
        string dimensionName,
        string family,
        IReadOnlyList<ArchitectureHealthValidationOutcome> outcomes,
        string? additionalFamily = null)
    {
        ArchitectureApplicabilityRecord[] records = outcomes
            .SelectMany(outcome => outcome.Outcome.ApplicabilityRecords)
            .Where(record => string.Equals(record.Family, family, StringComparison.Ordinal)
                || string.Equals(record.Family, additionalFamily, StringComparison.Ordinal))
            .ToArray();
        if (records.Length == 0)
        {
            return Dimension(dimensionName, ArchitectureHealthDimensionState.NotConfigured);
        }

        if (records.Any(record => record.State == ArchitectureApplicabilityRecordState.Unassessable))
        {
            return Dimension(dimensionName, ArchitectureHealthDimensionState.Unassessable,
                records.SelectMany(record => record.Reasons).Select(reason => reason.Code));
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

        return debtGate.PersistentDebt.New.Count > 0 || debtGate.PersistentDebt.Resolved.Count > 0
            ? Dimension(NewArchitectureDebt, ArchitectureHealthDimensionState.Degrading, "baseline_debt_changed")
            : Dimension(NewArchitectureDebt, ArchitectureHealthDimensionState.Pass);
    }

    private static ArchitectureHealthDimension ProjectWaiverDebt(
        IReadOnlyList<ArchitectureHealthValidationOutcome> outcomes)
    {
        ArchitecturePolicyInventory? inventory = outcomes
            .Select(outcome => outcome.Outcome.PolicyInventory)
            .FirstOrDefault(value => value is not null);
        if (inventory is null)
        {
            return Dimension(WaiverDebt, ArchitectureHealthDimensionState.Unassessable, "missing_policy_inventory");
        }

        if (inventory.IgnoreDebt.Invalid > 0 || inventory.IgnoreDebt.Expired > 0)
        {
            return Dimension(WaiverDebt, ArchitectureHealthDimensionState.Fail, "invalid_or_expired_waiver");
        }

        return inventory.IgnoreDebt.Total > 0
            ? Dimension(WaiverDebt, ArchitectureHealthDimensionState.Debt, "explicit_waiver_debt")
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

    private static ArchitectureHealthDimension Dimension(
        string name,
        ArchitectureHealthDimensionState state,
        params string[] reasonCodes)
    {
        return Dimension(name, state, (IEnumerable<string>)reasonCodes);
    }

    private static ArchitectureHealthDimension Dimension(
        string name,
        ArchitectureHealthDimensionState state,
        IEnumerable<string> reasonCodes)
    {
        return new ArchitectureHealthDimension(
            name,
            state,
            reasonCodes
                .Where(code => !string.IsNullOrWhiteSpace(code))
                .Distinct(StringComparer.Ordinal)
                .Select(code => new ArchitectureHealthReason(code, name))
                .ToArray());
    }

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
