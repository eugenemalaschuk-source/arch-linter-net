using System.Text.Json;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.PolicyWeakening;
using ArchLinterNet.Core.Reporting;
using ArchLinterNet.Core.Validation;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class ArchitectureHealthProjectorTests
{
    [Test]
    public void Project_CleanCompleteStrictOutput_PassesAndIsHealthy()
    {
        ArchitectureHealthSummary summary = Project(
            [Outcome("strict")],
            DebtGate());

        Assert.Multiple(() =>
        {
            Assert.That(summary.SchemaId, Is.EqualTo(ArchitectureHealthSummary.CurrentSchemaId));
            Assert.That(summary.Gate, Is.EqualTo(ArchitectureHealthGate.Pass));
            Assert.That(summary.Health, Is.EqualTo(ArchitectureHealthState.Healthy));
            Assert.That(Dimension(summary, "policy_inventory").State,
                Is.EqualTo(ArchitectureHealthDimensionState.Pass));
            Assert.That(Dimension(summary, "waiver_debt").State,
                Is.EqualTo(ArchitectureHealthDimensionState.Pass));
            Assert.That(Dimension(summary, "reviewed_finding_debt").State,
                Is.EqualTo(ArchitectureHealthDimensionState.Pass));
        });
    }

    [Test]
    public void Project_FrozenBaselineEntry_IsReviewedDebtAndNotNewDebt()
    {
        ArchitectureBaselineComparisonEntry frozen = Entry("frozen");
        ArchitectureHealthSummary summary = Project(
            [Outcome("strict")],
            DebtGate(frozen: [frozen]));

        Assert.Multiple(() =>
        {
            Assert.That(summary.Gate, Is.EqualTo(ArchitectureHealthGate.Pass));
            Assert.That(summary.Health, Is.EqualTo(ArchitectureHealthState.Debt));
            Assert.That(Dimension(summary, "reviewed_finding_debt").State,
                Is.EqualTo(ArchitectureHealthDimensionState.Debt));
            Assert.That(Dimension(summary, "reviewed_finding_debt").Reasons.Single(), Is.EqualTo(
                new ArchitectureHealthReason("reviewed_finding_debt", "reviewed_finding_debt")));
            Assert.That(Dimension(summary, "new_architecture_debt").State,
                Is.EqualTo(ArchitectureHealthDimensionState.Pass));
            Assert.That(Dimension(summary, "waiver_debt").State,
                Is.EqualTo(ArchitectureHealthDimensionState.Pass));
        });
    }

    [Test]
    public void Project_ActiveWaiver_IsDistinctDebtFromReviewedFindings()
    {
        ArchitecturePolicyInventory inventory = Inventory(waivers: [Waiver("active")]);
        ArchitectureHealthSummary summary = Project(
            [Outcome("strict", inventory: inventory)],
            DebtGate());

        Assert.Multiple(() =>
        {
            Assert.That(summary.Gate, Is.EqualTo(ArchitectureHealthGate.Pass));
            Assert.That(summary.Health, Is.EqualTo(ArchitectureHealthState.Debt));
            Assert.That(Dimension(summary, "waiver_debt").State,
                Is.EqualTo(ArchitectureHealthDimensionState.Debt));
            Assert.That(Dimension(summary, "reviewed_finding_debt").State,
                Is.EqualTo(ArchitectureHealthDimensionState.Pass));
            Assert.That(Dimension(summary, "new_architecture_debt").State,
                Is.EqualTo(ArchitectureHealthDimensionState.Pass));
        });
    }

    [Test]
    public void Project_InvalidWaiver_IsFailing()
    {
        ArchitecturePolicyInventory inventory = Inventory(waivers: [Waiver("invalid")]);
        ArchitectureHealthSummary summary = Project(
            [Outcome("strict", inventory: inventory)],
            DebtGate());

        Assert.Multiple(() =>
        {
            Assert.That(summary.Gate, Is.EqualTo(ArchitectureHealthGate.Fail));
            Assert.That(summary.Health, Is.EqualTo(ArchitectureHealthState.Failing));
            Assert.That(Dimension(summary, "waiver_debt").State,
                Is.EqualTo(ArchitectureHealthDimensionState.Fail));
        });
    }

    [Test]
    public void Project_CurrentStrictViolation_IsFailing()
    {
        ArchitectureViolation violation = new(
            "strict-rule",
            "strict-rule",
            "Sample.Application",
            "Sample.Infrastructure",
            ["Sample.Infrastructure.Repository"]);
        ArchitectureHealthSummary summary = Project(
            [Outcome("strict", passed: false, violations: [violation])],
            DebtGate());

        Assert.Multiple(() =>
        {
            Assert.That(summary.Gate, Is.EqualTo(ArchitectureHealthGate.Fail));
            Assert.That(summary.Health, Is.EqualTo(ArchitectureHealthState.Failing));
            Assert.That(Dimension(summary, "current_evaluation").State,
                Is.EqualTo(ArchitectureHealthDimensionState.Fail));
            Assert.That(Dimension(summary, "current_evaluation").Reasons.Single(), Is.EqualTo(
                new ArchitectureHealthReason("strict_validation_failed", "current_evaluation")));
        });
    }

    [Test]
    public void Project_MissingPolicyInventory_IsUnassessableRatherThanZero()
    {
        ArchitectureHealthSummary summary = Project(
            [Outcome("strict", includeInventory: false)],
            DebtGate());

        Assert.Multiple(() =>
        {
            Assert.That(summary.Gate, Is.EqualTo(ArchitectureHealthGate.Unassessable));
            Assert.That(summary.Health, Is.EqualTo(ArchitectureHealthState.Unassessable));
            Assert.That(Dimension(summary, "policy_inventory").State,
                Is.EqualTo(ArchitectureHealthDimensionState.Unassessable));
            Assert.That(Dimension(summary, "policy_inventory").Reasons.Single(), Is.EqualTo(
                new ArchitectureHealthReason("missing_policy_inventory", "policy_inventory")));
            Assert.That(Dimension(summary, "waiver_debt").State,
                Is.EqualTo(ArchitectureHealthDimensionState.Unassessable));
        });
    }

    [Test]
    public void Project_UnassessableApplicability_TakesPrecedenceOverCurrentFailure()
    {
        var reason = new ArchitectureApplicabilityReason(
            ArchitectureApplicabilityReasonCodes.MissingRequiredInput,
            "dependency",
            "control-a",
            "health-policy");
        var completion = new ArchitectureAssessmentCompletionEvidence(
            ArchitectureAssessmentCompletionState.Unassessable,
            [],
            [reason]);
        ArchitectureHealthSummary summary = Project(
            [Outcome("strict", passed: false, completion: completion)],
            DebtGate());

        Assert.Multiple(() =>
        {
            Assert.That(summary.Gate, Is.EqualTo(ArchitectureHealthGate.Unassessable));
            Assert.That(summary.Health, Is.EqualTo(ArchitectureHealthState.Unassessable));
            Assert.That(Dimension(summary, "current_evaluation").State,
                Is.EqualTo(ArchitectureHealthDimensionState.Fail));
            Assert.That(Dimension(summary, "applicability").State,
                Is.EqualTo(ArchitectureHealthDimensionState.Unassessable));
            Assert.That(Dimension(summary, "applicability").Reasons.Single(), Is.EqualTo(
                new ArchitectureHealthReason(
                    ArchitectureApplicabilityReasonCodes.MissingRequiredInput,
                    "applicability")
                {
                    Family = "dependency",
                    ControlIdentity = "control-a",
                    PolicyIdentity = "health-policy",
                }));
        });
    }

    [Test]
    public void Project_FailedApplicability_IsFailing()
    {
        var completion = new ArchitectureAssessmentCompletionEvidence(
            ArchitectureAssessmentCompletionState.Fail,
            [],
            [new ArchitectureApplicabilityReason("incomplete_required_assessment", "dependency", "control-a", "health-policy")]);
        ArchitectureHealthSummary summary = Project(
            [Outcome("strict", completion: completion)],
            DebtGate());

        Assert.Multiple(() =>
        {
            Assert.That(summary.Gate, Is.EqualTo(ArchitectureHealthGate.Fail));
            Assert.That(summary.Health, Is.EqualTo(ArchitectureHealthState.Failing));
            Assert.That(Dimension(summary, "applicability").State,
                Is.EqualTo(ArchitectureHealthDimensionState.Fail));
        });
    }

    [TestCase("declared_topology", "topology")]
    [TestCase("metrics", "metrics")]
    [TestCase("metric_budgets", "metrics")]
    [TestCase("external_diagnostics", "external_evidence")]
    public void Project_UnassessableAuthoritativeFamily_IsUnassessable(
        string family,
        string dimensionName)
    {
        var record = new ArchitectureApplicabilityRecord(
            "control-a",
            family,
            ArchitectureApplicabilityRecordState.Unassessable,
            [new ArchitectureApplicabilityReason("wrong_external_revision", family, "control-a", "health-policy")]);
        ArchitectureHealthSummary summary = Project(
            [Outcome("strict", applicabilityRecords: [record])],
            DebtGate());

        Assert.Multiple(() =>
        {
            Assert.That(summary.Gate, Is.EqualTo(ArchitectureHealthGate.Unassessable));
            Assert.That(summary.Health, Is.EqualTo(ArchitectureHealthState.Unassessable));
            Assert.That(Dimension(summary, dimensionName).State,
                Is.EqualTo(ArchitectureHealthDimensionState.Unassessable));
            Assert.That(Dimension(summary, dimensionName).Reasons.Single(), Is.EqualTo(
                new ArchitectureHealthReason("wrong_external_revision", dimensionName)
                {
                    Family = family,
                    ControlIdentity = "control-a",
                    PolicyIdentity = "health-policy",
                }));
        });
    }

    [Test]
    public void Project_NewBaselineDebtAndPolicyWeakening_AreDegrading()
    {
        ArchitectureBaselineComparisonEntry newEntry = Entry("new");
        var weakening = new ArchitecturePolicyWeakeningResult(
            ArchitecturePolicyWeakeningResult.CurrentSchemaVersion,
            ArchitecturePolicyWeakeningResult.ResultKind,
            "health-policy",
            1,
            "error",
            [new ArchitecturePolicyWeakeningFinding(
                "control-a",
                "strict_to_audit",
                "dependency:control-a",
                "semantic",
                "error",
                ["strict"],
                ["audit"],
                null,
                null,
                [],
                "control is less restrictive")]);
        ArchitectureHealthSummary summary = Project(
            [Outcome("strict")],
            DebtGate(
                passed: false,
                inSync: false,
                @new: [newEntry],
                weakening: weakening));

        Assert.Multiple(() =>
        {
            Assert.That(summary.Gate, Is.EqualTo(ArchitectureHealthGate.Fail));
            Assert.That(summary.Health, Is.EqualTo(ArchitectureHealthState.Degrading));
            Assert.That(Dimension(summary, "new_architecture_debt").State,
                Is.EqualTo(ArchitectureHealthDimensionState.Degrading));
            Assert.That(Dimension(summary, "new_architecture_debt").Reasons.Single(), Is.EqualTo(
                new ArchitectureHealthReason("new_baseline_debt", "new_architecture_debt")));
            Assert.That(Dimension(summary, "policy_weakening").State,
                Is.EqualTo(ArchitectureHealthDimensionState.Degrading));
            Assert.That(Dimension(summary, "policy_weakening").Reasons.Single(), Is.EqualTo(
                new ArchitectureHealthReason("policy_weakening_detected", "policy_weakening")));
        });
    }

    [Test]
    public void Project_AbsoluteMetricBudgetBreach_IsFailingWithCanonicalReceiptReference()
    {
        ArchitectureViolation breach = MetricBreach();
        ArchitectureHealthSummary summary = Project(
            [Outcome(
                "strict",
                passed: false,
                violations: [breach],
                applicabilityRecords: [EvaluableRecord("budget.api", "metric_budgets")])],
            DebtGate());

        ArchitectureHealthReason reason = Dimension(summary, "metrics").Reasons.Single();
        Assert.Multiple(() =>
        {
            Assert.That(Dimension(summary, "metrics").State, Is.EqualTo(ArchitectureHealthDimensionState.Fail));
            Assert.That(summary.Gate, Is.EqualTo(ArchitectureHealthGate.Fail));
            Assert.That(reason.Code, Is.EqualTo("metric_budget_breach"));
            Assert.That(reason.Family, Is.EqualTo("metric_budgets"));
            Assert.That(reason.ControlIdentity, Is.EqualTo("budget.api"));
            Assert.That(reason.EvidenceIdentity, Does.Contain("budget.api"));
        });
    }

    [Test]
    public void Project_BaselineRelativeMetricBreach_IsFailingRatherThanEvaluablePass()
    {
        ArchitectureViolation breach = MetricBreach() with
        {
            Payload = ((MetricBudgetPayload)MetricBreach().Payload!) with
            {
                BaselineMode = "relative",
                BaselineValue = 10,
                Delta = 5,
                AllowedDelta = 2,
                EffectiveThreshold = 12,
            },
        };
        ArchitectureHealthSummary summary = Project(
            [Outcome(
                "strict",
                passed: false,
                violations: [breach],
                applicabilityRecords: [EvaluableRecord("budget.api", "metric_budgets")])],
            DebtGate());

        Assert.That(Dimension(summary, "metrics").State, Is.EqualTo(ArchitectureHealthDimensionState.Fail));
    }

    [Test]
    public void Project_BlockingExternalFindingAndCleanEvidence_ProjectTheirTypedReceiptState()
    {
        ImportedExternalDiagnosticProjection blocking = ArchitectureImportedDiagnosticProjector.Project(
            new SarifExternalDiagnosticSelectionResult([SelectedExternalDiagnostic("strict", SarifExternalDiagnosticGovernanceMode.Strict)]));
        ArchitectureHealthSummary failed = Project(
            [Outcome(
                "strict",
                applicabilityRecords: [EvaluableRecord("external.scan", "external_diagnostics")],
                importedDiagnostics: blocking)],
            DebtGate());
        ArchitectureHealthSummary clean = Project(
            [Outcome(
                "strict",
                applicabilityRecords: [EvaluableRecord("external.scan", "external_diagnostics")],
                importedDiagnostics: ImportedExternalDiagnosticProjection.Empty)],
            DebtGate());

        ArchitectureHealthReason reason = Dimension(failed, "external_evidence").Reasons.Single();
        Assert.Multiple(() =>
        {
            Assert.That(Dimension(failed, "external_evidence").State, Is.EqualTo(ArchitectureHealthDimensionState.Fail));
            Assert.That(reason.Family, Is.EqualTo("external_diagnostics"));
            Assert.That(reason.ControlIdentity, Is.EqualTo("external.scan"));
            Assert.That(reason.EvidenceIdentity, Is.EqualTo("external-diagnostic:v2:strict"));
            Assert.That(Dimension(clean, "external_evidence").State, Is.EqualTo(ArchitectureHealthDimensionState.Pass));
        });
    }

    [Test]
    public void Project_DeclaredTopologyViolation_IsFailingRatherThanEvaluablePass()
    {
        ArchitectureViolation violation = new(
            "topology declared relationship",
            "declared-topology",
            "Api",
            "Infrastructure",
            ["Api -> Infrastructure"]);
        ArchitectureHealthSummary summary = Project(
            [Outcome(
                "strict",
                passed: false,
                violations: [violation],
                applicabilityRecords: [EvaluableRecord("declared-topology", "declared_topology")])],
            DebtGate());

        Assert.Multiple(() =>
        {
            Assert.That(Dimension(summary, "topology").State, Is.EqualTo(ArchitectureHealthDimensionState.Fail));
            Assert.That(Dimension(summary, "topology").Reasons.Single().ControlIdentity,
                Is.EqualTo("declared-topology"));
        });
    }

    [Test]
    public void FormatAsJson_RetainsCanonicalReasonProvenanceForEvidenceDrillDown()
    {
        ArchitectureHealthSummary summary = Project(
            [Outcome(
                "strict",
                passed: false,
                violations: [MetricBreach()],
                applicabilityRecords: [EvaluableRecord("budget.api", "metric_budgets")])],
            DebtGate());

        using JsonDocument document = JsonDocument.Parse(ArchitectureHealthProjector.FormatAsJson(summary));
        JsonElement reason = document.RootElement.GetProperty("dimensions")
            .EnumerateArray()
            .Single(dimension => dimension.GetProperty("name").GetString() == "metrics")
            .GetProperty("reasons")[0];

        Assert.Multiple(() =>
        {
            Assert.That(reason.GetProperty("source").GetString(), Is.EqualTo("metrics"));
            Assert.That(reason.GetProperty("family").GetString(), Is.EqualTo("metric_budgets"));
            Assert.That(reason.GetProperty("control_identity").GetString(), Is.EqualTo("budget.api"));
            Assert.That(reason.GetProperty("policy_identity").ValueKind, Is.EqualTo(JsonValueKind.Null));
            Assert.That(reason.GetProperty("evidence_identity").GetString(), Does.Contain("budget.api"));
        });
    }

    [Test]
    public void Project_StrictStaleAndCompatibilityMetadataIncompleteWaiversKeepLifecycleSemantics()
    {
        ArchitectureHealthSummary stale = Project(
            [Outcome(
                "strict",
                passed: false,
                inventory: Inventory(waivers: [Waiver("stale")]))],
            DebtGate());
        ArchitectureHealthSummary metadataIncomplete = Project(
            [Outcome(
                "strict",
                inventory: Inventory(waivers: [Waiver("metadata_incomplete")]),
                waiverLifecycleAssessment: Lifecycle([Waiver("metadata_incomplete")], "compatibility"))],
            DebtGate());

        Assert.Multiple(() =>
        {
            Assert.That(Dimension(stale, "waiver_debt").State, Is.EqualTo(ArchitectureHealthDimensionState.Fail));
            Assert.That(Dimension(stale, "waiver_debt").Reasons.Single().Code,
                Is.EqualTo("blocking_waiver_lifecycle:stale"));
            Assert.That(Dimension(metadataIncomplete, "waiver_debt").State,
                Is.EqualTo(ArchitectureHealthDimensionState.Degrading));
            Assert.That(Dimension(metadataIncomplete, "waiver_debt").Reasons.Single().Code,
                Is.EqualTo("waiver_lifecycle_attention:metadata_incomplete"));
        });
    }

    [Test]
    public void Project_ResolvedBaselineOnly_RemainsGateHygieneWithoutArchitectureDegradation()
    {
        ArchitectureHealthSummary summary = Project(
            [Outcome("strict")],
            DebtGate(passed: false, inSync: false, resolved: [Entry("resolved")]));

        Assert.Multiple(() =>
        {
            Assert.That(summary.Gate, Is.EqualTo(ArchitectureHealthGate.Fail));
            Assert.That(summary.Health, Is.EqualTo(ArchitectureHealthState.Healthy));
            Assert.That(Dimension(summary, "new_architecture_debt").State,
                Is.EqualTo(ArchitectureHealthDimensionState.Pass));
            Assert.That(Dimension(summary, "new_architecture_debt").Reasons.Single().Code,
                Is.EqualTo("resolved_baseline_hygiene"));
        });
    }

    [Test]
    public void Project_EquivalentCanonicalInputs_HaveDeterministicDimensionsAndReasons()
    {
        var completion = new ArchitectureAssessmentCompletionEvidence(
            ArchitectureAssessmentCompletionState.Unassessable,
            [],
            [
                new ArchitectureApplicabilityReason("z_reason", "dependency", "control-z", "health-policy"),
                new ArchitectureApplicabilityReason("a_reason", "dependency", "control-a", "health-policy"),
                new ArchitectureApplicabilityReason("a_reason", "dependency", "control-a", "health-policy"),
            ]);
        ArchitectureHealthValidationOutcome audit = Outcome("audit", completion: completion);
        ArchitectureHealthValidationOutcome strict = Outcome("strict", completion: completion);

        ArchitectureHealthSummary first = ArchitectureHealthProjector.Project([audit, strict], DebtGate());
        ArchitectureHealthSummary second = ArchitectureHealthProjector.Project([strict, audit], DebtGate());

        Assert.Multiple(() =>
        {
            Assert.That(ArchitectureHealthProjector.FormatAsHuman(first),
                Is.EqualTo(ArchitectureHealthProjector.FormatAsHuman(second)));
            Assert.That(ArchitectureHealthProjector.FormatAsJson(first),
                Is.EqualTo(ArchitectureHealthProjector.FormatAsJson(second)));
            Assert.That(first.Dimensions.Select(dimension => dimension.Name), Is.EqualTo(new[]
            {
                "applicability",
                "coverage",
                "current_evaluation",
                "external_evidence",
                "history",
                "metrics",
                "new_architecture_debt",
                "policy_inventory",
                "policy_weakening",
                "reviewed_finding_debt",
                "topology",
                "waiver_debt",
            }));
            Assert.That(Dimension(first, "applicability").Reasons.Select(reason => reason.Code),
                Is.EqualTo(new[] { "a_reason", "z_reason" }));
        });
    }

    private static ArchitectureHealthValidationOutcome Outcome(
        string mode,
        bool passed = true,
        ArchitecturePolicyInventory? inventory = null,
        ArchitectureAssessmentCompletionEvidence? completion = null,
        IReadOnlyCollection<ArchitectureViolation>? violations = null,
        IReadOnlyList<ArchitectureApplicabilityRecord>? applicabilityRecords = null,
        ImportedExternalDiagnosticProjection? importedDiagnostics = null,
        ArchitectureWaiverLifecycleAssessment? waiverLifecycleAssessment = null,
        bool includeInventory = true)
    {
        ArchitecturePolicyInventory? effectiveInventory = includeInventory ? inventory ?? Inventory() : null;
        ValidationOutcome validation = new(
            passed,
            violations ?? Array.Empty<ArchitectureViolation>(),
            Array.Empty<string>(),
            Array.Empty<ArchitectureViolation>(),
            "off",
            Array.Empty<ArchitectureUnmatchedIgnoredViolation>(),
            "off",
            Array.Empty<PolicyConsistencyDiagnostic>(),
            "off",
            Array.Empty<ArchitectureCoverageSummary>(),
            Array.Empty<ArchitectureClassificationConflict>(),
            Array.Empty<ArchitectureClassificationMetadataFailure>())
        {
            PolicyInventory = effectiveInventory,
            AssessmentCompletionEvidence = completion ?? CompleteApplicability(),
            ApplicabilityRecords = applicabilityRecords ?? Array.Empty<ArchitectureApplicabilityRecord>(),
            WaiverLifecycleAssessment = waiverLifecycleAssessment ?? (effectiveInventory is null
                ? null
                : Lifecycle(effectiveInventory.Waivers)),
        };
        if (importedDiagnostics is not null)
        {
            validation = validation.WithImportedDiagnostics(importedDiagnostics);
        }

        return new ArchitectureHealthValidationOutcome(mode, validation);
    }

    private static ArchitectureHealthSummary Project(
        IReadOnlyList<ArchitectureHealthValidationOutcome> outcomes,
        ArchitectureDebtGateOutcome debtGate) =>
        ArchitectureHealthProjector.Project(outcomes, debtGate);

    private static ArchitectureDebtGateOutcome DebtGate(
        bool succeeded = true,
        bool passed = true,
        bool inSync = true,
        IReadOnlyList<ArchitectureBaselineComparisonEntry>? @new = null,
        IReadOnlyList<ArchitectureBaselineComparisonEntry>? frozen = null,
        IReadOnlyList<ArchitectureBaselineComparisonEntry>? resolved = null,
        ArchitecturePolicyWeakeningResult? weakening = null)
    {
        return new ArchitectureDebtGateOutcome(
            succeeded,
            passed,
            new ArchitectureDebtGateEvaluation(succeeded, "strict", []),
            new BaselineVerifyOutcome(
                succeeded,
                inSync,
                @new ?? [],
                frozen ?? [],
                resolved ?? [],
                [],
                []))
        {
            PolicyWeakening = weakening,
        };
    }

    private static ArchitecturePolicyInventory Inventory(
        ArchitecturePolicyInventoryIgnoreDebt? ignoreDebt = null,
        IReadOnlyList<ArchitectureWaiverLifecycleRecord>? waivers = null) => new(
        ArchitecturePolicyInventory.CurrentSchemaId,
        0,
        new ArchitecturePolicyInventoryRules(0, 0, 0),
        ignoreDebt ?? DebtFor(waivers ?? []),
        waivers ?? []);

    private static ArchitectureWaiverLifecycleAssessment Lifecycle(
        IReadOnlyList<ArchitectureWaiverLifecycleRecord> records,
        string profile = "strict") => new(
        profile,
        records,
        profile == "strict" ? ["expired", "invalid", "stale"] : ["invalid"]);

    private static ArchitecturePolicyInventoryIgnoreDebt DebtFor(
        IReadOnlyList<ArchitectureWaiverLifecycleRecord> records) => new(
        records.Count,
        records.Count(record => record.State == "active"),
        records.Count(record => record.State == "stale"),
        records.Count(record => record.State == "expired"),
        records.Count(record => record.State == "metadata_incomplete"),
        records.Count(record => record.State == "invalid"));

    private static ArchitectureWaiverLifecycleRecord Waiver(string state, string id = "waiver-1") => new(
        id,
        state,
        "Sample waiver",
        "sample-waiver",
        "strict",
        "Sample.Application.Service",
        "Sample.Infrastructure.Repository",
        "sha256:example",
        "reviewed exception",
        "architecture@example.test",
        "#123",
        new DateOnly(2026, 1, 1),
        new DateOnly(2026, 12, 31),
        new DateOnly(2026, 9, 1),
        state == "active");

    private static ArchitectureApplicabilityRecord EvaluableRecord(string control, string family) => new(
        control,
        family,
        ArchitectureApplicabilityRecordState.Evaluable,
        new ArchitectureApplicabilityProvenance(family, control, "health-policy"));

    private static ArchitectureViolation MetricBreach() => new(
        "metric budget",
        "budget.api",
        "Sample.Api",
        "metric:api-lines",
        ["Sample.Api"])
    {
        Payload = new MetricBudgetPayload(
            "budget.api",
            "api-lines",
            "lines",
            "Sample.Api",
            "project",
            15,
            "maximum",
            10,
            ["Sample.Api"]),
    };

    private static SarifSelectedExternalDiagnostic SelectedExternalDiagnostic(
        string identity,
        SarifExternalDiagnosticGovernanceMode mode)
    {
        var source = new SarifEvidenceSourceDiagnostic(
            "external architecture finding",
            "ARCH100",
            SarifEvidenceSourceSeverity.Error,
            new SarifEvidenceSourceLocation("src/App/External.cs", new SarifEvidenceSourceRegion(startLine: 12, startColumn: 3)),
            project: "App",
            driverRuleTags: ["architecture"]);
        var provenance = new SarifEvidenceProvenance(
            "external.scan",
            "artifacts/external.sarif",
            "evidence-sha256",
            "Example Analyzer",
            "1.0.0",
            "run-1",
            1,
            new SarifEvidenceResolvedContext("external.scan", "repo", "revision", "scope"));
        return new SarifSelectedExternalDiagnostic(
            $"external-diagnostic:v2:{identity}",
            source,
            mode,
            new SarifExternalDiagnosticFingerprint(SarifExternalDiagnosticFingerprintOrigin.Source, "source-fingerprint", "primary"),
            [provenance]);
    }

    private static ArchitectureAssessmentCompletionEvidence CompleteApplicability() =>
        new(ArchitectureAssessmentCompletionState.Pass, [], []);

    private static ArchitectureBaselineComparisonEntry Entry(string id) => new(
        "strict",
        id,
        "Sample.Application.Service",
        "Sample.Infrastructure.Repository",
        "reviewed");

    private static ArchitectureHealthDimension Dimension(
        ArchitectureHealthSummary summary,
        string name) => summary.Dimensions.Single(dimension => dimension.Name == name);
}
