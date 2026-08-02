using ArchLinterNet.Core.Validation;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

public sealed partial class ArchitectureAnalysisSnapshotTests
{
    [Test]
    public void Counters_CancelledDuringContractExecution_RetainsPartialFamilyResults()
    {
        Fixture fixture = CreateFixture();
        fixture.ContractExecutor.BeforeReturn = session =>
        {
            session.Context.ProfilingCounters.RecordContractFamilyResults("dependency", 2);
            throw new OperationCanceledException("cancelled after completed dependency contracts");
        };

        using ArchitectureAnalysisSnapshot snapshot = fixture.ApplicationService.CreateSnapshot(CreateSnapshotRequest());

        Assert.Throws<OperationCanceledException>(() => snapshot.Evaluate("strict"));
        Assert.Multiple(() =>
        {
            Assert.That(snapshot.Cancelled, Is.True);
            Assert.That(snapshot.Counters.ContractFamilyResultCounts["dependency"], Is.EqualTo(2));
        });
    }
}
