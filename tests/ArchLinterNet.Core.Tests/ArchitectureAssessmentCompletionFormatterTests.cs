using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class ArchitectureAssessmentCompletionFormatterTests
{
    [Test]
    public void HumanFormatter_HandlesNoCompletionAndNoReasons()
    {
        ArchitectureAssessmentCompletionEvidence completion = new(
            ArchitectureAssessmentCompletionState.Pass,
            Array.Empty<ArchitectureApplicabilityAssessment>(),
            Array.Empty<ArchitectureApplicabilityReason>());

        Assert.Multiple(() =>
        {
            Assert.That(ArchitectureDiagnosticFormatter.FormatAssessmentCompletionForHumans(null), Is.Empty);
            Assert.That(ArchitectureDiagnosticFormatter.FormatAssessmentCompletionForHumans(completion),
                Is.EqualTo("Assessment completion: pass; reasons: none"));
        });
    }

    [Test]
    public void HumanFormatter_UsesCanonicalCompletionAndReasonProvenance()
    {
        ArchitectureAssessmentCompletionEvidence completion = new(
            ArchitectureAssessmentCompletionState.Unassessable,
            Array.Empty<ArchitectureApplicabilityAssessment>(),
            [new ArchitectureApplicabilityReason("stale_declaration", "family", "control", "policy")]);

        string formatted = ArchitectureDiagnosticFormatter.FormatAssessmentCompletionForHumans(completion);

        Assert.That(formatted, Is.EqualTo(
            "Assessment completion: unassessable; reasons: stale_declaration (family=family, control=control, policy=policy)"));
    }

    [Test]
    public void HumanFormatter_OmitsEmptyPolicyIdentity()
    {
        ArchitectureAssessmentCompletionEvidence completion = new(
            ArchitectureAssessmentCompletionState.Unassessable,
            Array.Empty<ArchitectureApplicabilityAssessment>(),
            [new ArchitectureApplicabilityReason("stale_declaration", "family", "control")]);

        string formatted = ArchitectureDiagnosticFormatter.FormatAssessmentCompletionForHumans(completion);

        Assert.That(formatted, Is.EqualTo(
            "Assessment completion: unassessable; reasons: stale_declaration (family=family, control=control)"));
    }
}
