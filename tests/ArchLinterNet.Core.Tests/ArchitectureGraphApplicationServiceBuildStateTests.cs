using ArchLinterNet.Core.BuildState;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Discovery;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Execution.Abstractions;
using ArchLinterNet.Core.Graph;
using ArchLinterNet.Core.Model;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

// Focused composition tests for graph build-state routing. The graph must execute only after
// the initial preflight has authorized the explicit build, and then on the fresh post-build
// runner that supplies isolated shared-framework probing.
[TestFixture]
public sealed class ArchitectureGraphApplicationServiceBuildStateTests
{
    [Test]
    public void BuildGraph_EnsureBuilt_RerunsAgainstFreshPostBuildRunnerAndForwardsOutputContext()
    {
        ArchitectureContractDocument document = new() { Version = 1, Name = "Fake" };
        ProjectDiscoveryResult discovery = ProjectDiscoveryResult.Empty with
        {
            DiscoveredProjects = new[] { new ArchitectureDiscoveredProject("Fixture.csproj", "Fixture", ["net10.0"]) },
        };
        FakeContractRunner firstRunner = new(CreateSession(document, discovery));
        FakeContractRunner secondRunner = new(CreateSession(document, discovery));
        var setupService = new FakeRunnerSetupService
        {
            DocumentToReturn = document,
            RunnersToReturn = new Queue<IArchitectureContractRunner>([firstRunner, secondRunner]),
        };
        var preparation = new RecordingBuildStatePreparationService();
        var executor = new FakeContractExecutor();
        var service = new ArchitectureGraphApplicationService(
            setupService, new FakeContractHandlerRegistry(), executor, preparation);

        ArchitectureGraphOutcome outcome = service.BuildGraph(new ArchitectureGraphRequest
        {
            PolicyPath = "unused.yml",
            Mode = "strict",
            PreparationMode = BuildPreparationMode.EnsureBuilt,
            NoRestore = true,
            RequestedConfiguration = "Release",
            RequestedTargetFramework = "net10.0",
            RequestedPlatform = "AnyCPU",
            RequestedRuntimeIdentifier = "win-x64",
        });

        Assert.Multiple(() =>
        {
            Assert.That(outcome.Graph, Is.Not.Null);
            Assert.That(setupService.BuildRunnerForPostBuildCallCount, Is.EqualTo(1));
            Assert.That(preparation.RequestsReceived, Has.Count.EqualTo(2));
            Assert.That(preparation.RequestsReceived[0].PreparationMode, Is.EqualTo(BuildPreparationMode.EnsureBuilt));
            Assert.That(preparation.RequestsReceived[1].PreparationMode, Is.EqualTo(BuildPreparationMode.Ordinary));
            Assert.That(preparation.RequestsReceived, Has.All.Matches<BuildStatePreflightRequest>(request =>
                request.NoRestore
                && request.RequestedConfiguration == "Release"
                && request.RequestedTargetFramework == "net10.0"
                && request.RequestedPlatform == "AnyCPU"
                && request.RequestedRuntimeIdentifier == "win-x64"));
            Assert.That(firstRunner.StrictArgumentsReceived, Is.Empty);
            Assert.That(secondRunner.StrictArgumentsReceived, Is.Not.Empty);
            Assert.That(executor.ModesReceived, Is.EqualTo(["strict"]));
        });
    }

    [Test]
    public void BuildGraph_BlockedPreflight_DoesNotContinueWithOrdinaryFacts()
    {
        ArchitectureContractDocument document = new() { Version = 1, Name = "Fake" };
        ProjectDiscoveryResult discovery = ProjectDiscoveryResult.Empty with
        {
            DiscoveredProjects = new[] { new ArchitectureDiscoveredProject("Fixture.csproj", "Fixture", ["net10.0"]) },
        };
        FakeContractRunner runner = new(CreateSession(document, discovery));
        var setupService = new FakeRunnerSetupService
        {
            DocumentToReturn = document,
            RunnerToReturn = runner,
        };
        var preparation = new RecordingBuildStatePreparationService
        {
            ResultToReturn = new BuildStatePreflightResult(new[]
            {
                new BuildStatePreflightDiagnostic(
                    "build-state-preflight", "Fixture.csproj", BuildStatePreflightState.MissingArtifact,
                    new BuildStatePreflightEvidence("Fixture.csproj", "Fixture")),
            }),
        };
        var executor = new FakeContractExecutor();
        var service = new ArchitectureGraphApplicationService(
            setupService, new FakeContractHandlerRegistry(), executor, preparation);

        Assert.That(
            () => service.BuildGraph(new ArchitectureGraphRequest
            {
                PolicyPath = "unused.yml",
                Mode = "strict",
                NoRestore = true,
            }),
            Throws.InvalidOperationException
                .With.Message.Contains("Graph build-state preflight is blocked"));

        Assert.Multiple(() =>
        {
            Assert.That(setupService.BuildRunnerForPostBuildCallCount, Is.EqualTo(0));
            Assert.That(executor.ModesReceived, Is.Empty);
        });
    }

    private static ArchitectureAnalysisSession CreateSession(
        ArchitectureContractDocument document, ProjectDiscoveryResult discovery)
    {
        var context = new ArchitectureAnalysisContext(
            "/fake/repository/root",
            Array.Empty<System.Reflection.Assembly>(),
            ["Fixture"],
            Array.Empty<string>(),
            projectDiscovery: discovery);
        return new ArchitectureAnalysisSession(
            context, document, selectedContractIds: null, enableUnmatchedIgnoreTracking: false,
            preprocessorSymbols: null);
    }

    private sealed class RecordingBuildStatePreparationService : IBuildStatePreparationService
    {
        public List<BuildStatePreflightRequest> RequestsReceived { get; } = new();

        public BuildStatePreflightResult ResultToReturn { get; init; } =
            new(Array.Empty<BuildStatePreflightDiagnostic>());

        public BuildStatePreflightResult Prepare(BuildStatePreflightRequest request)
        {
            RequestsReceived.Add(request);
            return ResultToReturn;
        }
    }
}
