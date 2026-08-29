using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Validation;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class ArchitectureApplicabilityEvaluatorTests
{
    [Test]
    public void EmptyExistingPolicyInput_PreservesNoCompletionEvidence()
    {
        ArchitectureAssessmentCompletionEvidence? result =
            ArchitectureApplicabilityEvaluator.Evaluate([], [], conformancePassed: true);

        Assert.That(result, Is.Null);
    }

    [Test]
    public void AllRequiredEvaluable_UsesOrdinaryConformanceForCompletion()
    {
        ArchitectureApplicabilityExpectedEntry[] expected =
        [Entry("control-a", ArchitectureApplicabilityMembership.Required), Entry("control-b", ArchitectureApplicabilityMembership.Required)];
        ArchitectureApplicabilityRecord[] records =
        [Record("control-b", ArchitectureApplicabilityRecordState.Evaluable), Record("control-a", ArchitectureApplicabilityRecordState.Evaluable)];

        ArchitectureAssessmentCompletionEvidence result =
            ArchitectureApplicabilityEvaluator.Evaluate(expected, records, conformancePassed: true)!;

        Assert.Multiple(() =>
        {
            Assert.That(result.State, Is.EqualTo(ArchitectureAssessmentCompletionState.Pass));
            Assert.That(result.RequiredCount, Is.EqualTo(2));
            Assert.That(result.RequiredEvaluableCount, Is.EqualTo(2));
            Assert.That(result.Controls.Select(control => control.ControlIdentity), Is.EqualTo(["control-a", "control-b"]));
        });
    }

    [Test]
    public void RequiredMissingRecord_RemainsInDenominatorAndIsUnassessable()
    {
        ArchitectureAssessmentCompletionEvidence result =
            ArchitectureApplicabilityEvaluator.Evaluate(
                [Entry("control-a", ArchitectureApplicabilityMembership.Required), Entry("control-b", ArchitectureApplicabilityMembership.Required)],
                [Record("control-a", ArchitectureApplicabilityRecordState.Evaluable)],
                conformancePassed: true)!;

        ArchitectureApplicabilityAssessment missing = result.Controls.Single(control => control.ControlIdentity == "control-b");
        Assert.Multiple(() =>
        {
            Assert.That(result.State, Is.EqualTo(ArchitectureAssessmentCompletionState.Unassessable));
            Assert.That(result.RequiredCount, Is.EqualTo(2));
            Assert.That(result.RequiredEvaluableCount, Is.EqualTo(1));
            Assert.That(missing.Record, Is.Null);
            Assert.That(missing.IntegrityReasons.Select(reason => reason.Code),
                Is.EqualTo([ArchitectureApplicabilityReasonCodes.MissingApplicabilityRecord]));
        });
    }

    [Test]
    public void DuplicateRecord_IsIntegrityEvidenceAndDoesNotEvaluateControl()
    {
        ArchitectureAssessmentCompletionEvidence result =
            ArchitectureApplicabilityEvaluator.Evaluate(
                [Entry("control-a", ArchitectureApplicabilityMembership.Required)],
                [Record("control-a", ArchitectureApplicabilityRecordState.Evaluable), Record("control-a", ArchitectureApplicabilityRecordState.Evaluable)],
                conformancePassed: true)!;

        ArchitectureApplicabilityAssessment assessment = result.Controls.Single();
        Assert.Multiple(() =>
        {
            Assert.That(result.State, Is.EqualTo(ArchitectureAssessmentCompletionState.Unassessable));
            Assert.That(assessment.Record, Is.Null);
            Assert.That(assessment.State, Is.Null);
            Assert.That(result.Reasons.Select(reason => reason.Code),
                Does.Contain(ArchitectureApplicabilityReasonCodes.DuplicateApplicabilityRecordIdentity));
        });
    }

    [Test]
    public void OrphanRecord_IsRetainedByAntiJoin()
    {
        ArchitectureAssessmentCompletionEvidence result =
            ArchitectureApplicabilityEvaluator.Evaluate(
                [Entry("control-a", ArchitectureApplicabilityMembership.Required)],
                [Record("control-a", ArchitectureApplicabilityRecordState.Evaluable), Record("control-x", ArchitectureApplicabilityRecordState.Evaluable)],
                conformancePassed: true)!;

        ArchitectureApplicabilityAssessment orphan = result.Controls.Single(control => control.ControlIdentity == "control-x");
        Assert.Multiple(() =>
        {
            Assert.That(result.State, Is.EqualTo(ArchitectureAssessmentCompletionState.Unassessable));
            Assert.That(orphan.Expected, Is.Null);
            Assert.That(orphan.Record, Is.Not.Null);
            Assert.That(orphan.IntegrityReasons.Single().Code,
                Is.EqualTo(ArchitectureApplicabilityReasonCodes.UnknownApplicabilityRecordIdentity));
        });
    }

    [Test]
    public void IncompatibleMembershipAndState_IsNotAValidRecord()
    {
        ArchitectureAssessmentCompletionEvidence result =
            ArchitectureApplicabilityEvaluator.Evaluate(
                [Entry("control-a", ArchitectureApplicabilityMembership.Required)],
                [Record("control-a", ArchitectureApplicabilityRecordState.NotApplicable)],
                conformancePassed: true)!;

        ArchitectureApplicabilityAssessment assessment = result.Controls.Single();
        Assert.Multiple(() =>
        {
            Assert.That(result.State, Is.EqualTo(ArchitectureAssessmentCompletionState.Unassessable));
            Assert.That(assessment.State, Is.Null);
            Assert.That(assessment.IntegrityReasons.Single().Code,
                Is.EqualTo(ArchitectureApplicabilityReasonCodes.IncompatibleApplicabilityRecord));
        });
    }

    [Test]
    public void OptionalAbsent_IsVisibleWithoutInflatingRequiredDenominator()
    {
        ArchitectureAssessmentCompletionEvidence result =
            ArchitectureApplicabilityEvaluator.Evaluate(
                [Entry("control-a", ArchitectureApplicabilityMembership.Required), Entry("control-b", ArchitectureApplicabilityMembership.Optional)],
                [Record("control-a", ArchitectureApplicabilityRecordState.Evaluable), Record("control-b", ArchitectureApplicabilityRecordState.NotApplicable)],
                conformancePassed: true)!;

        Assert.Multiple(() =>
        {
            Assert.That(result.State, Is.EqualTo(ArchitectureAssessmentCompletionState.Pass));
            Assert.That(result.RequiredCount, Is.EqualTo(1));
            Assert.That(result.Controls.Single(control => control.ControlIdentity == "control-b").State,
                Is.EqualTo(ArchitectureApplicabilityRecordState.NotApplicable));
        });
    }

    [Test]
    public void OptionalAndNotApplicableAbsent_DoNotMakeRequiredAssessmentUnassessable()
    {
        ArchitectureAssessmentCompletionEvidence result =
            ArchitectureApplicabilityEvaluator.Evaluate(
                [
                    Entry("control-required", ArchitectureApplicabilityMembership.Required),
                    Entry("control-optional", ArchitectureApplicabilityMembership.Optional),
                    Entry("control-not-applicable", ArchitectureApplicabilityMembership.NotApplicable),
                ],
                [Record("control-required", ArchitectureApplicabilityRecordState.Evaluable)],
                conformancePassed: true)!;

        Assert.Multiple(() =>
        {
            Assert.That(result.State, Is.EqualTo(ArchitectureAssessmentCompletionState.Pass));
            Assert.That(result.RequiredCount, Is.EqualTo(1));
            Assert.That(result.RequiredEvaluableCount, Is.EqualTo(1));
            Assert.That(result.Controls.Single(control => control.ControlIdentity == "control-optional").IntegrityReasons,
                Is.Empty);
            Assert.That(result.Controls.Single(control => control.ControlIdentity == "control-not-applicable").IntegrityReasons,
                Is.Empty);
        });
    }

    [Test]
    public void UnassessableRequiredEvidence_TakesPrecedenceOverConformanceFailure()
    {
        ArchitectureAssessmentCompletionEvidence result =
            ArchitectureApplicabilityEvaluator.Evaluate(
                [Entry("control-a", ArchitectureApplicabilityMembership.Required)],
                [Record("control-a", ArchitectureApplicabilityRecordState.Unassessable,
                    new ArchitectureApplicabilityReason(ArchitectureApplicabilityReasonCodes.StaleDeclaration, "family", "control-a"))],
                conformancePassed: false)!;

        Assert.That(result.State, Is.EqualTo(ArchitectureAssessmentCompletionState.Unassessable));
        Assert.That(result.Reasons.Single().Code, Is.EqualTo(ArchitectureApplicabilityReasonCodes.StaleDeclaration));
    }

    [Test]
    public void ReasonsAndControls_AreDeterministicallyOrdered()
    {
        ArchitectureAssessmentCompletionEvidence result =
            ArchitectureApplicabilityEvaluator.Evaluate(
                [Entry("control-z", ArchitectureApplicabilityMembership.Required), Entry("control-a", ArchitectureApplicabilityMembership.Required)],
                [Record("control-z", ArchitectureApplicabilityRecordState.Unassessable,
                    new ArchitectureApplicabilityReason(ArchitectureApplicabilityReasonCodes.UnmappedSubject, "family", "control-z")),
                 Record("control-a", ArchitectureApplicabilityRecordState.Unassessable,
                    new ArchitectureApplicabilityReason(ArchitectureApplicabilityReasonCodes.MissingRequiredInput, "family", "control-a"))],
                conformancePassed: true)!;

        Assert.Multiple(() =>
        {
            Assert.That(result.Controls.Select(control => control.ControlIdentity), Is.EqualTo(["control-a", "control-z"]));
            Assert.That(result.Reasons.Select(reason => reason.Provenance.ControlIdentity), Is.EqualTo(["control-a", "control-z"]));
        });
    }

    private static ArchitectureApplicabilityExpectedEntry Entry(
        string identity,
        ArchitectureApplicabilityMembership membership) =>
        new(identity, "family", membership, new ArchitectureApplicabilityProvenance("family", identity, "policy"));

    private static ArchitectureApplicabilityRecord Record(
        string identity,
        ArchitectureApplicabilityRecordState state,
        params ArchitectureApplicabilityReason[] reasons) =>
        new(identity, "family", state, reasons, new ArchitectureApplicabilityProvenance("family", identity, "policy"));
}
