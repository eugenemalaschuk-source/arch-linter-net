using System.Text.Json;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Validation;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class ArchitectureHealthReportEvidenceTests
{
    [Test]
    public void FormatAsJson_OutcomeAddsVersionedEvidenceWithoutChangingSummaryFields()
    {
        ArchitectureHealthOutcome outcome = CreateOutcome(includeInventory: true);

        using JsonDocument document = JsonDocument.Parse(ArchitectureHealthProjector.FormatAsJson(outcome));
        JsonElement root = document.RootElement;
        JsonElement evidence = root.GetProperty("report_evidence");
        JsonElement receipt = evidence.GetProperty("validation_outcomes")[0];

        Assert.Multiple(() =>
        {
            Assert.That(root.GetProperty("schema_id").GetString(), Is.EqualTo(ArchitectureHealthSummary.CurrentSchemaId));
            Assert.That(root.GetProperty("gate").GetString(), Is.EqualTo(outcome.Summary.Gate.ToString().ToLowerInvariant()));
            Assert.That(root.GetProperty("health").GetString(), Is.EqualTo(outcome.Summary.Health.ToString().ToLowerInvariant()));
            Assert.That(evidence.GetProperty("schema_version").GetInt32(), Is.EqualTo(2));
            Assert.That(evidence.GetProperty("kind").GetString(), Is.EqualTo("architecture-health-report-evidence"));
            Assert.That(evidence.GetProperty("execution_context").GetProperty("execution_id").GetString(), Is.EqualTo("run"));
            Assert.That(evidence.GetProperty("execution_context").GetProperty("condition_set").GetString(), Is.EqualTo("ci"));
            Assert.That(evidence.GetProperty("gate").GetString(), Is.EqualTo(root.GetProperty("gate").GetString()));
            Assert.That(evidence.GetProperty("health").GetString(), Is.EqualTo(root.GetProperty("health").GetString()));
            Assert.That(receipt.GetProperty("policy_inventory").GetProperty("effective_rule_count").GetInt32(), Is.EqualTo(3));
            Assert.That(receipt.GetProperty("waiver_lifecycle").GetProperty("records").GetArrayLength(), Is.EqualTo(1));
            Assert.That(receipt.GetProperty("applicability").GetProperty("controls").GetArrayLength(), Is.EqualTo(1));
            Assert.That(receipt.GetProperty("findings").GetArrayLength(), Is.GreaterThan(0));
            Assert.That(receipt.GetProperty("provenance").GetProperty("repository_root").GetString(), Is.EqualTo("/repo"));
        });
    }

    [Test]
    public void FormatAsJson_OutcomeRetainsApplicabilityTopologyFindingAndDebtReceipts()
    {
        ArchitectureHealthOutcome outcome = CreateOutcome(includeInventory: true);

        using JsonDocument document = JsonDocument.Parse(ArchitectureHealthProjector.FormatAsJson(outcome));
        JsonElement receipt = document.RootElement.GetProperty("report_evidence")
            .GetProperty("validation_outcomes")[0];
        JsonElement control = receipt.GetProperty("applicability").GetProperty("controls")[0];
        JsonElement topology = control.GetProperty("record").GetProperty("topology_evidence");

        Assert.Multiple(() =>
        {
            Assert.That(topology.GetProperty("declared_component_count").GetInt32(), Is.EqualTo(2));
            Assert.That(topology.GetProperty("counts").GetProperty("mapped").GetInt32(), Is.EqualTo(1));
            Assert.That(receipt.GetProperty("findings")[0].GetProperty("canonical_identity").GetString(), Is.Not.Empty);
            Assert.That(document.RootElement.GetProperty("report_evidence").GetProperty("debt_gate")
                .GetProperty("persistent_debt").GetProperty("in_sync").GetBoolean(), Is.True);
        });
    }

    [Test]
    public void FormatAsJson_MissingCompatibilityReceiptIsUnavailableAndNotAZeroInventory()
    {
        ArchitectureHealthOutcome outcome = CreateOutcome(includeInventory: false);

        using JsonDocument document = JsonDocument.Parse(ArchitectureHealthProjector.FormatAsJson(outcome));
        JsonElement receipt = document.RootElement.GetProperty("report_evidence")
            .GetProperty("validation_outcomes")[0];

        Assert.Multiple(() =>
        {
            Assert.That(receipt.TryGetProperty("policy_inventory", out _), Is.False);
            Assert.That(receipt.GetProperty("availability").GetProperty("policy_inventory").GetString(),
                Is.EqualTo("unavailable"));
        });
    }

    [Test]
    public void FormatAsJson_RetainsCanonicalExternalEvidenceTrustStateAndLogicalIdentity()
    {
        ArchitectureHealthOutcome outcome = CreateOutcome(
            includeInventory: true,
            externalTrustReceipts:
            [
                TrustReceipt("external.current", SarifEvidenceTrustStatus.Valid, "current", 0),
                TrustReceipt("external.previous", SarifEvidenceTrustStatus.WrongRevision, "previous", 4),
            ]);

        using JsonDocument document = JsonDocument.Parse(ArchitectureHealthProjector.FormatAsJson(outcome));
        JsonElement trustReceipts = document.RootElement.GetProperty("report_evidence")
            .GetProperty("validation_outcomes")[0]
            .GetProperty("external_evidence")
            .GetProperty("trust_receipts");
        JsonElement current = trustReceipts.EnumerateArray().Single(item =>
            item.GetProperty("logical_id").GetString() == "external.current");
        JsonElement previous = trustReceipts.EnumerateArray().Single(item =>
            item.GetProperty("logical_id").GetString() == "external.previous");

        Assert.Multiple(() =>
        {
            Assert.That(current.GetProperty("state").GetString(), Is.EqualTo("current"));
            Assert.That(current.GetProperty("trust_status").GetString(), Is.EqualTo("valid"));
            Assert.That(current.GetProperty("result_count").GetInt32(), Is.EqualTo(0));
            Assert.That(previous.GetProperty("state").GetString(), Is.EqualTo("stale"));
            Assert.That(previous.GetProperty("trust_status").GetString(), Is.EqualTo("wrong_revision"));
            Assert.That(previous.GetProperty("context").GetProperty("revision").GetString(), Is.EqualTo("previous"));
        });
    }

    [Test]
    public void FormatAsJson_OrdersValidationReceiptsIndependentlyOfInputOrder()
    {
        ArchitectureHealthOutcome strict = CreateOutcome(includeInventory: true, mode: "strict");
        ArchitectureHealthOutcome audit = CreateOutcome(includeInventory: true, mode: "audit");
        ArchitectureHealthOutcome first = strict with
        {
            ValidationOutcomes = [audit.ValidationOutcomes[0], strict.ValidationOutcomes[0]],
        };
        ArchitectureHealthOutcome second = strict with
        {
            ValidationOutcomes = [strict.ValidationOutcomes[0], audit.ValidationOutcomes[0]],
        };

        Assert.That(ArchitectureHealthProjector.FormatAsJson(first),
            Is.EqualTo(ArchitectureHealthProjector.FormatAsJson(second)));
    }

    private static ArchitectureHealthOutcome CreateOutcome(
        bool includeInventory,
        string mode = "strict",
        IReadOnlyList<SarifEvidenceReadResult>? externalTrustReceipts = null)
    {
        ArchitecturePolicyInventory? inventory = includeInventory
            ? new ArchitecturePolicyInventory(
                ArchitecturePolicyInventory.CurrentSchemaId,
                3,
                new ArchitecturePolicyInventoryRules(2, 1, 0),
                new ArchitecturePolicyInventoryIgnoreDebt(1, 1, 0, 0, 0, 0),
                [Waiver()])
            : null;
        ArchitectureApplicabilityExpectedEntry expected = new(
            "topology.control",
            "declared_topology",
            ArchitectureApplicabilityMembership.Required);
        ArchitectureApplicabilityRecord record = new(
            "topology.control",
            "declared_topology",
            ArchitectureApplicabilityRecordState.Evaluable)
        {
            TopologyEvidence = new ArchitectureTopologyMappingEvidence(
                mode,
                "namespace",
                2,
                [new ArchitectureTopologySubjectEvidence("subject", "Project", "Assembly", "subject", "mapped", ["Api"])],
                [new ArchitectureTopologyRelationEvidence("Api", "Core", "subject", true)],
                ["Stale"],
                [new ArchitectureTopologyStaleEdgeEvidence("Api", "Legacy")]),
        };
        var completion = new ArchitectureAssessmentCompletionEvidence(
            ArchitectureAssessmentCompletionState.Pass,
            [new ArchitectureApplicabilityAssessment(expected, record, [])],
            []);
        var violation = new ArchitectureViolation(
            "dependency violation",
            "contract-id",
            "App",
            "Infrastructure",
            ["Infrastructure.Repository"]);
        ValidationOutcome validation = new(
            Passed: false,
            Violations: [violation],
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
            PolicyInventory = inventory,
            WaiverLifecycleAssessment = includeInventory
                ? new ArchitectureWaiverLifecycleAssessment("strict", [Waiver()], ["expired"])
                : null,
            ApplicabilityExpectedEntries = [expected],
            ApplicabilityRecords = [record],
            AssessmentCompletionEvidence = completion,
            RepositoryRoot = "/repo",
            PolicyImportPaths = ["/repo/architecture/dependencies.arch.yml"],
            ResolvedAssemblyPaths = ["/repo/bin/App.dll"],
            DiscoveredProjectPaths = ["/repo/App.csproj"],
            ExternalEvidenceRequirements = externalTrustReceipts is null
                ? []
                : externalTrustReceipts.Select(receipt => new ArchitectureExternalEvidenceRequirement
                {
                    Id = receipt.LogicalId,
                    Format = "sarif",
                    Required = true,
                    Tool = "Synthetic.Scanner",
                    Run = "assessment-42",
                    RequireRepository = true,
                    RequireRevision = true,
                    RequireScope = true,
                }).ToArray(),
            ExternalEvidenceTrustReceipts = externalTrustReceipts ?? [],
        };
        var debt = new BaselineVerifyOutcome(true, true, [], [], [], [], []);
        var debtGate = new ArchitectureDebtGateOutcome(
            true,
            true,
            new ArchitectureDebtGateEvaluation(true, mode, []),
            debt);
        ArchitectureHealthSummary summary = ArchitectureHealthProjector.Project(
            [new ArchitectureHealthValidationOutcome(mode, validation)], debtGate);
        return new ArchitectureHealthOutcome(
            summary,
            [new ArchitectureHealthValidationOutcome(mode, validation)],
            debtGate)
        {
            ExecutionContext = "run",
            ConditionSetName = "ci",
        };
    }

    private static ArchitectureWaiverLifecycleRecord Waiver() => new(
        "waiver-1",
        "active",
        "dependency",
        "contract-id",
        "strict_dependencies",
        "App",
        "Infrastructure",
        "target",
        "reviewed",
        "owner",
        "#1",
        new DateOnly(2026, 1, 1),
        new DateOnly(2027, 1, 1),
        new DateOnly(2026, 9, 1),
        true);

    private static SarifEvidenceReadResult TrustReceipt(
        string logicalId,
        SarifEvidenceTrustStatus status,
        string revision,
        int resultCount) => new(
        status,
        status == SarifEvidenceTrustStatus.Valid ? "trusted" : "wrong_revision",
        "Synthetic trust receipt",
        new SarifEvidenceProvenance(
            logicalId,
            $"evidence/{logicalId}.sarif",
            "sha256",
            "Synthetic.Scanner",
            "1.0",
            "assessment-42",
            resultCount,
            new SarifEvidenceResolvedContext(logicalId, "repo", revision, "scope")));
}
