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
    public void BuildGraph_PreparedPostBuildState_UsesIsolatedRunnerWithoutAnotherEnsureBuiltRequest()
    {
        ArchitectureContractDocument document = new() { Version = 1, Name = "Fake" };
        ProjectDiscoveryResult discovery = ProjectDiscoveryResult.Empty with
        {
            DiscoveredProjects = new[] { new ArchitectureDiscoveredProject("Fixture.csproj", "Fixture", ["net10.0"]) },
        };
        FakeContractRunner preparedRunner = new(CreateSession(document, discovery));
        var setupService = new FakeRunnerSetupService
        {
            DocumentToReturn = document,
            RunnersToReturn = new Queue<IArchitectureContractRunner>([preparedRunner]),
        };
        var preparation = new RecordingBuildStatePreparationService();
        var executor = new FakeContractExecutor();
        var service = new ArchitectureGraphApplicationService(
            setupService, new FakeContractHandlerRegistry(), executor, preparation);

        ArchitectureGraphOutcome outcome = service.BuildGraph(new ArchitectureGraphRequest
        {
            PolicyPath = "unused.yml",
            Mode = "strict",
            PreparationMode = BuildPreparationMode.Ordinary,
            RequestedTargetFramework = "net10.0",
            UsePreparedPostBuildState = true,
            PreparedPostBuildRunner = CreatePreparedRunner(discovery),
        });

        Assert.Multiple(() =>
        {
            Assert.That(outcome.Graph, Is.Not.Null);
            Assert.That(setupService.BuildRunnerCallCount, Is.EqualTo(0));
            Assert.That(setupService.BuildRunnerForPostBuildCallCount, Is.EqualTo(0));
            Assert.That(setupService.MaterializePreparedRunnerCallCount, Is.EqualTo(1));
            Assert.That(preparation.RequestsReceived, Has.Count.EqualTo(1));
            Assert.That(preparation.RequestsReceived,
                Has.All.Matches<BuildStatePreflightRequest>(request =>
                    request.PreparationMode == BuildPreparationMode.Ordinary));
            Assert.That(preparedRunner.StrictArgumentsReceived, Is.Not.Empty);
            Assert.That(executor.ModesReceived, Is.EqualTo(["strict"]));
        });
    }

    [Test]
    public void BuildGraph_WithoutBuildStateOptions_UsesOrdinaryRunnerWithoutAPreparationService()
    {
        ArchitectureContractDocument document = new() { Version = 1, Name = "Fake" };
        FakeContractRunner runner = new(CreateSession(document, ProjectDiscoveryResult.Empty));
        var setupService = new FakeRunnerSetupService { DocumentToReturn = document, RunnerToReturn = runner };
        var executor = new FakeContractExecutor();
        var service = new ArchitectureGraphApplicationService(
            setupService, new FakeContractHandlerRegistry(), executor);

        ArchitectureGraphOutcome outcome = service.BuildGraph(new ArchitectureGraphRequest
        {
            PolicyPath = "unused.yml",
            Mode = "strict",
        });

        Assert.Multiple(() =>
        {
            Assert.That(outcome.Graph, Is.Not.Null);
            Assert.That(setupService.BuildRunnerForPostBuildCallCount, Is.EqualTo(0));
            Assert.That(executor.ModesReceived, Is.EqualTo(["strict"]));
        });
    }

    [Test]
    public void BuildGraph_PreparedPostBuildStateWithBlockedReceiptVerification_FailsClosed()
    {
        ArchitectureContractDocument document = new() { Version = 1, Name = "Fake" };
        ProjectDiscoveryResult discovery = DiscoveryWithFixtureProject();
        FakeContractRunner runner = new(CreateSession(document, discovery));
        var setupService = new FakeRunnerSetupService
        {
            DocumentToReturn = document,
            RunnerToReturn = runner,
        };
        var preparation = new RecordingBuildStatePreparationService { ResultToReturn = BlockingPreflight() };
        var executor = new FakeContractExecutor();
        var service = new ArchitectureGraphApplicationService(
            setupService, new FakeContractHandlerRegistry(), executor, preparation);

        Assert.That(
            () => service.BuildGraph(new ArchitectureGraphRequest
            {
                PolicyPath = "unused.yml",
                Mode = "strict",
                UsePreparedPostBuildState = true,
                PreparedPostBuildRunner = CreatePreparedRunner(discovery),
            }),
            Throws.InvalidOperationException.With.Message.Contains("Graph build-state preflight is blocked"));

        Assert.Multiple(() =>
        {
            Assert.That(setupService.MaterializePreparedRunnerCallCount, Is.EqualTo(1));
            Assert.That(setupService.BuildRunnerForPostBuildCallCount, Is.EqualTo(0));
            Assert.That(executor.ModesReceived, Is.Empty);
        });
    }

    [Test]
    public void BuildGraph_EnsureBuiltWithoutDiscoveredProjects_DoesNotCreatePostBuildRunner()
    {
        ArchitectureContractDocument document = new() { Version = 1, Name = "Fake" };
        FakeContractRunner runner = new(CreateSession(document, ProjectDiscoveryResult.Empty));
        var setupService = new FakeRunnerSetupService { DocumentToReturn = document, RunnerToReturn = runner };
        var executor = new FakeContractExecutor();
        var service = new ArchitectureGraphApplicationService(
            setupService, new FakeContractHandlerRegistry(), executor, new RecordingBuildStatePreparationService());

        ArchitectureGraphOutcome outcome = service.BuildGraph(new ArchitectureGraphRequest
        {
            PolicyPath = "unused.yml",
            Mode = "strict",
            PreparationMode = BuildPreparationMode.EnsureBuilt,
        });

        Assert.Multiple(() =>
        {
            Assert.That(outcome.Graph, Is.Not.Null);
            Assert.That(setupService.BuildRunnerForPostBuildCallCount, Is.EqualTo(0));
            Assert.That(executor.ModesReceived, Is.EqualTo(["strict"]));
        });
    }

    [Test]
    public void BuildGraph_EnsureBuiltWithBlockedPostBuildReceiptVerification_FailsClosed()
    {
        ArchitectureContractDocument document = new() { Version = 1, Name = "Fake" };
        ProjectDiscoveryResult discovery = DiscoveryWithFixtureProject();
        FakeContractRunner initialRunner = new(CreateSession(document, discovery));
        FakeContractRunner postBuildRunner = new(CreateSession(document, discovery));
        var setupService = new FakeRunnerSetupService
        {
            DocumentToReturn = document,
            RunnersToReturn = new Queue<IArchitectureContractRunner>([initialRunner, postBuildRunner]),
        };
        var preparation = new RecordingBuildStatePreparationService
        {
            ResultsToReturn = new Queue<BuildStatePreflightResult>([
                new BuildStatePreflightResult(Array.Empty<BuildStatePreflightDiagnostic>()),
                BlockingPreflight(),
            ]),
        };
        var executor = new FakeContractExecutor();
        var service = new ArchitectureGraphApplicationService(
            setupService, new FakeContractHandlerRegistry(), executor, preparation);

        Assert.That(
            () => service.BuildGraph(new ArchitectureGraphRequest
            {
                PolicyPath = "unused.yml",
                Mode = "strict",
                PreparationMode = BuildPreparationMode.EnsureBuilt,
            }),
            Throws.InvalidOperationException.With.Message.Contains("Graph build-state preflight is blocked"));

        Assert.Multiple(() =>
        {
            Assert.That(setupService.BuildRunnerForPostBuildCallCount, Is.EqualTo(1));
            Assert.That(executor.ModesReceived, Is.Empty);
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

    private static ArchitectureRunnerPreparation CreatePreparedRunner(ProjectDiscoveryResult discovery) => new(
        "/fake/repository/root",
        null,
        discovery,
        ResolveAssemblyOutputs: true,
        SelectedAssemblyArtifactPaths: ["/fake/repository/root/bin/Release/net10.0/win-x64/Fixture.dll"],
        CapturedArtifactContentDigests: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["/fake/repository/root/bin/Release/net10.0/win-x64/Fixture.dll"] = "digest",
        },
        MissingAssemblyNames: Array.Empty<string>(),
        IsMetadataReferenceClosureComplete: true);

    private static ProjectDiscoveryResult DiscoveryWithFixtureProject() => ProjectDiscoveryResult.Empty with
    {
        DiscoveredProjects = [new ArchitectureDiscoveredProject("Fixture.csproj", "Fixture", ["net10.0"])],
    };

    private static BuildStatePreflightResult BlockingPreflight() => new([
        new BuildStatePreflightDiagnostic(
            "build-state-preflight", "Fixture.csproj", BuildStatePreflightState.MissingArtifact,
            new BuildStatePreflightEvidence("Fixture.csproj", "Fixture")),
    ]);

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

        public Queue<BuildStatePreflightResult>? ResultsToReturn { get; init; }

        public BuildStatePreflightResult Prepare(BuildStatePreflightRequest request)
        {
            RequestsReceived.Add(request);
            return ResultsToReturn is { Count: > 0 } ? ResultsToReturn.Dequeue() : ResultToReturn;
        }
    }
}
