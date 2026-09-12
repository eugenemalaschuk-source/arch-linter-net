using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Validation;
using NUnit.Framework;
using static ArchLinterNet.Core.Tests.ArchitecturePublicApiApplicationServiceTests;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class ArchitecturePublicApiApplicationServiceResolverRegressionTests
{
    [Test]
    public void Capture_CancelledBeforeResolution_DoesNotBuildScanOrReadSnapshot()
    {
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();
        ArchitectureContractDocument document = Document(Contract());
        ArchitectureAnalysisSession session = Session(document);
        var runnerSetup = new FakeRunnerSetupService
        {
            DocumentToReturn = document,
            RunnerToReturn = new FakeContractRunner(session),
        };
        var store = new FakePublicApiSnapshotStore();
        var service = new ArchitecturePublicApiApplicationService(
            runnerSetup, new FakeBuildStatePreparationService(), store);

        Assert.Throws<OperationCanceledException>(() => service.Capture(new PublicApiCaptureRequest
        {
            PolicyPath = PolicyPath,
            ContractId = ContractId,
            OutputPath = SnapshotPath,
            CancellationToken = cancellation.Token,
        }));

        Assert.Multiple(() =>
        {
            Assert.That(runnerSetup.BuildRunnerCallCount, Is.Zero);
            Assert.That(session.PublicApiSurfaceMaterializationCount, Is.Zero);
            Assert.That(store.ReadCallCount, Is.Zero);
        });
    }

    [Test]
    public void Diff_UsesOneSurfaceMaterializationAndOneSnapshotRead()
    {
        ArchitectureContractDocument document = Document(Contract(apiSnapshot: SnapshotPath));
        ArchitectureAnalysisSession session = Session(document);
        var runnerSetup = new FakeRunnerSetupService
        {
            DocumentToReturn = document,
            RunnerToReturn = new FakeContractRunner(session),
        };
        var store = new FakePublicApiSnapshotStore();
        var service = new ArchitecturePublicApiApplicationService(
            runnerSetup, new FakeBuildStatePreparationService(), store);

        PublicApiDiffOutcome outcome = service.Diff(new PublicApiDiffRequest
        {
            PolicyPath = PolicyPath,
            ContractId = ContractId,
            SnapshotPath = SnapshotPath,
        });

        Assert.Multiple(() =>
        {
            Assert.That(outcome.Succeeded, Is.True, outcome.Error);
            Assert.That(runnerSetup.BuildRunnerCallCount, Is.EqualTo(1));
            Assert.That(runnerSetup.BuildRunnerForPostBuildCallCount, Is.Zero);
            Assert.That(session.PublicApiSurfaceMaterializationCount, Is.EqualTo(1));
            Assert.That(store.ReadCallCount, Is.EqualTo(1));
        });
    }
}
