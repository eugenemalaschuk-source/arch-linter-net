using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Validation;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

public sealed partial class ArchitectureHealthProjectorTests
{
    [Test]
    public void Project_WarningCoverageFinding_IsReportableWithoutFailingGate()
    {
        ArchitectureHealthSummary summary = Project(
            [Outcome("strict", coverageFindings: [CoverageFinding()], coverageConfig: "warn")],
            DebtGate());

        ArchitectureHealthReason reason = Dimension(summary, "coverage").Reasons.Single();
        Assert.Multiple(() =>
        {
            Assert.That(summary.Gate, Is.EqualTo(ArchitectureHealthGate.Pass));
            Assert.That(Dimension(summary, "current_evaluation").State,
                Is.EqualTo(ArchitectureHealthDimensionState.Pass));
            Assert.That(Dimension(summary, "coverage").State,
                Is.EqualTo(ArchitectureHealthDimensionState.Degrading));
            Assert.That(reason.Code, Is.EqualTo("coverage_finding"));
            Assert.That(reason.ControlIdentity, Is.EqualTo("coverage.namespace"));
            Assert.That(reason.EvidenceIdentity, Does.Contain("coverage.namespace"));
        });
    }

    [Test]
    public void Project_ErrorCoverageFinding_IsFailingAuthority()
    {
        ArchitectureHealthSummary summary = Project(
            [Outcome(
                "strict",
                passed: false,
                coverageFindings: [CoverageFinding()],
                coverageConfig: "error")],
            DebtGate());

        Assert.Multiple(() =>
        {
            Assert.That(summary.Gate, Is.EqualTo(ArchitectureHealthGate.Fail));
            Assert.That(Dimension(summary, "coverage").State,
                Is.EqualTo(ArchitectureHealthDimensionState.Fail));
        });
    }

    [Test]
    public void Project_AuditOnlyValidationFinding_IsRetainedWithoutFailingGate()
    {
        ArchitectureViolation auditFinding = new(
            "audit architecture finding",
            "audit-layering",
            "Sample.Api.Service",
            "Sample.Infrastructure.Repository",
            ["Sample.Infrastructure.Repository"]);
        ArchitectureHealthSummary summary = Project(
            [Outcome("strict"), Outcome("audit", passed: false, violations: [auditFinding])],
            DebtGate());

        ArchitectureHealthReason reason = Dimension(summary, "audit_evidence").Reasons.Single();
        Assert.Multiple(() =>
        {
            Assert.That(summary.Gate, Is.EqualTo(ArchitectureHealthGate.Pass));
            Assert.That(Dimension(summary, "current_evaluation").State,
                Is.EqualTo(ArchitectureHealthDimensionState.Pass));
            Assert.That(Dimension(summary, "audit_evidence").State,
                Is.EqualTo(ArchitectureHealthDimensionState.Degrading));
            Assert.That(reason.Code, Is.EqualTo("audit_validation_finding"));
            Assert.That(reason.ControlIdentity, Is.EqualTo("audit-layering"));
            Assert.That(reason.EvidenceIdentity, Does.Contain("audit-layering"));
        });
    }

    [Test]
    public void Project_EqualReasonCodesWithDistinctCanonicalProvenance_AreNotCollapsed()
    {
        ArchitectureApplicabilityRecord[] records =
        [
            new ArchitectureApplicabilityRecord(
                "external.sonar",
                "external_diagnostics",
                ArchitectureApplicabilityRecordState.Unassessable,
                [new ArchitectureApplicabilityReason(
                    "wrong_external_revision",
                    "external_diagnostics",
                    "external.sonar",
                    "strict-external-sonar")]),
            new ArchitectureApplicabilityRecord(
                "external.codeql",
                "external_diagnostics",
                ArchitectureApplicabilityRecordState.Unassessable,
                [new ArchitectureApplicabilityReason(
                    "wrong_external_revision",
                    "external_diagnostics",
                    "external.codeql",
                    "strict-external-codeql")]),
        ];
        ArchitectureHealthSummary summary = Project([Outcome("strict", applicabilityRecords: records)], DebtGate());

        ArchitectureHealthReason[] reasons = Dimension(summary, "external_evidence").Reasons.ToArray();
        Assert.Multiple(() =>
        {
            Assert.That(reasons, Has.Length.EqualTo(2));
            Assert.That(reasons.Select(reason => reason.Code), Is.EqualTo(
                new[] { "wrong_external_revision", "wrong_external_revision" }));
            Assert.That(reasons.Select(reason => reason.ControlIdentity), Is.EqualTo(
                new[] { "external.codeql", "external.sonar" }));
            Assert.That(reasons.Select(reason => reason.PolicyIdentity), Is.EqualTo(
                new[] { "strict-external-codeql", "strict-external-sonar" }));
        });
    }

    [TestCase("active", ArchitectureHealthDimensionState.Debt)]
    [TestCase("stale", ArchitectureHealthDimensionState.Fail)]
    [TestCase("expired", ArchitectureHealthDimensionState.Fail)]
    [TestCase("invalid", ArchitectureHealthDimensionState.Fail)]
    [TestCase("metadata_incomplete", ArchitectureHealthDimensionState.Degrading)]
    public void Project_WaiverLifecycleStatesRemainDistinct(string lifecycleState, ArchitectureHealthDimensionState expected)
    {
        ArchitectureWaiverLifecycleRecord waiver = Waiver(lifecycleState);
        ArchitectureHealthSummary summary = Project(
            [Outcome(
                "strict",
                passed: expected == ArchitectureHealthDimensionState.Fail ? false : true,
                inventory: Inventory(waivers: [waiver]))],
            DebtGate());

        ArchitectureHealthDimension dimension = Dimension(summary, "waiver_debt");
        Assert.Multiple(() =>
        {
            Assert.That(dimension.State, Is.EqualTo(expected));
            Assert.That(dimension.Reasons.Single().Code, Does.EndWith($":{lifecycleState}"));
            Assert.That(dimension.Reasons.Single().EvidenceIdentity, Is.EqualTo("waiver-1"));
        });
    }
}
