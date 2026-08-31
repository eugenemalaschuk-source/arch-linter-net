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
        ArchitecturePolicyInventory inventory = Inventory(ignoreDebt: new ArchitecturePolicyInventoryIgnoreDebt(
            Total: 1,
            Active: 1,
            Stale: 0,
            Expired: 0,
            MetadataIncomplete: 0,
            Invalid: 0));
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
        ArchitecturePolicyInventory inventory = Inventory(ignoreDebt: new ArchitecturePolicyInventoryIgnoreDebt(
            Total: 1,
            Active: 0,
            Stale: 0,
            Expired: 0,
            MetadataIncomplete: 0,
            Invalid: 1));
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
                    "applicability")));
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
                new ArchitectureHealthReason("wrong_external_revision", dimensionName)));
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
                new ArchitectureHealthReason("baseline_debt_changed", "new_architecture_debt")));
            Assert.That(Dimension(summary, "policy_weakening").State,
                Is.EqualTo(ArchitectureHealthDimensionState.Degrading));
            Assert.That(Dimension(summary, "policy_weakening").Reasons.Single(), Is.EqualTo(
                new ArchitectureHealthReason("policy_weakening_detected", "policy_weakening")));
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
        bool includeInventory = true)
    {
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
            PolicyInventory = includeInventory ? inventory ?? Inventory() : null,
            AssessmentCompletionEvidence = completion ?? CompleteApplicability(),
            ApplicabilityRecords = applicabilityRecords ?? Array.Empty<ArchitectureApplicabilityRecord>(),
        };

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
                [],
                [],
                []))
        {
            PolicyWeakening = weakening,
        };
    }

    private static ArchitecturePolicyInventory Inventory(
        ArchitecturePolicyInventoryIgnoreDebt? ignoreDebt = null) => new(
        ArchitecturePolicyInventory.CurrentSchemaId,
        0,
        new ArchitecturePolicyInventoryRules(0, 0, 0),
        ignoreDebt ?? new ArchitecturePolicyInventoryIgnoreDebt(0, 0, 0, 0, 0, 0),
        []);

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
