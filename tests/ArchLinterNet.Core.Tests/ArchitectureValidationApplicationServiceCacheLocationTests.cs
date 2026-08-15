using ArchLinterNet.Core.BuildState;
using ArchLinterNet.Core.Caching;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Discovery;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Execution.Abstractions;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;
using ArchLinterNet.Core.Validation;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

// Follows the same fake-composition-seam pattern as
// ArchitectureValidationApplicationServiceFakeCompositionTests, scoped to the new
// request.CacheLocation branch of BuildSnapshot: when a cache location is configured, setup goes
// through metadata-only PrepareRunner instead of the eager BuildRunner, RunBuildStatePreflight gets
// a new preparation-based overload, --ensure-built re-prepares from post-build metadata, and the
// snapshot's lazy materializeSetup factory picks MaterializePreparedRunner or falls back to
// BuildRunnerFor depending on whether the prepared root selection is complete. These tests fake
// IArchitectureRunnerSetupService.PrepareRunner/MaterializePreparedRunner directly (rather than
// exercising the real service) so the routing logic in ArchitectureValidationApplicationService
// itself — not ArchitectureRunnerSetupService's own PE-reading internals, covered separately in
// ArchitectureRunnerSetupServicePreparationTests — is what's under test.
[TestFixture]
public sealed class ArchitectureValidationApplicationServiceCacheLocationTests
{
    private static readonly string[] _value = { "SomethingMissing" };
    private static readonly string[] _value1 = { "Fixture" };
    private static readonly string[] _value2 = { "net10.0" };
    private static readonly string[] _value3 = { "/fake/repository/root/bin/Fixture.dll" };
    private sealed class FakeRunnerSetupService : IArchitectureRunnerSetupService
    {
        public int BuildRunnerCallCount { get; private set; }

        public int PrepareRunnerCallCount { get; private set; }

        public int MaterializePreparedRunnerCallCount { get; private set; }

        public CancellationToken LastBuildRunnerCancellationToken { get; private set; }

        public string RepositoryRootToReturn { get; set; } = "/fake/repository/root";

        public ArchitectureContractDocument DocumentToReturn { get; set; } = new() { Version = 1, Name = "Fake" };

        public IArchitectureContractRunner RunnerToReturn { get; set; } = null!;

        // 1-based call index in, preparation out — lets a test return a different preparation for
        // --ensure-built's second (post-build) PrepareRunner call than its first.
        public Func<int, ArchitectureRunnerPreparation> PreparationProvider { get; set; } = null!;

        public ArchitectureContractDocument LoadDocument(
            string policyPath, string? baselinePath = null, ValidationTiming? timing = null) => DocumentToReturn;

        public ArchitectureRunnerSetup BuildRunner(
            ArchitectureContractDocument document,
            string policyPath,
            string? conditionSetName = null,
            IReadOnlyList<string>? preprocessorSymbols = null,
            HashSet<string>? selectedContractIds = null,
            bool enableUnmatchedIgnoreTracking = true,
            ValidationTiming? timing = null,
            string? mode = null,
            CancellationToken cancellationToken = default,
            int? maxParallelism = null)
        {
            BuildRunnerCallCount++;
            LastBuildRunnerCancellationToken = cancellationToken;
            return new ArchitectureRunnerSetup(RepositoryRootToReturn, RunnerToReturn);
        }

        public ArchitectureRunnerSetup BuildRunnerForPostBuild(
            ArchitectureContractDocument document, string policyPath, string? conditionSetName = null,
            IReadOnlyList<string>? preprocessorSymbols = null, HashSet<string>? selectedContractIds = null,
            bool enableUnmatchedIgnoreTracking = true, ValidationTiming? timing = null, string? mode = null,
            CancellationToken cancellationToken = default, int? maxParallelism = null) =>
            BuildRunner(document, policyPath, conditionSetName, preprocessorSymbols, selectedContractIds,
                enableUnmatchedIgnoreTracking, timing, mode, cancellationToken, maxParallelism);

        public ArchitectureRunnerPreparation PrepareRunner(
            ArchitectureContractDocument document,
            string policyPath,
            string? conditionSetName = null,
            IReadOnlyList<string>? preprocessorSymbols = null,
            HashSet<string>? selectedContractIds = null,
            string? mode = null,
            CancellationToken cancellationToken = default)
        {
            PrepareRunnerCallCount++;
            return PreparationProvider(PrepareRunnerCallCount);
        }

        public ArchitectureRunnerSetup MaterializePreparedRunner(
            ArchitectureContractDocument document,
            ArchitectureRunnerPreparation preparation,
            HashSet<string>? selectedContractIds = null,
            bool enableUnmatchedIgnoreTracking = true,
            ValidationTiming? timing = null,
            string? mode = null,
            CancellationToken cancellationToken = default,
            int? maxParallelism = null)
        {
            MaterializePreparedRunnerCallCount++;
            return new ArchitectureRunnerSetup(RepositoryRootToReturn, RunnerToReturn);
        }
    }

    private sealed class FakeContractRunner(ArchitectureAnalysisSession session) : IArchitectureContractRunner
    {
        public ArchitectureAnalysisSession Session { get; } = session;

        public IReadOnlyList<ArchitectureUnmatchedIgnoredViolation> UnmatchedIgnoredViolations
            => Session.UnmatchedIgnoredViolations;

        public IReadOnlyList<ArchitectureBaselineCandidate> BaselineCandidates { get; }
            = Array.Empty<ArchitectureBaselineCandidate>();

        public List<ArchitectureViolation> CheckConfiguration() => CheckConfiguration(strict: true);

        public List<ArchitectureViolation> CheckConfiguration(bool strict) => new();

        public List<PolicyConsistencyDiagnostic> CheckPolicyConsistency() => new();
    }

    private sealed class FakeContractHandlerRegistry : IArchitectureContractHandlerRegistry
    {
        public bool TryGetHandler(string family, out ArchitectureContractChecker? checker)
        {
            checker = null;
            return false;
        }

        public ArchitectureHandlerResult Execute(
            string family, ArchitectureAnalysisSession session, IArchitectureContract contract)
        {
            throw new InvalidOperationException("Not expected to be called by these tests.");
        }
    }

    private sealed class FakeContractExecutor : IArchitectureContractExecutor
    {
        public bool WasCalled { get; private set; }

        public ArchitectureContractExecutionResult ResultToReturn { get; set; } = new(
            Array.Empty<ArchitectureViolation>(), Array.Empty<string>(),
            Array.Empty<ArchitectureViolation>(), Array.Empty<ArchitectureCoverageSummary>());

        public ArchitectureContractExecutionResult Execute(
            ArchitectureAnalysisSession session, string mode, IArchitectureContractHandlerRegistry handlerRegistry,
            bool includeAsmdefContracts = true, ValidationTiming? timing = null)
        {
            WasCalled = true;
            return ResultToReturn;
        }
    }

    private sealed class FakeBuildStatePreparationService : IBuildStatePreparationService
    {
        public int PrepareCallCount { get; private set; }

        public BuildStatePreflightRequest? LastRequest { get; private set; }

        public BuildStatePreflightResult ResultToReturn { get; set; } =
            new(Array.Empty<BuildStatePreflightDiagnostic>());

        public BuildStatePreflightResult Prepare(BuildStatePreflightRequest request)
        {
            PrepareCallCount++;
            LastRequest = request;
            return ResultToReturn;
        }
    }

    private static ArchitectureContractDocument CreateDocument() => new()
    {
        Version = 1,
        Name = "Fake",
        Analysis = new ArchitectureAnalysisConfiguration
        {
            UnmatchedIgnoredViolations = "off",
            PolicyConsistency = "off",
            Coverage = "off",
        },
    };

    private static ArchitectureAnalysisSession CreateEmptySession(
        ArchitectureContractDocument document, CancellationToken cancellationToken = default)
    {
        var context = new ArchitectureAnalysisContext(
            "/fake/repository/root", Array.Empty<System.Reflection.Assembly>(),
            Array.Empty<string>(), Array.Empty<string>())
        {
            CancellationToken = cancellationToken,
        };
        return new ArchitectureAnalysisSession(
            context, document, selectedContractIds: null, enableUnmatchedIgnoreTracking: true, preprocessorSymbols: null);
    }

    private static ArchitectureRunnerPreparation CreatePreparation(
        string repositoryRoot = "/fake/repository/root",
        ProjectDiscoveryResult? discovery = null,
        IReadOnlyList<string>? selectedPaths = null,
        IReadOnlyList<string>? missingAssemblyNames = null,
        bool closureComplete = true) => new(
        repositoryRoot,
        PreprocessorSymbols: null,
        discovery ?? ProjectDiscoveryResult.Empty,
        ResolveAssemblyOutputs: true,
        selectedPaths ?? Array.Empty<string>(),
        new Dictionary<string, string>(),
        missingAssemblyNames ?? Array.Empty<string>(),
        closureComplete);

    [Test]
    public void CreateSnapshot_CacheLocationConfigured_PreparesInsteadOfEagerlyBuilding()
    {
        var document = CreateDocument();
        var runnerSetupService = new FakeRunnerSetupService
        {
            DocumentToReturn = document,
            PreparationProvider = _ => CreatePreparation(),
        };
        var preparationService = new FakeBuildStatePreparationService();
        var applicationService = new ArchitectureValidationApplicationService(
            runnerSetupService, new FakeContractHandlerRegistry(), new FakeContractExecutor(), preparationService);

        using ArchitectureAnalysisSnapshot snapshot = applicationService.CreateSnapshot(new AnalysisSnapshotRequest
        {
            PolicyPath = "unused-by-fakes.arch.yml",
            CacheLocation = new AnalysisCacheLocation("/fake/cache", AnalysisCacheMode.ExplicitPath),
        });

        Assert.Multiple(() =>
        {
            Assert.That(runnerSetupService.PrepareRunnerCallCount, Is.EqualTo(1));
            Assert.That(runnerSetupService.BuildRunnerCallCount, Is.Zero,
                "a cache-location request must plan metadata-only, never eagerly build a runner");
            Assert.That(runnerSetupService.MaterializePreparedRunnerCallCount, Is.Zero,
                "materialization is lazy and must not run before Evaluate is called");
            Assert.That(snapshot.RepositoryRoot, Is.EqualTo("/fake/repository/root"));
            Assert.That(snapshot.Counters.ProjectGraphEvaluations, Is.EqualTo(1));
            Assert.That(preparationService.PrepareCallCount, Is.Zero,
                "the default empty ProjectDiscovery preparation has no discovered projects, so the " +
                "preparation-based preflight overload must short-circuit without calling Prepare");
        });
    }

    [Test]
    public void Evaluate_CompletePreparedRootSelection_MaterializesViaMaterializePreparedRunner()
    {
        var document = CreateDocument();
        var runnerSetupService = new FakeRunnerSetupService
        {
            DocumentToReturn = document,
            PreparationProvider = _ => CreatePreparation(missingAssemblyNames: Array.Empty<string>()),
            RunnerToReturn = new FakeContractRunner(CreateEmptySession(document)),
        };
        var applicationService = new ArchitectureValidationApplicationService(
            runnerSetupService, new FakeContractHandlerRegistry(), new FakeContractExecutor(),
            new FakeBuildStatePreparationService());

        using ArchitectureAnalysisSnapshot snapshot = applicationService.CreateSnapshot(new AnalysisSnapshotRequest
        {
            PolicyPath = "unused-by-fakes.arch.yml",
            CacheLocation = new AnalysisCacheLocation("/fake/cache", AnalysisCacheMode.ExplicitPath),
        });

        snapshot.Evaluate("strict");

        Assert.Multiple(() =>
        {
            Assert.That(runnerSetupService.MaterializePreparedRunnerCallCount, Is.EqualTo(1));
            Assert.That(runnerSetupService.BuildRunnerCallCount, Is.Zero);
        });
    }

    [Test]
    public void Evaluate_IncompletePreparedRootSelection_FallsBackToBuildRunnerFor()
    {
        var document = CreateDocument();
        var runnerSetupService = new FakeRunnerSetupService
        {
            DocumentToReturn = document,
            PreparationProvider = _ => CreatePreparation(missingAssemblyNames: _value),
            RunnerToReturn = new FakeContractRunner(CreateEmptySession(document)),
        };
        var applicationService = new ArchitectureValidationApplicationService(
            runnerSetupService, new FakeContractHandlerRegistry(), new FakeContractExecutor(),
            new FakeBuildStatePreparationService());

        using ArchitectureAnalysisSnapshot snapshot = applicationService.CreateSnapshot(new AnalysisSnapshotRequest
        {
            PolicyPath = "unused-by-fakes.arch.yml",
            CacheLocation = new AnalysisCacheLocation("/fake/cache", AnalysisCacheMode.ExplicitPath),
        });

        snapshot.Evaluate("strict");

        Assert.Multiple(() =>
        {
            Assert.That(runnerSetupService.BuildRunnerCallCount, Is.EqualTo(1),
                "an incomplete prepared root selection must fall back to the eager BuildRunnerFor path");
            Assert.That(runnerSetupService.MaterializePreparedRunnerCallCount, Is.Zero);
        });
    }

    [Test]
    public void CreateSnapshot_EnsureBuiltWithNonBlockingPreflight_RePreparesFromPostBuildMetadata()
    {
        var document = CreateDocument();
        var runnerSetupService = new FakeRunnerSetupService
        {
            DocumentToReturn = document,
            PreparationProvider = _ => CreatePreparation(),
        };
        var preparationService = new FakeBuildStatePreparationService();
        var applicationService = new ArchitectureValidationApplicationService(
            runnerSetupService, new FakeContractHandlerRegistry(), new FakeContractExecutor(), preparationService);

        using ArchitectureAnalysisSnapshot snapshot = applicationService.CreateSnapshot(new AnalysisSnapshotRequest
        {
            PolicyPath = "unused-by-fakes.arch.yml",
            CacheLocation = new AnalysisCacheLocation("/fake/cache", AnalysisCacheMode.ExplicitPath),
            PreparationMode = BuildPreparationMode.EnsureBuilt,
        });

        Assert.Multiple(() =>
        {
            Assert.That(runnerSetupService.PrepareRunnerCallCount, Is.EqualTo(2),
                "--ensure-built must re-prepare from post-build artifacts rather than trusting the pre-build plan");
            Assert.That(snapshot.Counters.ProjectGraphEvaluations, Is.EqualTo(2));
        });
    }

    [Test]
    public void CreateSnapshot_CacheLocationWithDiscoveredProject_RunsPreparationBasedPreflightOverloadWithResolvedPaths()
    {
        var document = CreateDocument();
        var discovery = new ProjectDiscoveryResult(
            _value1, Array.Empty<string>(), Array.Empty<string>(),
            Array.Empty<ArchitectureProjectDiscoveryDiagnostic>())
        {
            DiscoveredProjects = new[] { new ArchitectureDiscoveredProject("Fixture.csproj", "Fixture", _value2) },
            ResolvedAssemblyPaths = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["Fixture"] = "/fake/repository/root/bin/Fixture.dll",
            },
        };
        ArchitectureRunnerPreparation preparation = CreatePreparation(
            discovery: discovery, selectedPaths: _value3);
        var runnerSetupService = new FakeRunnerSetupService
        {
            DocumentToReturn = document,
            PreparationProvider = _ => preparation,
        };
        var preparationService = new FakeBuildStatePreparationService();
        var applicationService = new ArchitectureValidationApplicationService(
            runnerSetupService, new FakeContractHandlerRegistry(), new FakeContractExecutor(), preparationService);

        using ArchitectureAnalysisSnapshot snapshot = applicationService.CreateSnapshot(new AnalysisSnapshotRequest
        {
            PolicyPath = "unused-by-fakes.arch.yml",
            CacheLocation = new AnalysisCacheLocation("/fake/cache", AnalysisCacheMode.ExplicitPath),
            RequestedConfiguration = "Release",
            RequestedTargetFramework = "net10.0",
        });

        Assert.Multiple(() =>
        {
            Assert.That(preparationService.PrepareCallCount, Is.EqualTo(1));
            Assert.That(preparationService.LastRequest, Is.Not.Null);
            Assert.That(
                preparationService.LastRequest!.Resolution.ResolvedAssemblyPaths,
                Does.ContainKey("Fixture").WithValue("/fake/repository/root/bin/Fixture.dll"));
            Assert.That(preparationService.LastRequest.Resolution.MissingAssemblyNames, Is.Empty);
            Assert.That(preparationService.LastRequest.RequestedConfiguration, Is.EqualTo("Release"));
            Assert.That(preparationService.LastRequest.RequestedTargetFramework, Is.EqualTo("net10.0"));
            Assert.That(snapshot.RepositoryRoot, Is.EqualTo("/fake/repository/root"));
        });
    }

    // Finding from the coverage task: the non-cache-location snapshot construction call started
    // forwarding cancellationToken: request.CancellationToken instead of leaving it at the
    // parameter's default. Prove it actually reaches the snapshot's own cancellation check
    // (ArchitectureAnalysisSnapshot.Evaluate's `_cancellationToken.ThrowIfCancellationRequested()`)
    // by giving the fake runner's session context an unrelated, never-cancelled token of its own —
    // if the snapshot constructor still silently defaulted its token, Evaluate would run to
    // completion instead of observing this test's cancellation at all.
    [Test]
    public void Evaluate_NoCacheLocation_CancelledAfterSnapshotConstruction_ThrowsViaSnapshotsOwnToken()
    {
        var document = CreateDocument();
        using CancellationTokenSource cts = new();
        var runnerSetupService = new FakeRunnerSetupService
        {
            DocumentToReturn = document,
            RunnerToReturn = new FakeContractRunner(CreateEmptySession(document, CancellationToken.None)),
        };
        var contractExecutor = new FakeContractExecutor();
        var applicationService = new ArchitectureValidationApplicationService(
            runnerSetupService, new FakeContractHandlerRegistry(), contractExecutor,
            new FakeBuildStatePreparationService());

        using ArchitectureAnalysisSnapshot snapshot = applicationService.CreateSnapshot(new AnalysisSnapshotRequest
        {
            PolicyPath = "unused-by-fakes.arch.yml",
            CancellationToken = cts.Token,
        });

        // Cancel only now — after construction succeeded — so this proves Evaluate observes
        // cancellation requested *after* the snapshot was built, via the token captured at
        // construction time, not merely a pre-flight check during BuildSnapshot itself.
        cts.Cancel();

        Assert.Throws<OperationCanceledException>(() => snapshot.Evaluate("strict"));
        Assert.That(contractExecutor.WasCalled, Is.False,
            "the snapshot's own cancellation check must short-circuit before contract execution");
    }
}
