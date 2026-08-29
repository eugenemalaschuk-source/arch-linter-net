using ArchLinterNet.Core.Model;
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
}
