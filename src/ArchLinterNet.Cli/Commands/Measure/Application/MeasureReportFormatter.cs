using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;

namespace ArchLinterNet.Cli.Commands.Measure.Application;

internal static class MeasureReportFormatter
{
    private const string SchemaId = "architecture-metrics-report/v1";
    private const string ApplicabilityInterpretation = "completeness transparency; not an architecture quality score";

    public static string FormatHuman(
        ArchitectureMetricMeasurementOutcome outcome,
        int maxContributors,
        bool allContributors)
    {
        ArgumentNullException.ThrowIfNull(outcome);
        StringBuilder report = new();
        report.AppendLine("Architecture metric measurement");
        report.AppendLine($"Status: {OverallStatus(outcome)}");

        if (outcome.Measurements.Count == 0)
        {
            report.Append("Measurements: none declared.");
            return report.ToString().TrimEnd();
        }

        report.AppendLine("Measurements:");
        foreach (ArchitectureMetricMeasurement measurement in outcome.Measurements)
        {
            report.AppendLine($"- id: {measurement.Id}");
            report.AppendLine($"  kind: {measurement.Kind}");
            report.AppendLine($"  native_subject: {Display(measurement.NativeSubject)}");
            report.AppendLine($"  unit: {Display(measurement.Unit)}");
            report.AppendLine($"  effective_scope: {Display(measurement.EffectiveScope)}");
            report.AppendLine($"  state: {ArchitectureApplicabilityWireNames.StateToken(measurement.State)}");
            if (measurement.IsEvaluable)
            {
                report.AppendLine($"  value: {measurement.Value!.Value}");
            }

            AppendContributors(report, measurement, maxContributors, allContributors);
            AppendApplicability(report, outcome, measurement.Id);
        }

        return report.ToString().TrimEnd();
    }

    public static string FormatJson(
        ArchitectureMetricMeasurementOutcome outcome,
        int maxContributors,
        bool allContributors)
    {
        ArgumentNullException.ThrowIfNull(outcome);
        JsonObject report = new()
        {
            ["schema_id"] = SchemaId,
            ["schema_version"] = 1,
            ["status"] = OverallStatus(outcome),
            ["measurements"] = BuildMeasurements(outcome.Measurements, maxContributors, allContributors),
        };

        if (outcome.Completion is { } completion)
        {
            ArchitectureApplicabilityProjection? projection = outcome.Applicability;
            report["assessment_completion"] = BuildAssessmentCompletion(completion, projection);
            report["applicability_findings"] = BuildApplicabilityFindings(projection);
        }

        return report.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    }

    private static string OverallStatus(ArchitectureMetricMeasurementOutcome outcome) =>
        outcome.Measurements.All(static measurement => measurement.IsEvaluable)
            ? "complete"
            : "unassessable";

    private static JsonArray BuildMeasurements(
        IReadOnlyList<ArchitectureMetricMeasurement> measurements,
        int maxContributors,
        bool allContributors)
    {
        JsonArray result = new();
        foreach (ArchitectureMetricMeasurement measurement in measurements)
        {
            JsonObject entry = new()
            {
                ["id"] = measurement.Id,
                ["kind"] = measurement.Kind,
                ["native_subject"] = measurement.NativeSubject,
                ["unit"] = measurement.Unit,
                ["effective_scope"] = measurement.EffectiveScope,
                ["state"] = ArchitectureApplicabilityWireNames.StateToken(measurement.State),
                ["value"] = measurement.IsEvaluable ? measurement.Value : null,
            };
            if (!measurement.IsEvaluable)
            {
                // The contributor universe is unknown when a metric is unassessable. Null keeps
                // a stable schema without representing withheld evidence as an observed zero.
                entry["contributor_count"] = null;
                entry["contributors"] = null;
                entry["contributors_truncated"] = null;
                result.Add(entry);
                continue;
            }

            IReadOnlyList<string> contributors = measurement.Contributors;
            IReadOnlyList<string> bounded = allContributors
                ? contributors
                : contributors.Take(maxContributors).ToArray();
            entry["contributor_count"] = measurement.ContributorCount;
            entry["contributors"] = new JsonArray(bounded
                .Select(static contributor => JsonValue.Create(contributor))
                .ToArray());
            entry["contributors_truncated"] = !allContributors && bounded.Count < contributors.Count;
            result.Add(entry);
        }

        return result;
    }

    private static JsonObject BuildAssessmentCompletion(
        ArchitectureAssessmentCompletionEvidence completion,
        ArchitectureApplicabilityProjection? projection)
    {
        JsonObject result = new()
        {
            ["state"] = completion.State.ToString().ToLowerInvariant(),
            ["reasons"] = BuildReasons(completion.Reasons),
        };
        if (projection is not null)
        {
            result["summary"] = BuildSummary(projection.Summary);
            result["controls"] = BuildControls(projection.Controls);
        }

        return result;
    }

    private static JsonObject BuildSummary(ArchitectureApplicabilitySummary summary) => new()
    {
        ["interpretation"] = ApplicabilityInterpretation,
        ["required_count"] = summary.RequiredCount,
        ["required_evaluable_count"] = summary.RequiredEvaluableCount,
        ["required_unassessable_count"] = summary.RequiredUnassessableCount,
        ["evaluable_count"] = summary.EvaluableCount,
        ["unassessable_count"] = summary.UnassessableCount,
        ["optional_count"] = summary.OptionalCount,
        ["not_applicable_count"] = summary.NotApplicableCount,
    };

    private static JsonArray BuildControls(IReadOnlyList<ArchitectureApplicabilityAssessment> controls)
    {
        JsonArray result = new();
        foreach (ArchitectureApplicabilityAssessment control in controls)
        {
            result.Add(new JsonObject
            {
                ["control_identity"] = control.ControlIdentity,
                ["family"] = control.Expected?.Family ?? control.Record?.Family,
                ["membership"] = control.Membership is { } membership
                    ? ArchitectureApplicabilityWireNames.MembershipToken(membership)
                    : null,
                ["state"] = control.State is { } state
                    ? ArchitectureApplicabilityWireNames.StateToken(state)
                    : null,
                ["validated_state"] = control.State is { } validatedState
                    ? ArchitectureApplicabilityWireNames.StateToken(validatedState)
                    : null,
                ["record_state"] = control.Record is { } record
                    ? ArchitectureApplicabilityWireNames.StateToken(record.State)
                    : null,
                ["is_integrity_valid"] = control.IsIntegrityValid,
                ["integrity_reasons"] = BuildReasons(control.IntegrityReasons),
                ["expected"] = BuildExpected(control.Expected),
                ["record"] = BuildRecord(control.Record),
            });
        }

        return result;
    }

    private static JsonObject? BuildExpected(ArchitectureApplicabilityExpectedEntry? expected) => expected is null
        ? null
        : new JsonObject
        {
            ["control_identity"] = expected.ControlIdentity,
            ["family"] = expected.Family,
            ["membership"] = ArchitectureApplicabilityWireNames.MembershipToken(expected.Membership),
            ["provenance"] = BuildProvenance(expected.Provenance),
        };

    private static JsonObject? BuildRecord(ArchitectureApplicabilityRecord? record) => record is null
        ? null
        : new JsonObject
        {
            ["control_identity"] = record.ControlIdentity,
            ["family"] = record.Family,
            ["state"] = ArchitectureApplicabilityWireNames.StateToken(record.State),
            ["reasons"] = BuildReasons(record.Reasons),
            ["provenance"] = BuildProvenance(record.Provenance),
        };

    private static JsonArray BuildReasons(IReadOnlyList<ArchitectureApplicabilityReason> reasons)
    {
        JsonArray result = new();
        foreach (ArchitectureApplicabilityReason reason in reasons)
        {
            result.Add(new JsonObject
            {
                ["code"] = reason.Code,
                ["provenance"] = BuildProvenance(reason.Provenance),
            });
        }

        return result;
    }

    private static JsonObject BuildProvenance(ArchitectureApplicabilityProvenance provenance) => new()
    {
        ["family"] = provenance.Family,
        ["control_identity"] = provenance.ControlIdentity,
        ["policy_identity"] = provenance.PolicyIdentity,
    };

    private static JsonArray BuildApplicabilityFindings(ArchitectureApplicabilityProjection? projection)
    {
        JsonArray result = new();
        if (projection is null)
        {
            return result;
        }

        foreach (ArchitectureFinding finding in projection.Findings)
        {
            result.Add(JsonSerializer.SerializeToNode(
                ArchitectureDiagnosticFormatter.FormatNormalizedFindingForJson(finding)));
        }

        return result;
    }

    private static void AppendContributors(
        StringBuilder report,
        ArchitectureMetricMeasurement measurement,
        int maxContributors,
        bool allContributors)
    {
        if (!measurement.IsEvaluable)
        {
            report.AppendLine("  contributors: unavailable (scope is unassessable)");
            return;
        }

        IReadOnlyList<string> contributors = measurement.Contributors;
        IReadOnlyList<string> bounded = allContributors
            ? contributors
            : contributors.Take(maxContributors).ToArray();
        string suffix = allContributors
            ? " (all)"
            : bounded.Count < contributors.Count
                ? $" (showing {bounded.Count} of {contributors.Count}; truncated)"
                : string.Empty;
        report.AppendLine($"  contributors: {contributors.Count}{suffix}");
        foreach (string contributor in bounded)
        {
            report.AppendLine($"    - {contributor}");
        }

        if (bounded.Count == 0)
        {
            report.AppendLine("    (none)");
        }
    }

    private static void AppendApplicability(
        StringBuilder report,
        ArchitectureMetricMeasurementOutcome outcome,
        string metricId)
    {
        ArchitectureApplicabilityProjection? projection = outcome.Applicability;
        if (projection is null)
        {
            report.AppendLine("  applicability: none");
            return;
        }

        ArchitectureApplicabilityAssessment? control = projection.Controls
            .FirstOrDefault(candidate => string.Equals(candidate.ControlIdentity, metricId, StringComparison.Ordinal));
        string completion = projection.Completion.State.ToString().ToLowerInvariant();
        if (control is null)
        {
            report.AppendLine($"  applicability: completion={completion}; control=missing");
            return;
        }

        string state = control.State is { } controlState
            ? ArchitectureApplicabilityWireNames.StateToken(controlState)
            : "unassessable";
        string reasons = control.IntegrityReasons.Count == 0 && control.Record is { } record
            ? record.Reasons.Count == 0
                ? "none"
                : string.Join(", ", record.Reasons.Select(reason => reason.Code))
            : control.IntegrityReasons.Count == 0
                ? "missing_record"
                : string.Join(", ", control.IntegrityReasons.Select(reason => reason.Code));
        report.AppendLine($"  applicability: completion={completion}; membership={control.Membership?.ToString().ToLowerInvariant() ?? "unknown"}; state={state}; reasons={reasons}");
    }

    private static string Display(string? value) => value is null ? "<none>" : value;
}
