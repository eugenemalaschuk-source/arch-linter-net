using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Validation;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class ArchitectureExternalEvidenceApplicabilityProjectorTests
{
    [Test]
    public void Project_EmitsOneExpectedEntryPerDeclarationWithRequiredOrOptionalMembership()
    {
        (IReadOnlyList<ArchitectureApplicabilityExpectedEntry> expected, _) =
            ArchitectureExternalEvidenceApplicabilityProjector.Project(
                [Requirement("zeta", required: false), Requirement("alpha", required: true)],
                []);

        Assert.Multiple(() =>
        {
            Assert.That(expected.Select(entry => entry.ControlIdentity), Is.EqualTo(["alpha", "zeta"]));
            Assert.That(expected[0].Family, Is.EqualTo("external_diagnostics"));
            Assert.That(expected[0].Membership, Is.EqualTo(ArchitectureApplicabilityMembership.Required));
            Assert.That(expected[0].Provenance, Is.EqualTo(new ArchitectureApplicabilityProvenance(
                "external_diagnostics", "alpha", "alpha")));
            Assert.That(expected[1].Membership, Is.EqualTo(ArchitectureApplicabilityMembership.Optional));
        });
    }

    [Test]
    public void Project_ValidResultIsEvaluableEvenWhenItHasNoSelectedDiagnostics()
    {
        (IReadOnlyList<ArchitectureApplicabilityExpectedEntry> expected,
            IReadOnlyList<ArchitectureApplicabilityRecord> records) =
            ArchitectureExternalEvidenceApplicabilityProjector.Project(
                [Requirement("external.scan")],
                [ReadResult("external.scan", SarifEvidenceTrustStatus.Valid)]);

        ArchitectureApplicabilityRecord record = records.Single();
        ArchitectureAssessmentCompletionEvidence completion = ArchitectureApplicabilityEvaluator.Evaluate(
            expected,
            records,
            conformancePassed: true)!;

        Assert.Multiple(() =>
        {
            Assert.That(record.State, Is.EqualTo(ArchitectureApplicabilityRecordState.Evaluable));
            Assert.That(record.Reasons, Is.Empty);
            Assert.That(record.Provenance.PolicyIdentity, Is.EqualTo("external.scan"));
            Assert.That(completion.State, Is.EqualTo(ArchitectureAssessmentCompletionState.Pass));
        });
    }

    [TestCase(SarifEvidenceTrustStatus.OptionalNotConfigured)]
    [TestCase(SarifEvidenceTrustStatus.MissingOptionalInput)]
    public void Project_DeliberateOptionalAbsenceIsNotApplicable(SarifEvidenceTrustStatus status)
    {
        (_, IReadOnlyList<ArchitectureApplicabilityRecord> records) =
            ArchitectureExternalEvidenceApplicabilityProjector.Project(
                [Requirement("optional.scan", required: false)],
                [ReadResult("optional.scan", status)]);

        Assert.Multiple(() =>
        {
            Assert.That(records.Single().State, Is.EqualTo(ArchitectureApplicabilityRecordState.NotApplicable));
            Assert.That(records.Single().Reasons, Is.Empty);
        });
    }

    [TestCase(
        SarifEvidenceTrustStatus.MissingRequiredInput,
        ArchitectureApplicabilityReasonCodes.MissingRequiredInput)]
    [TestCase(
        SarifEvidenceTrustStatus.WrongLogicalId,
        ArchitectureApplicabilityReasonCodes.WrongExternalEvidenceIdentity)]
    [TestCase(
        SarifEvidenceTrustStatus.WrongRepository,
        ArchitectureApplicabilityReasonCodes.WrongExternalRepository)]
    [TestCase(
        SarifEvidenceTrustStatus.WrongRevision,
        ArchitectureApplicabilityReasonCodes.WrongExternalRevision)]
    [TestCase(
        SarifEvidenceTrustStatus.WrongScope,
        ArchitectureApplicabilityReasonCodes.WrongExternalScope)]
    public void Project_MapsContextAndRequiredInputTrustFailuresToCanonicalReasons(
        SarifEvidenceTrustStatus status,
        string reasonCode)
    {
        (_, IReadOnlyList<ArchitectureApplicabilityRecord> records) =
            ArchitectureExternalEvidenceApplicabilityProjector.Project(
                [Requirement("external.scan")],
                [ReadResult("external.scan", status)]);

        ArchitectureApplicabilityRecord record = records.Single();
        Assert.Multiple(() =>
        {
            Assert.That(record.State, Is.EqualTo(ArchitectureApplicabilityRecordState.Unassessable));
            Assert.That(record.Reasons.Select(reason => reason.Code), Is.EqualTo([reasonCode]));
            Assert.That(record.Reasons.Single().Provenance, Is.EqualTo(record.Provenance));
        });
    }

    [TestCase(SarifEvidenceTrustStatus.MissingLogicalId)]
    [TestCase(SarifEvidenceTrustStatus.UnreadableInput)]
    [TestCase(SarifEvidenceTrustStatus.MalformedInput)]
    [TestCase(SarifEvidenceTrustStatus.UnsupportedVersion)]
    [TestCase(SarifEvidenceTrustStatus.UnsupportedShape)]
    [TestCase(SarifEvidenceTrustStatus.MissingExpectedRun)]
    [TestCase(SarifEvidenceTrustStatus.AmbiguousExpectedRun)]
    [TestCase(SarifEvidenceTrustStatus.FailedExecution)]
    [TestCase(SarifEvidenceTrustStatus.IncompleteExecution)]
    [TestCase(SarifEvidenceTrustStatus.ConflictingContext)]
    [TestCase(SarifEvidenceTrustStatus.TooManyRuns)]
    [TestCase(SarifEvidenceTrustStatus.TooManyResults)]
    public void Project_MapsOtherTrustFailuresToMalformedExternalInput(
        SarifEvidenceTrustStatus status)
    {
        (_, IReadOnlyList<ArchitectureApplicabilityRecord> records) =
            ArchitectureExternalEvidenceApplicabilityProjector.Project(
                [Requirement("external.scan")],
                [ReadResult("external.scan", status)]);

        Assert.That(
            records.Single().Reasons.Select(reason => reason.Code),
            Is.EqualTo([ArchitectureApplicabilityReasonCodes.MalformedExternalInput]));
    }

    [Test]
    public void Project_RequiredFilterMismatchOverridesValidStatusWithStaleDeclaration()
    {
        SarifExternalDiagnosticSelectionResult selection = new(
            filterMismatches:
            [
                new SarifExternalDiagnosticFilterMismatch(
                    "external.scan",
                    SarifExternalDiagnosticFilterDimension.RuleId,
                    "SEC404"),
            ]);

        (_, IReadOnlyList<ArchitectureApplicabilityRecord> records) =
            ArchitectureExternalEvidenceApplicabilityProjector.Project(
                [Requirement("external.scan")],
                [ReadResult("external.scan", SarifEvidenceTrustStatus.Valid)],
                selection);

        ArchitectureApplicabilityRecord record = records.Single();
        Assert.Multiple(() =>
        {
            Assert.That(record.State, Is.EqualTo(ArchitectureApplicabilityRecordState.Unassessable));
            Assert.That(record.Reasons.Select(reason => reason.Code), Is.EqualTo([
                ArchitectureApplicabilityReasonCodes.StaleDeclaration]));
        });
    }

    [Test]
    public void Project_FilterMismatchForAnotherIdentityDoesNotChangeValidEvidence()
    {
        SarifExternalDiagnosticSelectionResult selection = new(
            filterMismatches:
            [
                new SarifExternalDiagnosticFilterMismatch(
                    "other.scan",
                    SarifExternalDiagnosticFilterDimension.RuleId,
                    "SEC404"),
            ]);

        (_, IReadOnlyList<ArchitectureApplicabilityRecord> records) =
            ArchitectureExternalEvidenceApplicabilityProjector.Project(
                [Requirement("external.scan")],
                [ReadResult("external.scan", SarifEvidenceTrustStatus.Valid)],
                selection);

        Assert.That(records.Single().State, Is.EqualTo(ArchitectureApplicabilityRecordState.Evaluable));
    }

    [Test]
    public void Project_DoesNotFabricateMissingOrCollapseDuplicateRecords()
    {
        IReadOnlyList<ArchitectureApplicabilityExpectedEntry> expected;
        IReadOnlyList<ArchitectureApplicabilityRecord> missingRecords;
        (expected, missingRecords) = ArchitectureExternalEvidenceApplicabilityProjector.Project(
            [Requirement("external.scan")],
            []);
        ArchitectureAssessmentCompletionEvidence missingCompletion = ArchitectureApplicabilityEvaluator.Evaluate(
            expected,
            missingRecords,
            conformancePassed: true)!;

        IReadOnlyList<ArchitectureApplicabilityRecord> duplicateRecords =
            ArchitectureExternalEvidenceApplicabilityProjector.ProjectRecords(
                [Requirement("external.scan")],
                [
                    ReadResult("external.scan", SarifEvidenceTrustStatus.Valid),
                    ReadResult("external.scan", SarifEvidenceTrustStatus.Valid),
                ]);
        ArchitectureAssessmentCompletionEvidence duplicateCompletion = ArchitectureApplicabilityEvaluator.Evaluate(
            expected,
            duplicateRecords,
            conformancePassed: true)!;

        Assert.Multiple(() =>
        {
            Assert.That(missingRecords, Is.Empty);
            Assert.That(missingCompletion.Reasons.Select(reason => reason.Code), Contains.Item(
                ArchitectureApplicabilityReasonCodes.MissingApplicabilityRecord));
            Assert.That(duplicateRecords, Has.Count.EqualTo(2));
            Assert.That(duplicateCompletion.Reasons.Select(reason => reason.Code), Contains.Item(
                ArchitectureApplicabilityReasonCodes.DuplicateApplicabilityRecordIdentity));
        });
    }

    [Test]
    public void Project_PreservesOrphanRecordForCommonEvaluatorIntegrityCheck()
    {
        (IReadOnlyList<ArchitectureApplicabilityExpectedEntry> expected,
            IReadOnlyList<ArchitectureApplicabilityRecord> records) =
            ArchitectureExternalEvidenceApplicabilityProjector.Project(
                [Requirement("declared.scan")],
                [ReadResult("orphan.scan", SarifEvidenceTrustStatus.Valid)]);

        ArchitectureAssessmentCompletionEvidence completion = ArchitectureApplicabilityEvaluator.Evaluate(
            expected,
            records,
            conformancePassed: true)!;

        Assert.Multiple(() =>
        {
            Assert.That(records, Has.Count.EqualTo(1));
            Assert.That(completion.Reasons.Select(reason => reason.Code), Contains.Item(
                ArchitectureApplicabilityReasonCodes.MissingApplicabilityRecord));
            Assert.That(completion.Reasons.Select(reason => reason.Code), Contains.Item(
                ArchitectureApplicabilityReasonCodes.UnknownApplicabilityRecordIdentity));
        });
    }

    private static ArchitectureExternalEvidenceRequirement Requirement(
        string id,
        bool required = true) => new()
        {
            Id = id,
            Format = "sarif",
            Required = required,
            Tool = "Acme.Scanner",
            Run = "assessment-42",
        };

    private static SarifEvidenceReadResult ReadResult(
        string logicalId,
        SarifEvidenceTrustStatus status) => new(
            status,
            status.ToString(),
            "test detail",
            new SarifEvidenceProvenance(logicalId, null, null, null, null, null, null, null));
}
