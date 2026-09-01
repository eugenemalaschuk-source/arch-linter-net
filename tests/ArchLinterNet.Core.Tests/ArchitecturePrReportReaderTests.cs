using System.Text.Json;
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
            Snapshot());

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
        ArchitectureChangeReport change = ArchitectureChangeReports.Compare(Snapshot(), Snapshot());

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
        ArchitectureChangeReport change = ArchitectureChangeReports.Compare(Snapshot(), Snapshot());
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
            debtGate);
    }
}
