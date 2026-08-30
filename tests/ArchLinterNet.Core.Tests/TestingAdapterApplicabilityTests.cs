using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;
using ArchLinterNet.Core.Validation;
using ArchLinterNet.Testing;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

public sealed partial class TestingAdapterTests
{
    [Test]
    public void Result_ExposesUnassessableCompletionAndShouldPassDetailsWithoutViolation()
    {
        ArchitectureApplicabilityReason reason = new(
            ArchitectureApplicabilityReasonCodes.MissingRequiredInput,
            new ArchitectureApplicabilityProvenance("topology", "topology-control", "policy-v08"));
        ArchitectureAssessmentCompletionEvidence completion = new(
            ArchitectureAssessmentCompletionState.Unassessable,
            Array.Empty<ArchitectureApplicabilityAssessment>(),
            [reason]);

        var result = new ArchitectureValidationResult(new ArchitectureValidationResultParams(
            Passed: false,
            Violations: Array.Empty<ArchitectureViolation>(),
            Cycles: Array.Empty<string>())
        {
            AssessmentCompletionEvidence = completion,
        });

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => result.ShouldPass())!;

        Assert.Multiple(() =>
        {
            Assert.That(result.AssessmentCompletionEvidence, Is.SameAs(completion));
            Assert.That(result.Findings, Is.Empty);
            Assert.That(exception.Message, Does.Contain("Assessment completion: unassessable"));
            Assert.That(exception.Message, Does.Contain(ArchitectureApplicabilityReasonCodes.MissingRequiredInput));
            Assert.That(exception.Message, Does.Contain("topology-control"));
            Assert.That(exception.Message, Does.Contain("policy-v08"));
            Assert.That(exception.Message, Does.Not.Contain("ArchitectureViolation"));
        });
    }

    [Test]
    public void Result_MapsProjectionAndAddsOnlyProjectedFindingsToNormalizedCollection()
    {
        ArchitectureApplicabilityReason reason = new(
            ArchitectureApplicabilityReasonCodes.MissingRequiredInput,
            new ArchitectureApplicabilityProvenance("topology", "topology-control", "policy-v08"));
        ArchitectureApplicabilityExpectedEntry expected = new(
            "topology-control", "topology", ArchitectureApplicabilityMembership.Required,
            new ArchitectureApplicabilityProvenance("topology", "topology-control", "policy-v08"));
        ArchitectureApplicabilityRecord record = new(
            "topology-control", "topology", ArchitectureApplicabilityRecordState.Unassessable, [reason],
            new ArchitectureApplicabilityProvenance("topology", "topology-control", "policy-v08"))
        {
            TopologyEvidence = new ArchitectureTopologyMappingEvidence(
                "exhaustive",
                "namespace",
                1,
                [new ArchitectureTopologySubjectEvidence(
                    "namespace|project=Example|assembly=Example|subject=Example.App",
                    "Example",
                    "Example",
                    "Example.App",
                    "mapped",
                    ["application"])],
                Array.Empty<ArchitectureTopologyRelationEvidence>(),
                Array.Empty<string>(),
                Array.Empty<ArchitectureTopologyStaleEdgeEvidence>()),
        };
        ArchitectureAssessmentCompletionEvidence completion = ArchitectureApplicabilityEvaluator.Evaluate(
            [expected], [record], conformancePassed: true)!;
        ArchitectureApplicabilityProjection projection = ArchitectureApplicabilityProjector.Project(completion, "strict")!;

        var result = new ArchitectureValidationResult(new ArchitectureValidationResultParams(
            Passed: false,
            Violations: Array.Empty<ArchitectureViolation>(),
            Cycles: Array.Empty<string>())
        {
            Mode = "strict",
            AssessmentCompletionEvidence = completion,
            ApplicabilityProjection = projection,
        });

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => result.ShouldPass())!;
        ArchitectureFinding finding = result.Findings.Single();

        Assert.Multiple(() =>
        {
            Assert.That(result.ApplicabilityProjection, Is.SameAs(projection));
            Assert.That(result.AssessmentCompletionEvidence, Is.SameAs(completion));
            Assert.That(projection.Summary.RequiredCount, Is.EqualTo(1));
            Assert.That(result.ApplicabilityProjection!.Controls.Single().Record!.TopologyEvidence!.MappedSubjectCount,
                Is.EqualTo(1));
            Assert.That(finding, Is.SameAs(projection.Findings.Single()));
            Assert.That(finding.Identity!.SourceType, Is.EqualTo("topology-control"));
            Assert.That(exception.Message, Does.Contain("Assessment completion: unassessable"));
            Assert.That(exception.Message, Does.Contain(ArchitectureApplicabilityReasonCodes.MissingRequiredInput));
        });
    }
}
