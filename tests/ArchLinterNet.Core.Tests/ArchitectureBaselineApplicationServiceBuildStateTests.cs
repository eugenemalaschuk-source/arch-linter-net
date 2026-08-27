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
// post-build preflight and prepared-runner materialization.
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
    private static readonly string[] _fixtureAssemblyNames = { "Fixture" };
    private static readonly string[] _materializationCallOrder =
        { "PrepareRunner", "PrepareBuild", "VerifyPostBuild", "MaterializePreparedRunner" };
    private static readonly string[] _fallbackCallOrder = { "PrepareRunner", "BuildRunner" };

    private sealed class FakeBuildStatePreparationService : IBuildStatePreparationService
    {
        public int PrepareCallCount { get; private set; }

        public List<BuildStatePreflightRequest> RequestsReceived { get; } = new();

        public Queue<BuildStatePreflightResult>? ResultsToReturn { get; init; }

        public List<string>? CallOrder { get; set; }

        public BuildStatePreflightResult ResultToReturn { get; set; } =
            new(Array.Empty<BuildStatePreflightDiagnostic>());

        public BuildStatePreflightResult Prepare(BuildStatePreflightRequest request)
        {
            CallOrder?.Add(request.PreparationMode == BuildPreparationMode.EnsureBuilt
                ? "PrepareBuild"
                : "VerifyPostBuild");
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

    private static BuildStatePreflightDiagnostic CurrentDiagnostic(string assemblyName = "Fixture") =>
        new(assemblyName, null, BuildStatePreflightState.Current,
            new BuildStatePreflightEvidence(
                $"{assemblyName}.csproj", assemblyName,
                ExpectedOutputPath: Path.GetFullPath(Path.Combine(
                    "/fake/repository/root", "bin", $"{assemblyName}.dll"))));

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
    public void Verify_EnsureBuiltWithNonBlockingFirstPreflight_MaterializesPreparedPostBuildRunner()
    {
        var document = CreateDocument();
        document.Analysis.TargetAssemblies = ["Fixture"];
        string fixturePath = Path.GetFullPath(Path.Combine("/fake/repository/root", "bin", "Fixture.dll"));
        string unselectedPath = Path.GetFullPath(Path.Combine("/fake/repository/root", "bin", "Unselected.dll"));
        var discovery = ProjectDiscoveryResult.Empty with
        {
            DiscoveredProjects = new[] { FixtureProject() },
            ResolvedAssemblyPaths = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["Fixture"] = fixturePath,
                ["Unselected"] = unselectedPath,
            },
        };

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
            DocumentToReturn = document,
            RunnerToReturn = firstRunner,
            RunnersToReturn = new Queue<IArchitectureContractRunner>(new IArchitectureContractRunner[] { secondRunner }),
            PreparationToReturn = new ArchitectureRunnerPreparation(
                "/fake/repository/root",
                PreprocessorSymbols: null,
                ProjectDiscovery: discovery,
                ResolveAssemblyOutputs: true,
                SelectedAssemblyArtifactPaths: new[] { fixturePath },
                CapturedArtifactContentDigests: new Dictionary<string, string>(),
                MissingAssemblyNames: _value4,
                IsMetadataReferenceClosureComplete: false),
        };
        var callOrder = new List<string>();
        runnerSetupService.CallOrder = callOrder;
        var preparationService = new FakeBuildStatePreparationService
        {
            CallOrder = callOrder,
            ResultsToReturn = new Queue<BuildStatePreflightResult>(new[]
            {
                new BuildStatePreflightResult(new[] { CurrentDiagnostic() }),
                new BuildStatePreflightResult(Array.Empty<BuildStatePreflightDiagnostic>()),
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
            Assert.That(outcome.Succeeded, Is.True);
            Assert.That(runnerSetupService.BuildRunnerCallCount, Is.EqualTo(0));
            Assert.That(runnerSetupService.BuildRunnerForPostBuildCallCount, Is.EqualTo(0));
            Assert.That(runnerSetupService.PrepareRunnerCallCount, Is.EqualTo(1));
            Assert.That(runnerSetupService.MaterializePreparedRunnerCallCount, Is.EqualTo(1));
            Assert.That(preparationService.PrepareCallCount, Is.EqualTo(2));
            Assert.That(preparationService.RequestsReceived[0].PreparationMode, Is.EqualTo(BuildPreparationMode.EnsureBuilt));
            // The post-build verification always runs as Ordinary: the build already ran once, so
            // a second EnsureBuilt attempt would be redundant.
            Assert.That(preparationService.RequestsReceived[1].PreparationMode, Is.EqualTo(BuildPreparationMode.Ordinary));
            Assert.That(preparationService.RequestsReceived[1].Resolution.ResolvedAssemblyPaths["Fixture"],
                Is.EqualTo(fixturePath));
            Assert.That(preparationService.RequestsReceived[0].Resolution.ResolvedAssemblyPaths.Keys,
                Is.EquivalentTo(_fixtureAssemblyNames));
            Assert.That(preparationService.RequestsReceived[1].Resolution.ResolvedAssemblyPaths.Keys,
                Is.EquivalentTo(_fixtureAssemblyNames));
            // Contract execution must run against the materialized post-build runner, not anything
            // used only to discover what needed building.
            Assert.That(firstRunner.StrictArgumentsReceived, Is.Empty);
            Assert.That(secondRunner.StrictArgumentsReceived, Is.Not.Empty);
            Assert.That(outcome.New.Single().SourceType, Is.EqualTo("SrcFromPostBuild"));
            Assert.That(callOrder, Is.EqualTo(_materializationCallOrder));
        });
    }

    [Test]
    public void Verify_EnsureBuiltWithGraphDrivenStaleOutput_UsesDiscoveredRootForBothPreflights()
    {
        var document = CreateDocument();
        string fixturePath = Path.GetFullPath(Path.Combine("/fake/repository/root", "bin", "Fixture.dll"));
        var discovery = ProjectDiscoveryResult.Empty with
        {
            DiscoveredProjects = new[] { FixtureProject() },
            ResolvedAssemblyPaths = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["Fixture"] = fixturePath,
            },
        };
        var firstRunner = new FakeContractRunner(CreateSession(document, discovery));
        var secondRunner = new FakeContractRunner(CreateSession(document, discovery));
        var runnerSetupService = new FakeRunnerSetupService
        {
            DocumentToReturn = document,
            RunnerToReturn = firstRunner,
            RunnersToReturn = new Queue<IArchitectureContractRunner>(new IArchitectureContractRunner[] { secondRunner }),
            PreparationToReturn = new ArchitectureRunnerPreparation(
                "/fake/repository/root",
                PreprocessorSymbols: null,
                ProjectDiscovery: discovery,
                ResolveAssemblyOutputs: true,
                SelectedAssemblyArtifactPaths: Array.Empty<string>(),
                CapturedArtifactContentDigests: new Dictionary<string, string>(),
                MissingAssemblyNames: Array.Empty<string>(),
                IsMetadataReferenceClosureComplete: false)
            {
                GraphDrivenRootAssemblyNames = _fixtureAssemblyNames,
            },
        };
        var preparationService = new FakeBuildStatePreparationService
        {
            ResultsToReturn = new Queue<BuildStatePreflightResult>(new[]
            {
                new BuildStatePreflightResult(new[] { CurrentDiagnostic() }),
                new BuildStatePreflightResult(new[] { CurrentDiagnostic() }),
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
            Assert.That(outcome.Succeeded, Is.True);
            Assert.That(preparationService.PrepareCallCount, Is.EqualTo(2));
            Assert.That(preparationService.RequestsReceived[0].Resolution.ResolvedAssemblyPaths.Keys,
                Is.EquivalentTo(_fixtureAssemblyNames));
            Assert.That(preparationService.RequestsReceived[1].Resolution.ResolvedAssemblyPaths.Keys,
                Is.EquivalentTo(_fixtureAssemblyNames));
            Assert.That(document.Analysis.TargetAssemblies, Is.EquivalentTo(_fixtureAssemblyNames));
            Assert.That(runnerSetupService.MaterializePreparedRunnerCallCount, Is.EqualTo(1));
        });
    }

    [Test]
    public void Verify_EnsureBuiltWithSecondPreflightBlocked_ReturnsPreflightBlockedBeforeMaterialization()
    {
        var document = CreateDocument();
        var discovery = ProjectDiscoveryResult.Empty with { DiscoveredProjects = new[] { FixtureProject() } };

        var firstRunner = new FakeContractRunner(CreateSession(document, discovery, missingAssemblyNames: _value6));
        var secondRunner = new FakeContractRunner(CreateSession(document, discovery, missingAssemblyNames: _value7));

        var runnerSetupService = new FakeRunnerSetupService
        {
            RunnerToReturn = firstRunner,
            RunnersToReturn = new Queue<IArchitectureContractRunner>(new IArchitectureContractRunner[] { secondRunner }),
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
            Assert.That(runnerSetupService.PrepareRunnerCallCount, Is.EqualTo(1));
            Assert.That(runnerSetupService.MaterializePreparedRunnerCallCount, Is.EqualTo(0));
            Assert.That(runnerSetupService.BuildRunnerCallCount, Is.EqualTo(0));
        });
    }

    [Test]
    public void Verify_EnsureBuiltWithoutProjectGraph_FallsBackAfterMetadataPreflight()
    {
        var document = CreateDocument();
        var runner = new FakeContractRunner(CreateSession(document));
        var callOrder = new List<string>();
        var runnerSetupService = new FakeRunnerSetupService
        {
            DocumentToReturn = document,
            RunnerToReturn = runner,
            CallOrder = callOrder,
            PreparationToReturn = new ArchitectureRunnerPreparation(
                "/fake/repository/root",
                PreprocessorSymbols: null,
                ProjectDiscoveryResult.Empty,
                ResolveAssemblyOutputs: true,
                SelectedAssemblyArtifactPaths: Array.Empty<string>(),
                CapturedArtifactContentDigests: new Dictionary<string, string>(),
                MissingAssemblyNames: new[] { "Direct" },
                IsMetadataReferenceClosureComplete: false),
        };
        var preparationService = new FakeBuildStatePreparationService { CallOrder = callOrder };

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
            Assert.That(runnerSetupService.PrepareRunnerCallCount, Is.EqualTo(1));
            Assert.That(runnerSetupService.MaterializePreparedRunnerCallCount, Is.EqualTo(0));
            Assert.That(runnerSetupService.BuildRunnerCallCount, Is.EqualTo(1));
            Assert.That(preparationService.PrepareCallCount, Is.Zero);
            Assert.That(callOrder, Is.EqualTo(_fallbackCallOrder));
        });
    }
}
