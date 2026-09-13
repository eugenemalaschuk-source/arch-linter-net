using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Validation;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class ArchitectureAnalysisSnapshotEvaluationDateTests
{
    [Test]
    public void Evaluate_PropagatesExplicitDateToEmptyWaiverLifecycleReceipt()
    {
        ArchitectureAnalysisSnapshotTests.Fixture fixture = ArchitectureAnalysisSnapshotTests.CreateFixture();
        DateOnly evaluationDate = new(2026, 9, 9);

        using ArchitectureAnalysisSnapshot snapshot = fixture.ApplicationService.CreateSnapshot(
            ArchitectureAnalysisSnapshotTests.CreateSnapshotRequest() with
            {
                WaiverEvaluationDate = evaluationDate,
            });
        ValidationOutcome outcome = snapshot.Evaluate("strict");

        Assert.Multiple(() =>
        {
            Assert.That(outcome.WaiverLifecycleAssessment!.Records, Is.Empty);
            Assert.That(outcome.WaiverLifecycleAssessment.EvaluationDate, Is.EqualTo(evaluationDate));
        });
    }
}
