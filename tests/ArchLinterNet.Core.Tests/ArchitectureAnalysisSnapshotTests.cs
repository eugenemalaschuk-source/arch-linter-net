using System.Reflection;
using ArchLinterNet.Core.BuildState;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Execution.Abstractions;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;
using ArchLinterNet.Core.Validation;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

// Issue #363: one immutable ArchitectureAnalysisSnapshot composes policy/project-graph/assembly
// setup once and serves strict/audit Evaluate calls from that one fact set. Follows the same
// fake-composition-seam pattern as ArchitectureValidationApplicationServiceFakeCompositionTests —
// fake the application service's collaborators rather than touching real files/assemblies.
[TestFixture]
public sealed class ArchitectureAnalysisSnapshotTests
{
    private sealed class CountingRunnerSetupService : IArchitectureRunnerSetupService
    {
        public int BuildRunnerCallCount { get; private set; }

        public ArchitectureContractDocument DocumentToReturn { get; set; } = new() { Version = 1, Name = "Fake" };

        public IArchitectureContractRunner RunnerToReturn { get; set; } = null!;

        public ArchitectureContractDocument LoadDocument(
            string policyPath, string? baselinePath = null, ValidationTiming? timing = null)
        {
            return DocumentToReturn;
        }

        public ArchitectureRunnerSetup BuildRunner(
            ArchitectureContractDocument document,
            string policyPath,
            string? conditionSetName = null,
            IReadOnlyList<string>? preprocessorSymbols = null,
            HashSet<string>? selectedContractIds = null,
            bool enableUnmatchedIgnoreTracking = true,
            ValidationTiming? timing = null,
            string? mode = null)
        {
            BuildRunnerCallCount++;
            return new ArchitectureRunnerSetup("/fake/repository/root", RunnerToReturn);
        }
    }

    private sealed class FakeContractRunner(ArchitectureAnalysisSession session) : IArchitectureContractRunner
    {
        public ArchitectureAnalysisSession Session { get; } = session;

        public IReadOnlyList<ArchitectureUnmatchedIgnoredViolation> UnmatchedIgnoredViolations { get; set; }
            = Array.Empty<ArchitectureUnmatchedIgnoredViolation>();

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
            throw new InvalidOperationException("Not expected to be called directly by the application service.");
        }
    }

    private sealed class CountingContractExecutor : IArchitectureContractExecutor
    {
        public Dictionary<string, int> CallCountByMode { get; } = new(StringComparer.Ordinal);

        public ArchitectureContractExecutionResult Execute(
            ArchitectureAnalysisSession session,
            string mode,
            IArchitectureContractHandlerRegistry handlerRegistry,
            bool includeAsmdefContracts = true,
            ValidationTiming? timing = null)
        {
            CallCountByMode[mode] = CallCountByMode.GetValueOrDefault(mode) + 1;
            return new ArchitectureContractExecutionResult(
                Array.Empty<ArchitectureViolation>(),
                Array.Empty<string>(),
                Array.Empty<ArchitectureViolation>(),
                Array.Empty<ArchitectureCoverageSummary>());
        }
    }

    private sealed class FakeBuildStatePreparationService : IBuildStatePreparationService
    {
        public BuildStatePreflightResult ResultToReturn { get; set; } =
            new(Array.Empty<BuildStatePreflightDiagnostic>());

        public BuildStatePreflightResult Prepare(BuildStatePreflightRequest request) => ResultToReturn;
    }

    private static ArchitectureAnalysisSession CreateEmptySession(ArchitectureContractDocument document)
    {
        var context = new ArchitectureAnalysisContext(
            "/fake/repository/root",
            Array.Empty<Assembly>(),
            Array.Empty<string>(),
            Array.Empty<string>());

        return new ArchitectureAnalysisSession(
            context, document, selectedContractIds: null, enableUnmatchedIgnoreTracking: true,
            preprocessorSymbols: null);
    }

    private static ArchitectureContractDocument CreateDocument()
    {
        return new ArchitectureContractDocument
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
    }

    private sealed record Fixture(
        ArchitectureValidationApplicationService ApplicationService,
        CountingRunnerSetupService RunnerSetupService,
        CountingContractExecutor ContractExecutor,
        FakeBuildStatePreparationService PreparationService);

    private static Fixture CreateFixture()
    {
        ArchitectureContractDocument document = CreateDocument();
        var runnerSetupService = new CountingRunnerSetupService { DocumentToReturn = document };
        var runner = new FakeContractRunner(CreateEmptySession(document));
        runnerSetupService.RunnerToReturn = runner;
        var handlerRegistry = new FakeContractHandlerRegistry();
        var contractExecutor = new CountingContractExecutor();
        var preparationService = new FakeBuildStatePreparationService();

        var applicationService = new ArchitectureValidationApplicationService(
            runnerSetupService, handlerRegistry, contractExecutor, preparationService);

        return new Fixture(applicationService, runnerSetupService, contractExecutor, preparationService);
    }

    private static AnalysisSnapshotRequest CreateSnapshotRequest()
    {
        return new AnalysisSnapshotRequest { PolicyPath = "unused-by-fakes.arch.yml" };
    }

    [Test]
    public void CreateSnapshot_EvaluatedForStrictAndAudit_ComposesSetupOnceAndExecutesEachModeOnce()
    {
        Fixture fixture = CreateFixture();

        using ArchitectureAnalysisSnapshot snapshot = fixture.ApplicationService.CreateSnapshot(CreateSnapshotRequest());
        snapshot.Evaluate("strict");
        snapshot.Evaluate("audit");

        Assert.Multiple(() =>
        {
            Assert.That(fixture.RunnerSetupService.BuildRunnerCallCount, Is.EqualTo(1));
            Assert.That(fixture.ContractExecutor.CallCountByMode.GetValueOrDefault("strict"), Is.EqualTo(1));
            Assert.That(fixture.ContractExecutor.CallCountByMode.GetValueOrDefault("audit"), Is.EqualTo(1));
        });
    }

    [Test]
    public void Evaluate_CalledTwiceForSameMode_MemoizesAndDoesNotReexecuteContracts()
    {
        Fixture fixture = CreateFixture();

        using ArchitectureAnalysisSnapshot snapshot = fixture.ApplicationService.CreateSnapshot(CreateSnapshotRequest());
        ValidationOutcome first = snapshot.Evaluate("strict");
        ValidationOutcome second = snapshot.Evaluate("strict");

        Assert.Multiple(() =>
        {
            Assert.That(fixture.ContractExecutor.CallCountByMode.GetValueOrDefault("strict"), Is.EqualTo(1));
            Assert.That(second, Is.SameAs(first));
        });
    }

    [Test]
    public void Evaluate_AfterDispose_ThrowsObjectDisposedException()
    {
        Fixture fixture = CreateFixture();

        ArchitectureAnalysisSnapshot snapshot = fixture.ApplicationService.CreateSnapshot(CreateSnapshotRequest());
        snapshot.Dispose();

        Assert.Throws<ObjectDisposedException>(() => snapshot.Evaluate("strict"));
    }

    [Test]
    public void Evaluate_BlockedPreflight_ShortCircuitsEveryModeWithoutExecutingContracts()
    {
        Fixture fixture = CreateFixture();
        var blockingDiagnostic = new BuildStatePreflightDiagnostic(
            "build-state-preflight", "Fixture.csproj", BuildStatePreflightState.MissingArtifact,
            new BuildStatePreflightEvidence("Fixture.csproj", "Fixture"));
        fixture.PreparationService.ResultToReturn = new BuildStatePreflightResult(new[] { blockingDiagnostic });

        // Preflight only runs when project discovery produced a project graph — reuse the
        // discovered-project session shape from ArchitectureValidationApplicationServiceFakeCompositionTests.
        var discovery = new Discovery.ProjectDiscoveryResult(
            new[] { "Fixture" }, Array.Empty<string>(), Array.Empty<string>(),
            Array.Empty<Discovery.ArchitectureProjectDiscoveryDiagnostic>())
        {
            DiscoveredProjects = new[]
            {
                new Discovery.ArchitectureDiscoveredProject("Fixture.csproj", "Fixture", new[] { "net10.0" })
            }
        };
        var context = new ArchitectureAnalysisContext(
            "/fake/repository/root",
            Array.Empty<Assembly>(),
            new[] { "Fixture" },
            Array.Empty<string>(),
            projectDiscovery: discovery);
        ArchitectureContractDocument document = fixture.RunnerSetupService.DocumentToReturn;
        var session = new ArchitectureAnalysisSession(
            context, document, selectedContractIds: null, enableUnmatchedIgnoreTracking: true,
            preprocessorSymbols: null);
        fixture.RunnerSetupService.RunnerToReturn = new FakeContractRunner(session);

        using ArchitectureAnalysisSnapshot snapshot = fixture.ApplicationService.CreateSnapshot(CreateSnapshotRequest());

        ValidationOutcome strict = snapshot.Evaluate("strict");
        ValidationOutcome audit = snapshot.Evaluate("audit");

        Assert.Multiple(() =>
        {
            Assert.That(snapshot.Failed, Is.True);
            Assert.That(strict.PreflightBlocked, Is.True);
            Assert.That(audit.PreflightBlocked, Is.True);
            Assert.That(fixture.ContractExecutor.CallCountByMode, Is.Empty);
        });
    }

    [Test]
    public void Counters_ReflectOneCompositionAndEachEvaluatedMode()
    {
        Fixture fixture = CreateFixture();

        using ArchitectureAnalysisSnapshot snapshot = fixture.ApplicationService.CreateSnapshot(CreateSnapshotRequest());
        Assert.That(snapshot.Counters.ModesEvaluated, Is.EqualTo(0));

        snapshot.Evaluate("strict");
        snapshot.Evaluate("audit");

        Assert.Multiple(() =>
        {
            Assert.That(snapshot.Counters.PolicyCompositions, Is.EqualTo(1));
            Assert.That(snapshot.Counters.ProjectGraphEvaluations, Is.EqualTo(1));
            Assert.That(snapshot.Counters.AssemblyLoads, Is.EqualTo(0));
            Assert.That(snapshot.Counters.ModesEvaluated, Is.EqualTo(2));
        });
    }

    [Test]
    public void Validate_SingleMode_ProducesSameOutcomeAsEvaluatingSnapshotDirectly()
    {
        Fixture fixture = CreateFixture();

        ValidationOutcome viaValidate = fixture.ApplicationService.Validate(
            new ValidationRequest { PolicyPath = "unused-by-fakes.arch.yml", Mode = "strict" });

        using ArchitectureAnalysisSnapshot snapshot = fixture.ApplicationService.CreateSnapshot(CreateSnapshotRequest());
        ValidationOutcome viaSnapshot = snapshot.Evaluate("strict");

        Assert.Multiple(() =>
        {
            Assert.That(viaValidate.Passed, Is.EqualTo(viaSnapshot.Passed));
            Assert.That(viaValidate.Violations, Is.EqualTo(viaSnapshot.Violations));
            Assert.That(viaValidate.Cycles, Is.EqualTo(viaSnapshot.Cycles));
        });
    }
}
