using System.Reflection;
using ArchLinterNet.Core.BuildState;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Families;
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

        public int LoadDocumentCallCount { get; private set; }

        public ArchitectureContractDocument DocumentToReturn { get; set; } = new() { Version = 1, Name = "Fake" };

        public IArchitectureContractRunner RunnerToReturn { get; set; } = null!;

        public ArchitectureContractDocument LoadDocument(
            string policyPath, string? baselinePath = null, ValidationTiming? timing = null)
        {
            LoadDocumentCallCount++;
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

        public ArchitectureRunnerSetup BuildRunnerForPostBuild(
            ArchitectureContractDocument document, string policyPath, string? conditionSetName = null,
            IReadOnlyList<string>? preprocessorSymbols = null, HashSet<string>? selectedContractIds = null,
            bool enableUnmatchedIgnoreTracking = true, ValidationTiming? timing = null, string? mode = null)
        {
            return BuildRunner(document, policyPath, conditionSetName, preprocessorSymbols, selectedContractIds,
                enableUnmatchedIgnoreTracking, timing, mode);
        }
    }

    private sealed class FakeContractRunner(ArchitectureAnalysisSession session) : IArchitectureContractRunner
    {
        public ArchitectureAnalysisSession Session { get; } = session;

        // Delegates to the real session's live list — matching how the real
        // ArchitectureContractRunner.UnmatchedIgnoredViolations delegates to _session
        // .UnmatchedIgnoredViolations. A disconnected settable property here would let tests that
        // exercise the session's real unmatched-ignore tracking (see the pollution regression
        // tests below) silently observe stale/empty data regardless of what the session actually
        // accumulated.
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
            throw new InvalidOperationException("Not expected to be called directly by the application service.");
        }
    }

    private sealed class CountingContractExecutor : IArchitectureContractExecutor
    {
        private int _activeExecutions;

        public Dictionary<string, int> CallCountByMode { get; } = new(StringComparer.Ordinal);

        public int MaxConcurrentExecutions { get; private set; }

        public bool DelayExecution { get; set; }

        // When set for a mode, Execute actually runs this real ArchitectureDependencyContract
        // against the session (via ArchitectureAnalysisSession.CheckContract) instead of
        // no-op'ing — needed so tests can observe the session's real mutable unmatched-ignore-list
        // behavior across multiple Evaluate calls, not a fake's stubbed return value.
        public Dictionary<string, ArchitectureDependencyContract> ContractByMode { get; } =
            new(StringComparer.Ordinal);

        public ArchitectureContractExecutionResult Execute(
            ArchitectureAnalysisSession session,
            string mode,
            IArchitectureContractHandlerRegistry handlerRegistry,
            bool includeAsmdefContracts = true,
            ValidationTiming? timing = null)
        {
            int concurrent = Interlocked.Increment(ref _activeExecutions);
            MaxConcurrentExecutions = Math.Max(MaxConcurrentExecutions, concurrent);
            try
            {
                if (DelayExecution)
                {
                    Thread.Sleep(50);
                }

                CallCountByMode[mode] = CallCountByMode.GetValueOrDefault(mode) + 1;

                List<ArchitectureViolation> violations = ContractByMode.TryGetValue(mode, out ArchitectureDependencyContract? contract)
                    ? session.CheckContract(contract)
                    : new List<ArchitectureViolation>();

                return new ArchitectureContractExecutionResult(
                    violations,
                    Array.Empty<string>(),
                    Array.Empty<ArchitectureViolation>(),
                    Array.Empty<ArchitectureCoverageSummary>());
            }
            finally
            {
                Interlocked.Decrement(ref _activeExecutions);
            }
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
    public async Task Evaluate_ConcurrentModes_SerializesSessionAccess()
    {
        Fixture fixture = CreateFixture();
        fixture.ContractExecutor.DelayExecution = true;

        using ArchitectureAnalysisSnapshot snapshot = fixture.ApplicationService.CreateSnapshot(CreateSnapshotRequest());

        await Task.WhenAll(
            Task.Run(() => snapshot.Evaluate("strict")),
            Task.Run(() => snapshot.Evaluate("audit")));

        Assert.Multiple(() =>
        {
            Assert.That(fixture.ContractExecutor.MaxConcurrentExecutions, Is.EqualTo(1));
            Assert.That(fixture.ContractExecutor.CallCountByMode, Has.Count.EqualTo(2));
            Assert.That(snapshot.Counters.ModesEvaluated, Is.EqualTo(2));
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
    public void PublicApi_DoesNotExposeMutableRunner()
    {
        Assert.That(typeof(ArchitectureAnalysisSnapshot).GetProperty("Runner"), Is.Null);
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
    public void Counters_AssemblyAlreadyPresentInRunnerContext_DoesNotCountAsSnapshotLoad()
    {
        Fixture fixture = CreateFixture();
        ArchitectureAnalysisContext context = new(
            "/fake/repository/root", new[] { typeof(ArchitectureAnalysisSnapshotTests).Assembly },
            Array.Empty<string>(), Array.Empty<string>());
        fixture.RunnerSetupService.RunnerToReturn = new FakeContractRunner(new ArchitectureAnalysisSession(
            context, fixture.RunnerSetupService.DocumentToReturn, selectedContractIds: null,
            enableUnmatchedIgnoreTracking: true, preprocessorSymbols: null));

        using ArchitectureAnalysisSnapshot snapshot = fixture.ApplicationService.CreateSnapshot(CreateSnapshotRequest());

        Assert.That(snapshot.Counters.AssemblyLoads, Is.EqualTo(0));
    }

    // Regression test for a PR #390 review defect: --ensure-built triggers a second BuildRunner
    // pass to pick up freshly built assemblies (see CreateSnapshotCore), but the snapshot's
    // counters unconditionally reported PolicyCompositions=1/ProjectGraphEvaluations=1 regardless
    // of whether that second pass actually happened. This asserts both halves of the fix: policy
    // composition (LoadDocument) genuinely happens only once even when the reload triggers, and
    // ProjectGraphEvaluations reports the real pass count (2) rather than a hardcoded 1.
    [Test]
    public void CreateSnapshot_EnsureBuiltTriggersReload_ComposesPolicyOnceButCountsTwoProjectGraphEvaluations()
    {
        ArchitectureContractDocument document = CreateDocument();
        var runnerSetupService = new CountingRunnerSetupService { DocumentToReturn = document };

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
            "/fake/repository/root", Array.Empty<Assembly>(), new[] { "Fixture" }, Array.Empty<string>(),
            projectDiscovery: discovery);
        var session = new ArchitectureAnalysisSession(
            context, document, selectedContractIds: null, enableUnmatchedIgnoreTracking: true,
            preprocessorSymbols: null);
        runnerSetupService.RunnerToReturn = new FakeContractRunner(session);

        var handlerRegistry = new FakeContractHandlerRegistry();
        var contractExecutor = new CountingContractExecutor();
        // Non-blocking: simulates ensure-built succeeding, which is what triggers the reload.
        var preparationService = new FakeBuildStatePreparationService
        {
            ResultToReturn = new BuildStatePreflightResult(Array.Empty<BuildStatePreflightDiagnostic>())
        };

        var applicationService = new ArchitectureValidationApplicationService(
            runnerSetupService, handlerRegistry, contractExecutor, preparationService);

        AnalysisSnapshotRequest request = new()
        {
            PolicyPath = "unused-by-fakes.arch.yml",
            PreparationMode = BuildPreparationMode.EnsureBuilt,
        };
        using ArchitectureAnalysisSnapshot snapshot = applicationService.CreateSnapshot(request);

        Assert.Multiple(() =>
        {
            Assert.That(runnerSetupService.LoadDocumentCallCount, Is.EqualTo(1),
                "policy document should be composed exactly once, reused across the ensure-built reload");
            Assert.That(runnerSetupService.BuildRunnerCallCount, Is.EqualTo(2),
                "ensure-built triggers a genuine second project-graph/assembly-resolution pass");
            Assert.That(snapshot.Counters.PolicyCompositions, Is.EqualTo(1));
            Assert.That(snapshot.Counters.ProjectGraphEvaluations, Is.EqualTo(2),
                "counters must reflect the real pass count, not a hardcoded 1");
        });
    }

    // Regression test for a PR #390 review defect: a --contract-id filter for a snapshot meant to
    // serve any/all requested modes was validated only against the union of strict and audit
    // contract IDs at CreateSnapshot time. A contract ID valid in one mode but not another was
    // therefore silently accepted for the snapshot, and the mode that doesn't recognize it simply
    // never matched it during execution instead of failing — unlike an independent single-mode
    // Validate call for that mode, which throws "Unknown contract IDs". This asserts Evaluate(mode)
    // now throws the same error an independent run would, restoring semantic equivalence.
    [Test]
    public void Evaluate_ContractIdValidOnlyInOtherMode_ThrowsSameAsIndependentRunWould()
    {
        var document = new ArchitectureContractDocument
        {
            Version = 1,
            Name = "Fake",
            Layers = new Dictionary<string, ArchitectureLayer>
            {
                ["resolution"] = new() { Namespace = "ArchLinterNet.Core.Resolution" },
                ["execution"] = new() { Namespace = "ArchLinterNet.Core.Execution" },
            },
            Analysis = new ArchitectureAnalysisConfiguration
            {
                UnmatchedIgnoredViolations = "off",
                PolicyConsistency = "off",
                Coverage = "off",
            },
            Contracts = new ArchitectureContractGroups
            {
                Strict = new List<ArchitectureDependencyContract>
                {
                    new() { Id = "strict-only-id", Name = "strict-execution-not-resolution", Source = "execution", Forbidden = new List<string> { "resolution" } },
                },
            },
        };

        Fixture fixture = CreateFixture();
        fixture.RunnerSetupService.DocumentToReturn = document;

        AnalysisSnapshotRequest request = CreateSnapshotRequest() with { ContractIds = new[] { "strict-only-id" } };

        // CreateSnapshot itself must not reject it — the ID is known to at least one mode (strict).
        using ArchitectureAnalysisSnapshot snapshot = fixture.ApplicationService.CreateSnapshot(request);

        // Evaluating "strict" (the mode that actually declares this ID) must succeed.
        Assert.DoesNotThrow(() => snapshot.Evaluate("strict"));

        // Evaluating "audit" — which has no contract with this ID — must throw the same
        // "Unknown contract IDs" error an independent Validate(audit) call for this ID would.
        InvalidOperationException? ex = Assert.Throws<InvalidOperationException>(() => snapshot.Evaluate("audit"));
        Assert.That(ex!.Message, Does.Contain("Unknown contract IDs"));
    }

    // Regression test for a PR #390 review defect: ArchitectureAnalysisSession.
    // UnmatchedIgnoredViolations is one mutable list every contract check across every mode
    // appends to, never cleared between modes. Before the fix, evaluating a second mode returned
    // the *entire* accumulated list (including the first mode's diagnostics), not just what that
    // mode's own checks added. This exercises the real ArchitectureAnalysisSession (only the
    // application service's other collaborators are faked) so the session's real mutable-list
    // behavior is what's under test, not a fake.
    [Test]
    public void Evaluate_SecondModeAfterFirst_DoesNotIncludeFirstModesUnmatchedIgnoreDiagnostics()
    {
        Assembly coreAssembly = typeof(ArchitecturePolicyDocumentLoader).Assembly;
        var document = new ArchitectureContractDocument
        {
            Version = 1,
            Name = "Fake",
            Layers = new Dictionary<string, ArchitectureLayer>
            {
                ["resolution"] = new() { Namespace = "ArchLinterNet.Core.Resolution" },
                ["execution"] = new() { Namespace = "ArchLinterNet.Core.Execution" },
            },
            Analysis = new ArchitectureAnalysisConfiguration
            {
                UnmatchedIgnoredViolations = "warn",
                PolicyConsistency = "off",
                Coverage = "off",
            },
        };

        var strictContract = new ArchitectureDependencyContract
        {
            Name = "strict-execution-not-resolution",
            Source = "execution",
            Forbidden = new List<string> { "resolution" },
            IgnoredViolations = new List<ArchitectureIgnoredViolation>
            {
                new()
                {
                    SourceType = "ArchLinterNet.Core.Execution.NoSuchType",
                    ForbiddenReference = "ArchLinterNet.Core.Resolution.NoSuchType",
                    Reason = "never matches an actual violation — forces an unmatched-ignore diagnostic",
                },
            },
        };
        var auditContract = new ArchitectureDependencyContract
        {
            Name = "audit-execution-not-resolution",
            Source = "execution",
            Forbidden = new List<string> { "resolution" },
        };

        var context = new ArchitectureAnalysisContext(
            "/fake/repository/root", new[] { coreAssembly }, Array.Empty<string>(), Array.Empty<string>());
        var session = new ArchitectureAnalysisSession(
            context, document, selectedContractIds: null, enableUnmatchedIgnoreTracking: true,
            preprocessorSymbols: null);

        Fixture fixture = CreateFixture();
        fixture.RunnerSetupService.DocumentToReturn = document;
        fixture.RunnerSetupService.RunnerToReturn = new FakeContractRunner(session);
        fixture.ContractExecutor.ContractByMode["strict"] = strictContract;
        fixture.ContractExecutor.ContractByMode["audit"] = auditContract;

        AnalysisSnapshotRequest request = CreateSnapshotRequest() with { EnforceUnmatchedIgnoredViolationsPolicy = true };
        using ArchitectureAnalysisSnapshot snapshot = fixture.ApplicationService.CreateSnapshot(request);

        ValidationOutcome strict = snapshot.Evaluate("strict");
        ValidationOutcome audit = snapshot.Evaluate("audit");

        Assert.Multiple(() =>
        {
            Assert.That(strict.UnmatchedIgnoredViolations, Has.Count.EqualTo(1),
                "strict's own unmatched ignore should be reported");
            Assert.That(audit.UnmatchedIgnoredViolations, Is.Empty,
                "audit added no unmatched ignores of its own and must not inherit strict's");
        });
    }

    // Same defect, opposite evaluation order — the fix must not depend on which mode runs first.
    [Test]
    public void Evaluate_FirstModeAfterSecond_DoesNotIncludeSecondModesUnmatchedIgnoreDiagnostics()
    {
        Assembly coreAssembly = typeof(ArchitecturePolicyDocumentLoader).Assembly;
        var document = new ArchitectureContractDocument
        {
            Version = 1,
            Name = "Fake",
            Layers = new Dictionary<string, ArchitectureLayer>
            {
                ["resolution"] = new() { Namespace = "ArchLinterNet.Core.Resolution" },
                ["execution"] = new() { Namespace = "ArchLinterNet.Core.Execution" },
            },
            Analysis = new ArchitectureAnalysisConfiguration
            {
                UnmatchedIgnoredViolations = "warn",
                PolicyConsistency = "off",
                Coverage = "off",
            },
        };

        var strictContract = new ArchitectureDependencyContract
        {
            Name = "strict-execution-not-resolution",
            Source = "execution",
            Forbidden = new List<string> { "resolution" },
        };
        var auditContract = new ArchitectureDependencyContract
        {
            Name = "audit-execution-not-resolution",
            Source = "execution",
            Forbidden = new List<string> { "resolution" },
            IgnoredViolations = new List<ArchitectureIgnoredViolation>
            {
                new()
                {
                    SourceType = "ArchLinterNet.Core.Execution.NoSuchType",
                    ForbiddenReference = "ArchLinterNet.Core.Resolution.NoSuchType",
                    Reason = "never matches an actual violation — forces an unmatched-ignore diagnostic",
                },
            },
        };

        var context = new ArchitectureAnalysisContext(
            "/fake/repository/root", new[] { coreAssembly }, Array.Empty<string>(), Array.Empty<string>());
        var session = new ArchitectureAnalysisSession(
            context, document, selectedContractIds: null, enableUnmatchedIgnoreTracking: true,
            preprocessorSymbols: null);

        Fixture fixture = CreateFixture();
        fixture.RunnerSetupService.DocumentToReturn = document;
        fixture.RunnerSetupService.RunnerToReturn = new FakeContractRunner(session);
        fixture.ContractExecutor.ContractByMode["strict"] = strictContract;
        fixture.ContractExecutor.ContractByMode["audit"] = auditContract;

        AnalysisSnapshotRequest request = CreateSnapshotRequest() with { EnforceUnmatchedIgnoredViolationsPolicy = true };
        using ArchitectureAnalysisSnapshot snapshot = fixture.ApplicationService.CreateSnapshot(request);

        ValidationOutcome audit = snapshot.Evaluate("audit");
        ValidationOutcome strict = snapshot.Evaluate("strict");

        Assert.Multiple(() =>
        {
            Assert.That(audit.UnmatchedIgnoredViolations, Has.Count.EqualTo(1),
                "audit's own unmatched ignore should be reported");
            Assert.That(strict.UnmatchedIgnoredViolations, Is.Empty,
                "strict added no unmatched ignores of its own and must not inherit audit's");
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
