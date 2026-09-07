using System.Reflection;
using ArchLinterNet.Core.Execution.Results;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;
using ArchLinterNet.Core.Validation;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class ArchitectureAnalysisSnapshotApplicabilityTrustBoundaryTests
{
    [Test]
    public void DeriveAssessmentCompletion_DoesNotAcceptLegacyExecutorSuppliedCompletion()
    {
        var execution = new ArchitectureContractExecutionResult(
            Array.Empty<ArchitectureViolation>(),
            Array.Empty<string>(),
            Array.Empty<ArchitectureViolation>(),
            Array.Empty<ArchitectureCoverageSummary>());
        PropertyInfo? legacyCompletion = typeof(ArchitectureContractExecutionResult).GetProperty(
            "AssessmentCompletionEvidence",
            BindingFlags.Instance | BindingFlags.Public);
        legacyCompletion?.SetValue(execution, new ArchitectureAssessmentCompletionEvidence(
            ArchitectureAssessmentCompletionState.Pass,
            Array.Empty<ArchitectureApplicabilityAssessment>(),
            Array.Empty<ArchitectureApplicabilityReason>()));
        ArchitectureAssessmentCompletionEvidence? completion =
            ArchitectureAnalysisSnapshotApplicabilityProjector.DeriveAssessmentCompletion(execution, true);

        Assert.That(completion, Is.Null);
    }
}
