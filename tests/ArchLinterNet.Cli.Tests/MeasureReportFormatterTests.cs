using System.Text.Json;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;
using ArchLinterNet.Core.Validation;
using NUnit.Framework;

namespace ArchLinterNet.Cli.Tests;

[TestFixture]
public sealed class MeasureReportFormatterTests
{
    [Test]
    public void FormatJson_BoundsOrdinalContributorsAndPreservesTotalEvidence()
    {
        ArchitectureMetricMeasurementOutcome outcome = CompleteOutcome(
            new ArchitectureMetricMeasurement(
                "application-outgoing",
                "outgoing_component_count",
                "application",
                null,
                "application",
                ArchitectureApplicabilityRecordState.Evaluable,
                3,
                ["zeta", "alpha", "middle"]));

        using JsonDocument document = JsonDocument.Parse(MeasureReportFormatter.FormatJson(
            outcome, maxContributors: 2, allContributors: false));
        JsonElement[] measurements = document.RootElement.GetProperty("measurements").EnumerateArray().ToArray();
        JsonElement measurement = measurements.Single();

        Assert.Multiple(() =>
        {
            Assert.That(document.RootElement.GetProperty("schema_id").GetString(),
                Is.EqualTo("architecture-metrics-report/v1"));
            Assert.That(document.RootElement.GetProperty("schema_version").GetInt32(), Is.EqualTo(1));
            Assert.That(document.RootElement.GetProperty("status").GetString(), Is.EqualTo("complete"));
            Assert.That(measurement.GetProperty("value").GetInt32(), Is.EqualTo(3));
            Assert.That(measurement.GetProperty("contributor_count").GetInt32(), Is.EqualTo(3));
            Assert.That(measurement.GetProperty("contributors_truncated").GetBoolean(), Is.True);
            Assert.That(measurement.GetProperty("contributors").EnumerateArray()
                .Select(item => item.GetString()), Is.EqualTo(new[] { "alpha", "middle" }));
        });
    }

    [Test]
    public void FormatHuman_UnassessableValueDoesNotLookLikeValidationResult()
    {
        ArchitectureMetricMeasurement measurement = new(
            "application-outgoing",
            "outgoing_component_count",
            "application",
            null,
            "application",
            ArchitectureApplicabilityRecordState.Unassessable,
            null,
            Array.Empty<string>());
        ArchitectureMetricMeasurementOutcome outcome = new([measurement], null, null);

        string report = MeasureReportFormatter.FormatHuman(outcome, maxContributors: 20, allContributors: false);

        Assert.Multiple(() =>
        {
            Assert.That(report, Does.Contain("Architecture metric measurement"));
            Assert.That(report, Does.Contain("Status: unassessable"));
            Assert.That(report, Does.Contain("contributors: unavailable (scope is unassessable)"));
            Assert.That(report, Does.Not.Contain("Architecture validation passed"));
            Assert.That(report, Does.Not.Contain("value:"));
        });
    }

    [Test]
    public void FormatJson_UnassessableMeasurementLeavesContributorEvidenceUnknown()
    {
        ArchitectureMetricMeasurement measurement = new(
            "application-outgoing",
            "outgoing_component_count",
            "application",
            null,
            "application",
            ArchitectureApplicabilityRecordState.Unassessable,
            null,
            Array.Empty<string>());

        using JsonDocument document = JsonDocument.Parse(MeasureReportFormatter.FormatJson(
            new ArchitectureMetricMeasurementOutcome([measurement], null, null), 20, allContributors: false));
        JsonElement result = document.RootElement.GetProperty("measurements").EnumerateArray().Single();

        Assert.Multiple(() =>
        {
            Assert.That(result.GetProperty("value").ValueKind, Is.EqualTo(JsonValueKind.Null));
            Assert.That(result.GetProperty("contributor_count").ValueKind, Is.EqualTo(JsonValueKind.Null));
            Assert.That(result.GetProperty("contributors").ValueKind, Is.EqualTo(JsonValueKind.Null));
            Assert.That(result.GetProperty("contributors_truncated").ValueKind, Is.EqualTo(JsonValueKind.Null));
        });
    }

    private static ArchitectureMetricMeasurementOutcome CompleteOutcome(ArchitectureMetricMeasurement measurement)
    {
        ArchitectureApplicabilityProvenance provenance = new("metrics", measurement.Id, "test-policy");
        ArchitectureApplicabilityExpectedEntry expected = new(
            measurement.Id,
            "metrics",
            ArchitectureApplicabilityMembership.Required,
            provenance);
        ArchitectureApplicabilityRecord record = new(
            measurement.Id,
            "metrics",
            ArchitectureApplicabilityRecordState.Evaluable,
            Array.Empty<ArchitectureApplicabilityReason>(),
            provenance);
        ArchitectureAssessmentCompletionEvidence completion = ArchitectureApplicabilityEvaluator.Evaluate(
            [expected], [record], conformancePassed: true)
            ?? throw new InvalidOperationException("A required metric control must have completion evidence.");
        return new ArchitectureMetricMeasurementOutcome(
            [measurement], completion, ArchitectureApplicabilityProjector.Project(completion));
    }
}
