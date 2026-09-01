using System.Text.Json;
using System.Text.Json.Nodes;
using ArchLinterNet.Core.Change;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.PolicyContext;
using ArchLinterNet.Core.PolicyWeakening;
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

    [Test]
    public void Read_ParsesCompleteDebtAndPolicyWeakeningReceipts()
    {
        ArchitectureChangeReport change = ArchitectureChangeReports.Compare(Snapshot(), Snapshot(), "run-1");

        ArchitecturePrReportInput input = ArchitecturePrReportReader.Read(
            ArchitectureHealthProjector.FormatAsJson(CreateOutcomeWithDebtEvidence()),
            ArchitectureChangeReports.FormatJson(change));

        ArchitecturePrReportDebtGateReceipt debtGate = input.Evidence!.DebtGate;

        Assert.Multiple(() =>
        {
            Assert.That(debtGate.Succeeded, Is.True);
            Assert.That(debtGate.Passed, Is.False);
            Assert.That(debtGate.Evaluation.ReusedAnalysisSnapshot, Is.True);
            Assert.That(debtGate.Evaluation.PreflightDiagnostics.Single().Kind, Is.EqualTo("build_state_preflight"));
            Assert.That(debtGate.PersistentDebt.InSync, Is.False);
            Assert.That(debtGate.PersistentDebt.Entries.Select(entry => entry.Status), Is.EquivalentTo(
                ["new", "matched", "resolved", "stale", "changed", "ambiguous", "configuration-error"]));
            Assert.That(debtGate.PersistentDebt.Entries.Single(entry => entry.Status == "new").Identity,
                Does.Contain("identity_version"));
            Assert.That(debtGate.PersistentDebt.ConfigurationViolations.Single().CanonicalIdentity, Is.Not.Empty);
            Assert.That(debtGate.PolicyWeakening, Is.Not.Null);
            Assert.That(debtGate.PolicyWeakening!.Findings.Single().BaseProvenance!.SourcePath,
                Is.EqualTo("/repo/base.yml"));
            Assert.That(debtGate.PolicyWeakening.Findings.Single().CurrentProvenance!.Role,
                Is.EqualTo("current"));
        });
    }

    [Test]
    public void FormatAsJson_UsesLegacyDebtBucketsWhenLifecycleEntriesAreAbsent()
    {
        using JsonDocument document = JsonDocument.Parse(
            ArchitectureHealthProjector.FormatAsJson(CreateOutcomeWithDebtEvidence(includeLifecycleEntries: false)));

        JsonElement entries = document.RootElement.GetProperty("report_evidence")
            .GetProperty("debt_gate")
            .GetProperty("persistent_debt")
            .GetProperty("entries");

        Assert.That(entries.EnumerateArray().Select(entry => entry.GetProperty("status").GetString()),
            Is.EquivalentTo(["new", "matched", "resolved", "ambiguous", "configuration-error"]));
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

    private static ArchitectureHealthOutcome CreateOutcomeWithDebtEvidence(bool includeLifecycleEntries = true)
    {
        ArchitectureHealthOutcome outcome = CreateOutcome();
        ArchitectureBaselineComparisonEntry Entry(string suffix) => new(
            "strict_dependencies",
            $"contract-{suffix}",
            "App.Source",
            $"Domain.Target.{suffix}",
            $"reason-{suffix}",
            new ArchitectureViolationIdentity(
                ArchitectureViolationIdentity.CurrentVersion,
                "strict",
                "dependency",
                $"contract-{suffix}",
                "App",
                "App.Source",
                null,
                "Domain",
                "Domain.Target",
                null,
                1))
        {
            Issue = $"#{suffix}",
            CurrentForbiddenReference = $"Domain.Current.{suffix}",
        };

        ArchitectureBaselineComparisonEntry newEntry = Entry("new");
        ArchitectureBaselineComparisonEntry matchedEntry = Entry("matched");
        ArchitectureBaselineComparisonEntry resolvedEntry = Entry("resolved");
        ArchitectureBaselineComparisonEntry ambiguousEntry = Entry("ambiguous");
        ArchitectureBaselineComparisonEntry configurationEntry = Entry("configuration");
        IReadOnlyList<ArchitectureBaselineComparisonEntry> newEntries = includeLifecycleEntries ? [] : [newEntry];
        IReadOnlyList<ArchitectureBaselineComparisonEntry> frozenEntries = includeLifecycleEntries ? [] : [matchedEntry];
        IReadOnlyList<ArchitectureBaselineComparisonEntry> resolvedEntries = includeLifecycleEntries ? [] : [resolvedEntry];
        IReadOnlyList<ArchitectureBaselineComparisonEntry> configurationEntries = includeLifecycleEntries ? [] : [configurationEntry];
        IReadOnlyList<ArchitectureBaselineComparisonEntry> ambiguousEntries = includeLifecycleEntries ? [] : [ambiguousEntry];
        var persistentDebt = new BaselineVerifyOutcome(
            Succeeded: true,
            InSync: false,
            New: newEntries,
            Frozen: frozenEntries,
            Resolved: resolvedEntries,
            ConfigurationErrors: configurationEntries,
            ConfigurationViolations:
            [
                new ArchitectureViolation(
                    "baseline-configuration",
                    "baseline-config",
                    "App.Source",
                    "Domain.Configuration",
                    ["Domain.Configuration.Reference"]),
            ])
        {
            Entries =
                includeLifecycleEntries
                    ?
                    [
                        new BaselineLifecycleEntry(newEntry, BaselineEntryLifecycle.New, BaselineEntryDisposition.Added),
                        new BaselineLifecycleEntry(matchedEntry, BaselineEntryLifecycle.Matched,
                            BaselineEntryDisposition.Retained),
                        new BaselineLifecycleEntry(resolvedEntry, BaselineEntryLifecycle.Resolved,
                            BaselineEntryDisposition.Removed),
                        new BaselineLifecycleEntry(Entry("stale"), BaselineEntryLifecycle.Stale),
                        new BaselineLifecycleEntry(Entry("changed"), BaselineEntryLifecycle.Changed),
                        new BaselineLifecycleEntry(ambiguousEntry, BaselineEntryLifecycle.Ambiguous),
                        new BaselineLifecycleEntry(configurationEntry, BaselineEntryLifecycle.ConfigurationError),
                    ]
                    : [],
            Ambiguous = ambiguousEntries,
        };
        var debtGate = new ArchitectureDebtGateOutcome(
            Succeeded: true,
            Passed: false,
            new ArchitectureDebtGateEvaluation(
                Completed: true,
                Mode: "strict",
                [
                    new BuildStatePreflightDiagnostic(
                        "build-state",
                        "build-state-id",
                        BuildStatePreflightState.MissingArtifact,
                        new BuildStatePreflightEvidence("/repo/App.csproj", "App")),
                ])
            {
                ReusedAnalysisSnapshot = true,
            },
            persistentDebt)
        {
            PolicyWeakeningRequested = true,
            PolicyWeakening = new ArchitecturePolicyWeakeningResult(
                ArchitecturePolicyWeakeningResult.CurrentSchemaVersion,
                "policy-weakening",
                "architecture",
                2,
                "error",
                [
                    new ArchitecturePolicyWeakeningFinding(
                        "weakening-1",
                        "broadened_waiver",
                        "strict_dependencies:contract",
                        "broadened",
                        "error",
                        ["old"],
                        ["new"],
                        new ArchitecturePolicyContextProvenance(
                            "/repo/base.yml", "/repo", "base", "rules[0]", 1),
                        new ArchitecturePolicyContextProvenance(
                            "/repo/current.yml", "/repo", "current", "rules[0]", 2),
                        ["App.Source"],
                        "reviewed"),
                ]),
        };

        return outcome with
        {
            DebtGate = debtGate,
            Summary = ArchitectureHealthProjector.Project(outcome.ValidationOutcomes, debtGate),
        };
    }
}
