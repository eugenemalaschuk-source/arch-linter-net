using ArchLinterNet.Core.BuildState;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Discovery;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Execution.Abstractions;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Validation;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

// Fake-composition tests for the baseline verify build-state preflight path added alongside
// --ensure-built/--no-restore support (#486): CollectVerifyCandidates' build-state option
// construction, RunBuildStatePreflight's short-circuit branches, and CollectCandidatesCore's
// post-build rerun against a fresh isolated runner.
[TestFixture]
public sealed class ArchitectureBaselineApplicationServiceBuildStateTests
{
    private static readonly string[] _value = { "net10.0" };
    private static readonly string[] _value1 = { "Fixture" };
    private static readonly string[] _value2 = { "Fixture" };
    private static readonly string[] _value3 = { "Fixture" };
    private static readonly string[] _value4 = { "Fixture" };
    private static readonly string[] _value5 = { "Fixture" };
    private static readonly string[] _value6 = { "Fixture" };
    private static readonly string[] _value7 = { "Fixture" };
    private sealed class FakeBuildStatePreparationService : IBuildStatePreparationService
    {
        public int PrepareCallCount { get; private set; }

        public List<BuildStatePreflightRequest> RequestsReceived { get; } = new();

        public Queue<BuildStatePreflightResult>? ResultsToReturn { get; init; }

        public BuildStatePreflightResult ResultToReturn { get; set; } =
            new(Array.Empty<BuildStatePreflightDiagnostic>());

        public BuildStatePreflightResult Prepare(BuildStatePreflightRequest request)
        {
            PrepareCallCount++;
            RequestsReceived.Add(request);
            return ResultsToReturn is { Count: > 0 } ? ResultsToReturn.Dequeue() : ResultToReturn;
        }
    }

    private static ArchitectureContractDocument CreateDocument() => new() { Version = 1, Name = "Fake" };

    private static ArchitectureDiscoveredProject FixtureProject() =>
        new("Fixture.csproj", "Fixture", _value);

    private static ArchitectureAnalysisSession CreateSession(
        ArchitectureContractDocument document,
        ProjectDiscoveryResult? projectDiscovery = null,
        IReadOnlyCollection<string>? missingAssemblyNames = null)
    {
        var context = new ArchitectureAnalysisContext(
            "/fake/repository/root",
            Array.Empty<System.Reflection.Assembly>(),
            missingAssemblyNames ?? Array.Empty<string>(),
            Array.Empty<string>(),
            projectDiscovery: projectDiscovery);

        return new ArchitectureAnalysisSession(
            context, document, selectedContractIds: null, enableUnmatchedIgnoreTracking: true,
            preprocessorSymbols: null);
    }

    private static BuildStatePreflightDiagnostic BlockingDiagnostic(string contractName = "Acme.Module") =>
        new(contractName, null, BuildStatePreflightState.MissingArtifact,
            new BuildStatePreflightEvidence($"{contractName}.csproj", contractName));

    [Test]
    public void Verify_NoDiscovery_SkipsPreparationServiceAndProceedsNormally()
    {
        var document = CreateDocument();
        var runnerSetupService = new FakeRunnerSetupService { DocumentToReturn = document };
        var runner = new FakeContractRunner(CreateSession(document));
        runnerSetupService.RunnerToReturn = runner;
        var preparationService = new FakeBuildStatePreparationService();

        var applicationService = new ArchitectureBaselineApplicationService(
            runnerSetupService, new FakeContractHandlerRegistry(), new FakeContractExecutor(),
            new FakeBaselineGenerator(), new FakeBaselineLoadingService(), preparationService);

        BaselineVerifyOutcome outcome = applicationService.Verify(new BaselineVerifyRequest
        {
            PolicyPath = "unused-by-fakes.arch.yml",
            BaselinePath = "unused-by-fakes.baseline.yml",
            Mode = "all",
            NoRestore = true,
        });

        Assert.Multiple(() =>
        {
            Assert.That(outcome.Succeeded, Is.True);
            Assert.That(preparationService.PrepareCallCount, Is.EqualTo(0));
            Assert.That(runnerSetupService.BuildRunnerForPostBuildCallCount, Is.EqualTo(0));
        });
    }

    [Test]
    public void Verify_DiscoveryWithNoProjects_SkipsPreparationServiceAndProceedsNormally()
    {
        var document = CreateDocument();
        var runnerSetupService = new FakeRunnerSetupService { DocumentToReturn = document };
        var runner = new FakeContractRunner(CreateSession(document, ProjectDiscoveryResult.Empty));
        runnerSetupService.RunnerToReturn = runner;
        var preparationService = new FakeBuildStatePreparationService();

        var applicationService = new ArchitectureBaselineApplicationService(
            runnerSetupService, new FakeContractHandlerRegistry(), new FakeContractExecutor(),
            new FakeBaselineGenerator(), new FakeBaselineLoadingService(), preparationService);

        BaselineVerifyOutcome outcome = applicationService.Verify(new BaselineVerifyRequest
        {
            PolicyPath = "unused-by-fakes.arch.yml",
            BaselinePath = "unused-by-fakes.baseline.yml",
            Mode = "all",
            NoRestore = true,
        });

        Assert.Multiple(() =>
        {
            Assert.That(outcome.Succeeded, Is.True);
            Assert.That(preparationService.PrepareCallCount, Is.EqualTo(0));
        });
    }

    [Test]
    public void Verify_DiscoveredProjectsButNothingResolvedOrMissing_SkipsPreparationServiceAndProceedsNormally()
    {
        var document = CreateDocument();
        var discovery = ProjectDiscoveryResult.Empty with { DiscoveredProjects = new[] { FixtureProject() } };
        var runnerSetupService = new FakeRunnerSetupService { DocumentToReturn = document };
        var runner = new FakeContractRunner(CreateSession(document, discovery));
        runnerSetupService.RunnerToReturn = runner;
        var preparationService = new FakeBuildStatePreparationService();

        var applicationService = new ArchitectureBaselineApplicationService(
            runnerSetupService, new FakeContractHandlerRegistry(), new FakeContractExecutor(),
            new FakeBaselineGenerator(), new FakeBaselineLoadingService(), preparationService);

        BaselineVerifyOutcome outcome = applicationService.Verify(new BaselineVerifyRequest
        {
            PolicyPath = "unused-by-fakes.arch.yml",
            BaselinePath = "unused-by-fakes.baseline.yml",
            Mode = "all",
            NoRestore = true,
        });

        Assert.Multiple(() =>
        {
            Assert.That(outcome.Succeeded, Is.True);
            // No target assemblies and no missing assembly names: nothing for the preparation
            // service to verify, so it must not even be consulted.
            Assert.That(preparationService.PrepareCallCount, Is.EqualTo(0));
        });
    }

    [Test]
    public void Verify_NonTrivialResolutionWithoutPreparationService_ThrowsInvalidOperationException()
    {
        var document = CreateDocument();
        var discovery = ProjectDiscoveryResult.Empty with { DiscoveredProjects = new[] { FixtureProject() } };
        var runnerSetupService = new FakeRunnerSetupService { DocumentToReturn = document };
        var runner = new FakeContractRunner(CreateSession(document, discovery, missingAssemblyNames: _value1));
        runnerSetupService.RunnerToReturn = runner;

        var applicationService = new ArchitectureBaselineApplicationService(
            runnerSetupService, new FakeContractHandlerRegistry(), new FakeContractExecutor(),
            new FakeBaselineGenerator(), new FakeBaselineLoadingService());

        InvalidOperationException? ex = Assert.Throws<InvalidOperationException>(() =>
            applicationService.Verify(new BaselineVerifyRequest
            {
                PolicyPath = "unused-by-fakes.arch.yml",
                BaselinePath = "unused-by-fakes.baseline.yml",
                Mode = "all",
                NoRestore = true,
            }));

        Assert.That(ex!.Message, Does.Contain("Build-state preparation is unavailable"));
    }

    [Test]
    public void Verify_BlockedPreflight_ReturnsPreflightBlockedOutcomeWithoutRunningContracts()
    {
        var document = CreateDocument();
        var discovery = ProjectDiscoveryResult.Empty with { DiscoveredProjects = new[] { FixtureProject() } };
        var runnerSetupService = new FakeRunnerSetupService { DocumentToReturn = document };
        var runner = new FakeContractRunner(CreateSession(document, discovery, missingAssemblyNames: _value2));
        runnerSetupService.RunnerToReturn = runner;
        var blockingDiagnostic = BlockingDiagnostic();
        var preparationService = new FakeBuildStatePreparationService
        {
            ResultToReturn = new BuildStatePreflightResult(new[] { blockingDiagnostic }),
        };

        var applicationService = new ArchitectureBaselineApplicationService(
            runnerSetupService, new FakeContractHandlerRegistry(), new FakeContractExecutor(),
            new FakeBaselineGenerator(), new FakeBaselineLoadingService(), preparationService);

        BaselineVerifyOutcome outcome = applicationService.Verify(new BaselineVerifyRequest
        {
            PolicyPath = "unused-by-fakes.arch.yml",
            BaselinePath = "unused-by-fakes.baseline.yml",
            Mode = "all",
            NoRestore = true,
        });

        Assert.Multiple(() =>
        {
            Assert.That(outcome.Succeeded, Is.False);
            Assert.That(outcome.InSync, Is.False);
            Assert.That(outcome.PreflightDiagnostics, Has.Count.EqualTo(1));
            Assert.That(outcome.PreflightDiagnostics.Single().State, Is.EqualTo(BuildStatePreflightState.MissingArtifact));
            Assert.That(runner.StrictArgumentsReceived, Is.Empty);
            Assert.That(runnerSetupService.BuildRunnerForPostBuildCallCount, Is.EqualTo(0));
        });
    }

    [Test]
    public void Verify_OrdinaryPreparationModeWithNonBlockingPreflight_NeverRerunsPostBuildSetup()
    {
        var document = CreateDocument();
        var discovery = ProjectDiscoveryResult.Empty with { DiscoveredProjects = new[] { FixtureProject() } };
        var runnerSetupService = new FakeRunnerSetupService { DocumentToReturn = document };
        var runner = new FakeContractRunner(CreateSession(document, discovery, missingAssemblyNames: _value3));
        runnerSetupService.RunnerToReturn = runner;
        var preparationService = new FakeBuildStatePreparationService();

        var applicationService = new ArchitectureBaselineApplicationService(
            runnerSetupService, new FakeContractHandlerRegistry(), new FakeContractExecutor(),
            new FakeBaselineGenerator(), new FakeBaselineLoadingService(), preparationService);

        BaselineVerifyOutcome outcome = applicationService.Verify(new BaselineVerifyRequest
        {
            PolicyPath = "unused-by-fakes.arch.yml",
            BaselinePath = "unused-by-fakes.baseline.yml",
            Mode = "all",
            NoRestore = true,
        });

        Assert.Multiple(() =>
        {
            Assert.That(outcome.Succeeded, Is.True);
            Assert.That(preparationService.PrepareCallCount, Is.EqualTo(1));
            Assert.That(preparationService.RequestsReceived.Single().PreparationMode, Is.EqualTo(BuildPreparationMode.Ordinary));
            // Ordinary mode never triggers the isolated post-build rerun; only --ensure-built does.
            Assert.That(runnerSetupService.BuildRunnerForPostBuildCallCount, Is.EqualTo(0));
            Assert.That(runner.StrictArgumentsReceived, Is.Not.Empty);
        });
    }

    [Test]
    public void Verify_EnsureBuiltWithNonBlockingFirstPreflight_RerunsAgainstFreshPostBuildRunner()
    {
        var document = CreateDocument();
        var discovery = ProjectDiscoveryResult.Empty with { DiscoveredProjects = new[] { FixtureProject() } };

        var firstRunner = new FakeContractRunner(CreateSession(document, discovery, missingAssemblyNames: _value4));
        var secondRunner = new FakeContractRunner(CreateSession(document, discovery, missingAssemblyNames: _value5))
        {
            BaselineCandidates = new List<ArchitectureBaselineCandidate>
            {
                new("strict", "known-rule", "SrcFromPostBuild", "RefFromPostBuild"),
            },
        };

        var runnerSetupService = new FakeRunnerSetupService
        {
            RunnersToReturn = new Queue<IArchitectureContractRunner>(new IArchitectureContractRunner[] { firstRunner, secondRunner }),
        };
        var preparationService = new FakeBuildStatePreparationService();

        var applicationService = new ArchitectureBaselineApplicationService(
            runnerSetupService, new FakeContractHandlerRegistry(), new FakeContractExecutor(),
            new FakeBaselineGenerator(), new FakeBaselineLoadingService(), preparationService);

        BaselineVerifyOutcome outcome = applicationService.Verify(new BaselineVerifyRequest
        {
            PolicyPath = "unused-by-fakes.arch.yml",
            BaselinePath = "unused-by-fakes.baseline.yml",
            Mode = "all",
            PreparationMode = BuildPreparationMode.EnsureBuilt,
        });

        Assert.Multiple(() =>
        {
            Assert.That(outcome.Succeeded, Is.True);
            Assert.That(runnerSetupService.BuildRunnerForPostBuildCallCount, Is.EqualTo(1));
            Assert.That(preparationService.PrepareCallCount, Is.EqualTo(2));
            Assert.That(preparationService.RequestsReceived[0].PreparationMode, Is.EqualTo(BuildPreparationMode.EnsureBuilt));
            // The rerun against the post-build runner always re-verifies as Ordinary: the build
            // already ran once, so a second EnsureBuilt attempt would be redundant.
            Assert.That(preparationService.RequestsReceived[1].PreparationMode, Is.EqualTo(BuildPreparationMode.Ordinary));
            // Contract execution must run against the fresh post-build runner, not the one used
            // only to discover what needed building.
            Assert.That(firstRunner.StrictArgumentsReceived, Is.Empty);
            Assert.That(secondRunner.StrictArgumentsReceived, Is.Not.Empty);
            Assert.That(outcome.New.Single().SourceType, Is.EqualTo("SrcFromPostBuild"));
        });
    }

    [Test]
    public void Verify_EnsureBuiltWithSecondPreflightBlocked_ReturnsPreflightBlockedFromPostBuildRerun()
    {
        var document = CreateDocument();
        var discovery = ProjectDiscoveryResult.Empty with { DiscoveredProjects = new[] { FixtureProject() } };

        var firstRunner = new FakeContractRunner(CreateSession(document, discovery, missingAssemblyNames: _value6));
        var secondRunner = new FakeContractRunner(CreateSession(document, discovery, missingAssemblyNames: _value7));

        var runnerSetupService = new FakeRunnerSetupService
        {
            RunnersToReturn = new Queue<IArchitectureContractRunner>(new IArchitectureContractRunner[] { firstRunner, secondRunner }),
        };
        var postBuildBlockingDiagnostic = BlockingDiagnostic("PostBuild.Module");
        var preparationService = new FakeBuildStatePreparationService
        {
            ResultsToReturn = new Queue<BuildStatePreflightResult>(new[]
            {
                new BuildStatePreflightResult(Array.Empty<BuildStatePreflightDiagnostic>()),
                new BuildStatePreflightResult(new[] { postBuildBlockingDiagnostic }),
            }),
        };

        var applicationService = new ArchitectureBaselineApplicationService(
            runnerSetupService, new FakeContractHandlerRegistry(), new FakeContractExecutor(),
            new FakeBaselineGenerator(), new FakeBaselineLoadingService(), preparationService);

        BaselineVerifyOutcome outcome = applicationService.Verify(new BaselineVerifyRequest
        {
            PolicyPath = "unused-by-fakes.arch.yml",
            BaselinePath = "unused-by-fakes.baseline.yml",
            Mode = "all",
            PreparationMode = BuildPreparationMode.EnsureBuilt,
        });

        Assert.Multiple(() =>
        {
            Assert.That(outcome.Succeeded, Is.False);
            Assert.That(outcome.PreflightDiagnostics, Has.Count.EqualTo(1));
            Assert.That(outcome.PreflightDiagnostics.Single().ContractName, Is.EqualTo("PostBuild.Module"));
            Assert.That(firstRunner.StrictArgumentsReceived, Is.Empty);
            Assert.That(secondRunner.StrictArgumentsReceived, Is.Empty);
        });
    }
}
