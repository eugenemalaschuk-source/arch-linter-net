using System.Text.Json;
using System.Text.Json.Nodes;
using ArchLinterNet.Core.Change;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;
using ArchLinterNet.Core.Validation;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class ArchitecturePrReportReaderTests
{
    [Test]
    public void ReadAndProject_ParsesHealthEvidenceAndResolvedChangeFinding()
    {
        ArchitectureHealthOutcome outcome = CreateOutcome();
        ArchitectureChangeReport change = ArchitectureChangeReports.Compare(
            Snapshot([new ArchitectureChangeFinding("resolved", "dependency", "resolved")]),
            Snapshot(),
            "run-1");

        ArchitecturePrReportProjection projection = ArchitecturePrReportProjector.ReadAndProject(
            ArchitectureHealthProjector.FormatAsJson(outcome),
            ArchitectureChangeReports.FormatJson(change));

        Assert.Multiple(() =>
        {
            Assert.That(projection.Availability, Is.EqualTo(ArchitecturePrReportAvailability.Complete));
            Assert.That(projection.Headline.Gate, Is.EqualTo(outcome.Summary.Gate));
            Assert.That(projection.Headline.Health, Is.EqualTo(outcome.Summary.Health));
            Assert.That(projection.Evidence, Is.Not.Null);
            Assert.That(projection.Evidence!.ValidationOutcomes[0].PolicyInventory, Is.Not.Null);
            Assert.That(projection.Change.ResolvedFindings.Select(finding => finding.Identity), Is.EqualTo(["resolved"]));
            Assert.That(projection.Navigation.Select(reference => reference.Authority), Does.Contain("change_finding"));
        });
    }

    [Test]
    public void ReadAndProject_LegacySummaryWithoutEvidencePreservesGateAndMarksUnavailable()
    {
        ArchitectureHealthOutcome outcome = CreateOutcome();
        ArchitectureChangeReport change = ArchitectureChangeReports.Compare(Snapshot(), Snapshot(), "run-1");

        ArchitecturePrReportProjection projection = ArchitecturePrReportProjector.ReadAndProject(
            ArchitectureHealthProjector.FormatAsJson(outcome.Summary),
            ArchitectureChangeReports.FormatJson(change));

        Assert.Multiple(() =>
        {
            Assert.That(projection.Evidence, Is.Null);
            Assert.That(projection.Availability, Is.EqualTo(ArchitecturePrReportAvailability.Unavailable));
            Assert.That(projection.Headline.Gate, Is.EqualTo(outcome.Summary.Gate));
            Assert.That(projection.Headline.Health, Is.EqualTo(outcome.Summary.Health));
        });
    }

    [Test]
    public void Read_RejectsMalformedAndUnsupportedArtifacts()
    {
        ArchitectureChangeReport change = ArchitectureChangeReports.Compare(Snapshot(), Snapshot(), "run-1");
        string changeJson = ArchitectureChangeReports.FormatJson(change);
        string healthJson = ArchitectureHealthProjector.FormatAsJson(CreateOutcome());

        Assert.Multiple(() =>
        {
            Assert.That(() => ArchitecturePrReportReader.Read("not-json", changeJson), Throws.ArgumentException);
            Assert.That(() => ArchitecturePrReportReader.Read(
                healthJson.Replace("architecture-health/v1", "architecture-health/v9", StringComparison.Ordinal),
                changeJson), Throws.ArgumentException);
            Assert.That(() => ArchitecturePrReportReader.Read(
                healthJson.Replace("architecture-health-report-evidence", "unknown-evidence", StringComparison.Ordinal),
                changeJson), Throws.ArgumentException);
            Assert.That(() => ArchitecturePrReportReader.Read(
                healthJson, changeJson.Replace("architecture-change-report", "unknown-change", StringComparison.Ordinal)),
                Throws.ArgumentException);
        });
    }

    [Test]
    public void Read_RejectsHealthAndChangeArtifactsWithDifferentExecutionContextModeOrConditionSet()
    {
        string healthJson = ArchitectureHealthProjector.FormatAsJson(CreateOutcome());
        string changeJson = ArchitectureChangeReports.FormatJson(
            ArchitectureChangeReports.Compare(Snapshot(), Snapshot(), "run-1"));

        Assert.Multiple(() =>
        {
            Assert.That(() => ArchitecturePrReportReader.Read(
                healthJson,
                changeJson.Replace("run-1", "run-2", StringComparison.Ordinal)), Throws.ArgumentException);
            Assert.That(() => ArchitecturePrReportReader.Read(
                healthJson,
                changeJson.Replace("\"mode\": \"strict\"", "\"mode\": \"audit\"", StringComparison.Ordinal)), Throws.ArgumentException);
            Assert.That(() => ArchitecturePrReportReader.Read(
                healthJson,
                changeJson.Replace("\"condition_set\": \"ci\"", "\"condition_set\": \"developer\"", StringComparison.Ordinal)), Throws.ArgumentException);
        });
    }

    [Test]
    public void Read_RejectsAvailabilityThatDoesNotMatchPayloadOrKnownWireContract()
    {
        string changeJson = ArchitectureChangeReports.FormatJson(
            ArchitectureChangeReports.Compare(Snapshot(), Snapshot(), "run-1"));

        JsonNode missingPayload = JsonNode.Parse(ArchitectureHealthProjector.FormatAsJson(CreateOutcome()))!;
        missingPayload["report_evidence"]!["validation_outcomes"]![0]!["availability"]!["external_evidence"] = "available";

        JsonNode unknownKey = JsonNode.Parse(ArchitectureHealthProjector.FormatAsJson(CreateOutcome()))!;
        unknownKey["report_evidence"]!["validation_outcomes"]![0]!["availability"]!["future_authority"] = "available";

        JsonNode unknownValue = JsonNode.Parse(ArchitectureHealthProjector.FormatAsJson(CreateOutcome()))!;
        unknownValue["report_evidence"]!["validation_outcomes"]![0]!["availability"]!["policy_inventory"] = "clean";

        Assert.Multiple(() =>
        {
            Assert.That(() => ArchitecturePrReportReader.Read(missingPayload.ToJsonString(), changeJson), Throws.ArgumentException);
            Assert.That(() => ArchitecturePrReportReader.Read(unknownKey.ToJsonString(), changeJson), Throws.ArgumentException);
            Assert.That(() => ArchitecturePrReportReader.Read(unknownValue.ToJsonString(), changeJson), Throws.ArgumentException);
        });
    }

    [Test]
    public void Read_RejectsRequestedButIncompletePolicyWeakeningReceipt()
    {
        string changeJson = ArchitectureChangeReports.FormatJson(
            ArchitectureChangeReports.Compare(Snapshot(), Snapshot(), "run-1"));
        JsonNode health = JsonNode.Parse(ArchitectureHealthProjector.FormatAsJson(CreateOutcome()))!;
        JsonNode debtGate = health["report_evidence"]!["debt_gate"]!;
        debtGate["succeeded"] = false;
        debtGate["policy_weakening"] = new JsonObject
        {
            ["requested"] = true,
            ["schema_version"] = 1,
            ["kind"] = "policy-weakening",
            ["policy_name"] = "policy",
            ["policy_version"] = 1,
            ["severity"] = "error",
            ["has_blocking_findings"] = true,
            ["findings"] = new JsonArray(),
        };

        Assert.That(() => ArchitecturePrReportReader.Read(health.ToJsonString(), changeJson), Throws.ArgumentException);
    }

    private static ArchitectureChangeSnapshot Snapshot(IReadOnlyList<ArchitectureChangeFinding>? findings = null) =>
        new(
            ArchitectureChangeSnapshot.CurrentSchemaVersion,
            "strict",
            "ci",
            [],
            findings ?? [],
            []);

    private static ArchitectureHealthOutcome CreateOutcome()
    {
        ArchitectureApplicabilityExpectedEntry expected = new(
            "control",
            "dependencies",
            ArchitectureApplicabilityMembership.Required);
        ArchitectureApplicabilityRecord record = new(
            "control",
            "dependencies",
            ArchitectureApplicabilityRecordState.Evaluable);
        var completion = new ArchitectureAssessmentCompletionEvidence(
            ArchitectureAssessmentCompletionState.Pass,
            [new ArchitectureApplicabilityAssessment(expected, record, [])],
            []);
        ValidationOutcome validation = new(
            Passed: true,
            Violations: [],
            Cycles: [],
            CoverageFindings: [],
            CoverageConfig: "off",
            UnmatchedIgnoredViolations: [],
            UnmatchedIgnoredViolationsConfig: "off",
            PolicyConsistencyFindings: [],
            PolicyConsistencyConfig: "off",
            CoverageSummaries: [],
            ClassificationConflicts: [],
            ClassificationMetadataFailures: [])
        {
            PolicyInventory = new ArchitecturePolicyInventory(
                ArchitecturePolicyInventory.CurrentSchemaId,
                0,
                new ArchitecturePolicyInventoryRules(0, 0, 0),
                new ArchitecturePolicyInventoryIgnoreDebt(0, 0, 0, 0, 0, 0),
                []),
            WaiverLifecycleAssessment = new ArchitectureWaiverLifecycleAssessment("strict", [], []),
            ApplicabilityExpectedEntries = [expected],
            ApplicabilityRecords = [record],
            AssessmentCompletionEvidence = completion,
            RepositoryRoot = "/repo",
            PolicyImportPaths = ["/repo/policy.yml"],
            ResolvedAssemblyPaths = [],
            DiscoveredProjectPaths = ["/repo/App.csproj"],
        };
        var baseline = new BaselineVerifyOutcome(true, true, [], [], [], [], []);
        var debtGate = new ArchitectureDebtGateOutcome(
            true,
            true,
            new ArchitectureDebtGateEvaluation(true, "strict", []),
            baseline);
        ArchitectureHealthSummary summary = ArchitectureHealthProjector.Project(
            [new ArchitectureHealthValidationOutcome("strict", validation)], debtGate);
        return new ArchitectureHealthOutcome(
            summary,
            [new ArchitectureHealthValidationOutcome("strict", validation)],
            debtGate)
        {
            ExecutionContext = "run-1",
            ConditionSetName = "ci",
        };
    }
}
