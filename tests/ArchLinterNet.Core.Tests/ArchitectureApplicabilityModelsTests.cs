using ArchLinterNet.Core.Model;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class ArchitectureApplicabilityModelsTests
{
    [Test]
    public void ProvenanceAndExpectedEntry_ExposeCanonicalAliases()
    {
        ArchitectureApplicabilityProvenance provenance = new("family", "control", "policy");
        ArchitectureApplicabilityExpectedEntry entry = new(
            "control", "family", ArchitectureApplicabilityMembership.Required, provenance);

        Assert.Multiple(() =>
        {
            Assert.That(provenance.EffectiveControlId, Is.EqualTo("control"));
            Assert.That(provenance.PolicyId, Is.EqualTo("policy"));
            Assert.That(entry.ControlIdentity, Is.EqualTo("control"));
            Assert.That(entry.EffectiveControlId, Is.EqualTo("control"));
            Assert.That(entry.Provenance, Is.SameAs(provenance));
        });
    }

    [Test]
    public void RecordAndAssessment_OrderReasonsAndHideInvalidState()
    {
        ArchitectureApplicabilityReason zReason = new("z", "family", "control", "policy-z");
        ArchitectureApplicabilityReason aReason = new("a", "family", "control", "policy-a");
        ArchitectureApplicabilityRecord record = new(
            "control", "family", ArchitectureApplicabilityRecordState.Unassessable, [zReason, aReason]);
        ArchitectureApplicabilityAssessment assessment = new(
            new ArchitectureApplicabilityExpectedEntry(
                "control", "family", ArchitectureApplicabilityMembership.Required),
            record,
            [zReason, aReason]);

        Assert.Multiple(() =>
        {
            Assert.That(record.EffectiveControlId, Is.EqualTo("control"));
            Assert.That(record.Reasons.Select(reason => reason.Code), Is.EqualTo(["a", "z"]));
            Assert.That(assessment.EffectiveControlId, Is.EqualTo("control"));
            Assert.That(assessment.IsIntegrityValid, Is.False);
            Assert.That(assessment.State, Is.Null);
        });
    }

    [Test]
    public void Completion_OrdersControlsAndCalculatesRequiredCounts()
    {
        ArchitectureApplicabilityAssessment evaluable = Assessment(
            "control-a", ArchitectureApplicabilityMembership.Required, ArchitectureApplicabilityRecordState.Evaluable);
        ArchitectureApplicabilityAssessment unassessable = Assessment(
            "control-z", ArchitectureApplicabilityMembership.Required, ArchitectureApplicabilityRecordState.Unassessable);
        ArchitectureApplicabilityAssessment optional = Assessment(
            "control-b", ArchitectureApplicabilityMembership.Optional, ArchitectureApplicabilityRecordState.NotApplicable);
        ArchitectureAssessmentCompletionEvidence completion = new(
            ArchitectureAssessmentCompletionState.Unassessable,
            [unassessable, optional, evaluable],
            [new ArchitectureApplicabilityReason("z", "family", "control-z"), new ArchitectureApplicabilityReason("a", "family", "control-a")]);

        Assert.Multiple(() =>
        {
            Assert.That(completion.Completion, Is.EqualTo(ArchitectureAssessmentCompletionState.Unassessable));
            Assert.That(completion.IsUnassessable, Is.True);
            Assert.That(completion.Controls.Select(control => control.ControlIdentity),
                Is.EqualTo(["control-a", "control-b", "control-z"]));
            Assert.That(completion.Reasons.Select(reason => reason.Code), Is.EqualTo(["a", "z"]));
            Assert.That(completion.RequiredCount, Is.EqualTo(2));
            Assert.That(completion.RequiredEvaluableCount, Is.EqualTo(1));
            Assert.That(completion.RequiredUnassessableCount, Is.EqualTo(1));
        });
    }

    [TestCase(null, "control")]
    [TestCase("family", null)]
    public void Provenance_RejectsMissingCanonicalValues(string? family, string? controlIdentity)
    {
        Assert.That(
            () => new ArchitectureApplicabilityProvenance(family!, controlIdentity!),
            Throws.TypeOf<ArgumentException>());
    }

    private static ArchitectureApplicabilityAssessment Assessment(
        string controlIdentity,
        ArchitectureApplicabilityMembership membership,
        ArchitectureApplicabilityRecordState state)
    {
        ArchitectureApplicabilityExpectedEntry expected = new(controlIdentity, "family", membership);
        ArchitectureApplicabilityRecord record = new(controlIdentity, "family", state);
        return new ArchitectureApplicabilityAssessment(expected, record, Array.Empty<ArchitectureApplicabilityReason>());
    }
}
