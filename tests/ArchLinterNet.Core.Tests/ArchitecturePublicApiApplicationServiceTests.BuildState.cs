using ArchLinterNet.Core.BuildState;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Discovery;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Execution.Abstractions;
using ArchLinterNet.Core.Validation;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

public sealed partial class ArchitecturePublicApiApplicationServiceTests
{
    [Test]
    public void Capture_EnsureBuilt_RecreatesRunnerAndReverifiesPostBuildArtifacts()
    {
        const string ArtifactPath = "/fake/repository/root/bin/Release/net10.0/Test.dll";
        ProjectDiscoveryResult discovery = new(
            new[] { AssemblyName }, Array.Empty<string>(), Array.Empty<string>(),
            Array.Empty<ArchitectureProjectDiscoveryDiagnostic>())
        {
            DiscoveredProjects = new[]
            {
                new ArchitectureDiscoveredProject("Test.csproj", AssemblyName, new[] { "net10.0" }),
            },
            ResolvedAssemblyPaths = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [AssemblyName] = ArtifactPath,
            },
        };
        ArchitectureContractDocument document = Document(Contract());
        document.Analysis.Configuration = "Release";
        document.Analysis.TargetFramework = "net10.0";
        FakeRunnerSetupService runnerSetup = new()
        {
            DocumentToReturn = document,
            RunnersToReturn = new Queue<IArchitectureContractRunner>(new IArchitectureContractRunner[]
            {
                new FakeContractRunner(Session(document, discovery: discovery)),
                new FakeContractRunner(Session(document, discovery: discovery)),
            }),
        };
        FakeBuildStatePreparationService preparation = new();
        ArchitecturePublicApiApplicationService service = new(
            runnerSetup, preparation, new FakePublicApiSnapshotStore());

        PublicApiCaptureOutcome outcome = service.Capture(new PublicApiCaptureRequest
        {
            PolicyPath = PolicyPath,
            ContractId = ContractId,
            OutputPath = SnapshotPath,
            PreparationMode = BuildPreparationMode.EnsureBuilt,
            NoRestore = true,
        });

        Assert.Multiple(() =>
        {
            Assert.That(outcome.Succeeded, Is.True);
            Assert.That(runnerSetup.BuildRunnerCallCount, Is.EqualTo(1));
            Assert.That(runnerSetup.BuildRunnerForPostBuildCallCount, Is.EqualTo(1));
            Assert.That(preparation.Requests.Select(request => request.PreparationMode), Is.EqualTo(new[]
            {
                BuildPreparationMode.EnsureBuilt,
                BuildPreparationMode.Ordinary,
            }));
            Assert.That(preparation.Requests, Is.All.Property(nameof(BuildStatePreflightRequest.NoRestore)).True);
            Assert.That(preparation.Requests.Select(request => request.RequestedConfiguration),
                Is.EqualTo(new[] { "Release", "Release" }));
            Assert.That(preparation.Requests.Select(request => request.RequestedTargetFramework),
                Is.EqualTo(new[] { "net10.0", "net10.0" }));
            Assert.That(preparation.Requests.Select(request => request.Resolution.ResolvedAssemblyPaths[AssemblyName]),
                Is.EqualTo(new[] { ArtifactPath, ArtifactPath }));
        });
    }
}
