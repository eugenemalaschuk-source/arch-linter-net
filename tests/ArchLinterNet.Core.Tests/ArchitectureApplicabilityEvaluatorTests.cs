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

    [TestCase("family-a", "control-x")]
    [TestCase("family-x", "control-a")]
    public void MismatchedExpectedProvenance_IsIntegrityEvidenceEvenWhenRecordMatches(
        string provenanceFamily,
        string provenanceControlIdentity)
    {
        ArchitectureApplicabilityExpectedEntry expected = new(
            "control-a", "family-a", ArchitectureApplicabilityMembership.Required,
            new ArchitectureApplicabilityProvenance(provenanceFamily, provenanceControlIdentity, "untrusted-policy"));
        ArchitectureApplicabilityRecord record = new(
            "control-a", "family-a", ArchitectureApplicabilityRecordState.Evaluable,
            Array.Empty<ArchitectureApplicabilityReason>(),
            new ArchitectureApplicabilityProvenance("family-a", "control-a", "trusted-policy"));

        ArchitectureAssessmentCompletionEvidence result =
            ArchitectureApplicabilityEvaluator.Evaluate([expected], [record], conformancePassed: true)!;

        ArchitectureApplicabilityReason reason = result.Reasons.Single();
        Assert.Multiple(() =>
        {
            Assert.That(result.State, Is.EqualTo(ArchitectureAssessmentCompletionState.Unassessable));
            Assert.That(result.Controls.Single().State, Is.Null);
            Assert.That(reason.Code,
                Is.EqualTo(ArchitectureApplicabilityReasonCodes.InvalidApplicabilityExpectedIntegrity));
            Assert.That(reason.Provenance.Family, Is.EqualTo("family-a"));
            Assert.That(reason.Provenance.ControlIdentity, Is.EqualTo("control-a"));
            Assert.That(reason.Provenance.PolicyIdentity, Is.Empty);
        });
    }

    [Test]
    public void DuplicateRecord_IsIntegrityEvidenceAndRetainsEveryProducerProvenance()
    {
        ArchitectureApplicabilityRecord first = new(
            "control-a", "family", ArchitectureApplicabilityRecordState.Evaluable,
            Array.Empty<ArchitectureApplicabilityReason>(),
            new ArchitectureApplicabilityProvenance("family", "control-a", "policy-z"));
        ArchitectureApplicabilityRecord second = new(
            "control-a", "family", ArchitectureApplicabilityRecordState.Evaluable,
            Array.Empty<ArchitectureApplicabilityReason>(),
            new ArchitectureApplicabilityProvenance("family", "control-a", "policy-a"));
        ArchitectureAssessmentCompletionEvidence result =
            ArchitectureApplicabilityEvaluator.Evaluate(
                [Entry("control-a", ArchitectureApplicabilityMembership.Required)],
                [first, second],
                conformancePassed: true)!;

        ArchitectureApplicabilityAssessment assessment = result.Controls.Single();
        Assert.Multiple(() =>
        {
            Assert.That(result.State, Is.EqualTo(ArchitectureAssessmentCompletionState.Unassessable));
            Assert.That(assessment.Record, Is.Null);
            Assert.That(assessment.State, Is.Null);
            Assert.That(result.Reasons.Select(reason => reason.Code), Is.EqualTo(
                [
                    ArchitectureApplicabilityReasonCodes.DuplicateApplicabilityRecordIdentity,
                    ArchitectureApplicabilityReasonCodes.DuplicateApplicabilityRecordIdentity,
                ]));
            Assert.That(result.Reasons.Select(reason => reason.Provenance.PolicyIdentity), Is.EqualTo(["policy-a", "policy-z"]));
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
    public void MissingOptionalAndNotApplicableRecords_AreCollectionIntegrityEvidence()
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
            Assert.That(result.State, Is.EqualTo(ArchitectureAssessmentCompletionState.Unassessable));
            Assert.That(result.RequiredCount, Is.EqualTo(1));
            Assert.That(result.RequiredEvaluableCount, Is.EqualTo(1));
            Assert.That(result.Controls.Single(control => control.ControlIdentity == "control-optional").IntegrityReasons
                .Select(reason => reason.Code), Is.EqualTo([ArchitectureApplicabilityReasonCodes.MissingApplicabilityRecord]));
            Assert.That(result.Controls.Single(control => control.ControlIdentity == "control-not-applicable").IntegrityReasons
                .Select(reason => reason.Code), Is.EqualTo([ArchitectureApplicabilityReasonCodes.MissingApplicabilityRecord]));
        });
    }

    [Test]
    public void ReorderedDuplicateExpectedEntries_ProduceIdenticalCanonicalAssessment()
    {
        ArchitectureApplicabilityExpectedEntry required = new(
            "control-a", "family", ArchitectureApplicabilityMembership.Required,
            new ArchitectureApplicabilityProvenance("family", "control-a", "policy-z"));
        ArchitectureApplicabilityExpectedEntry optional = new(
            "control-a", "family", ArchitectureApplicabilityMembership.Optional,
            new ArchitectureApplicabilityProvenance("family", "control-a", "policy-a"));
        ArchitectureApplicabilityRecord record = new(
            "control-a", "family", ArchitectureApplicabilityRecordState.Evaluable,
            Array.Empty<ArchitectureApplicabilityReason>(),
            new ArchitectureApplicabilityProvenance("family", "control-a", "policy-z"));

        ArchitectureAssessmentCompletionEvidence first = ArchitectureApplicabilityEvaluator.Evaluate(
            [optional, required], [record], conformancePassed: true)!;
        ArchitectureAssessmentCompletionEvidence second = ArchitectureApplicabilityEvaluator.Evaluate(
            [required, optional], [record], conformancePassed: true)!;

        Assert.Multiple(() =>
        {
            Assert.That(first.State, Is.EqualTo(ArchitectureAssessmentCompletionState.Unassessable));
            Assert.That(first.RequiredCount, Is.EqualTo(1));
            Assert.That(first.Controls.Single().Expected!.Membership, Is.EqualTo(ArchitectureApplicabilityMembership.Required));
            Assert.That(first.Controls.Single().Expected!.Provenance.PolicyIdentity, Is.EqualTo("policy-z"));
            Assert.That(first.Reasons.Select(reason => reason.Provenance.PolicyIdentity), Is.EqualTo(["policy-a", "policy-z"]));
            Assert.That(second.State, Is.EqualTo(first.State));
            Assert.That(second.RequiredCount, Is.EqualTo(first.RequiredCount));
            Assert.That(second.Controls.Single().Expected!.Membership,
                Is.EqualTo(first.Controls.Single().Expected!.Membership));
            Assert.That(second.Controls.Single().Expected!.Provenance.PolicyIdentity,
                Is.EqualTo(first.Controls.Single().Expected!.Provenance.PolicyIdentity));
            Assert.That(second.Reasons.Select(reason => reason.Provenance.PolicyIdentity),
                Is.EqualTo(first.Reasons.Select(reason => reason.Provenance.PolicyIdentity)));
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
    public void UnreasonedUnassessableRecord_IsIntegrityEvidence()
    {
        ArchitectureAssessmentCompletionEvidence result =
            ArchitectureApplicabilityEvaluator.Evaluate(
                [Entry("control-a", ArchitectureApplicabilityMembership.Required)],
                [Record("control-a", ArchitectureApplicabilityRecordState.Unassessable)],
                conformancePassed: true)!;

        Assert.Multiple(() =>
        {
            Assert.That(result.State, Is.EqualTo(ArchitectureAssessmentCompletionState.Unassessable));
            Assert.That(result.Reasons.Single().Code,
                Is.EqualTo(ArchitectureApplicabilityReasonCodes.InvalidApplicabilityRecordIntegrity));
        });
    }

    [Test]
    public void TrustedEvidenceWithOrdinaryFailure_ReturnsFail()
    {
        ArchitectureAssessmentCompletionEvidence result =
            ArchitectureApplicabilityEvaluator.Evaluate(
                [Entry("control-a", ArchitectureApplicabilityMembership.Required)],
                [Record("control-a", ArchitectureApplicabilityRecordState.Evaluable)],
                conformancePassed: false)!;

        Assert.That(result.State, Is.EqualTo(ArchitectureAssessmentCompletionState.Fail));
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
