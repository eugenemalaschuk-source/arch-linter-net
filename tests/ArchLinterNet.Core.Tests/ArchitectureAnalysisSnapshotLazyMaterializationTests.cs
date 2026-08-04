using ArchLinterNet.Core.BuildState;
using ArchLinterNet.Core.Caching;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Execution.Abstractions;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;
using ArchLinterNet.Core.Validation;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

// ArchitectureAnalysisSnapshotTests is already at the file-size lint ceiling, so lazy
// materialization ("setup: null, materializeSetup: ...") coverage lives here instead. These tests
// construct ArchitectureAnalysisSnapshot directly through its internal constructor (visible via
// InternalsVisibleTo) rather than through ArchitectureValidationApplicationService, so each test
// can isolate exactly one of: EnsureSetup's lazy-materialize/ObjectDisposedException behavior, the
// _prepared* field fallbacks used while setup is still null, the new
// !_preparedArtifactClosureComplete early cache-reject branch, and the real cache miss-then-hit
// round trip (ArchitectureAnalysisSnapshot.CacheWork.cs's CaptureWorkSnapshot/CreateWorkProvenance)
// including the promise that a genuine cache hit never materializes the runner at all.
[TestFixture]
public sealed class ArchitectureAnalysisSnapshotLazyMaterializationTests
{
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
        public ArchitectureContractExecutionResult ResultToReturn { get; set; } = new(
            Array.Empty<ArchitectureViolation>(), Array.Empty<string>(),
            Array.Empty<ArchitectureViolation>(), Array.Empty<ArchitectureCoverageSummary>());

        public ArchitectureContractExecutionResult Execute(
            ArchitectureAnalysisSession session, string mode, IArchitectureContractHandlerRegistry handlerRegistry,
            bool includeAsmdefContracts = true, ValidationTiming? timing = null) => ResultToReturn;
    }

    private static readonly BuildStatePreflightResult _nonBlockingPreflight =
        new(Array.Empty<BuildStatePreflightDiagnostic>());

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

    private static ArchitectureRunnerSetup CreateWorkingSetup(ArchitectureContractDocument document, string repositoryRoot)
    {
        var context = new ArchitectureAnalysisContext(
            repositoryRoot, Array.Empty<System.Reflection.Assembly>(), Array.Empty<string>(), Array.Empty<string>());
        var session = new ArchitectureAnalysisSession(
            context, document, selectedContractIds: null, enableUnmatchedIgnoreTracking: true, preprocessorSymbols: null);
        return new ArchitectureRunnerSetup(repositoryRoot, new FakeContractRunner(session));
    }

    private static ArchitectureAnalysisSnapshot BuildSnapshot(
        ArchitectureContractDocument document,
        string preparedRepositoryRoot,
        Func<ArchitectureRunnerSetup>? materializeSetup = null,
        AnalysisSnapshotCacheContext? cacheContext = null,
        IReadOnlyList<string>? preparedArtifactPaths = null,
        IReadOnlyDictionary<string, string>? preparedArtifactContentDigests = null,
        IReadOnlyList<string>? preparedProjectPaths = null,
        bool preparedArtifactClosureComplete = true,
        BuildStatePreflightResult? preflight = null)
    {
        return new ArchitectureAnalysisSnapshot(
            document,
            setup: null,
            preflight ?? _nonBlockingPreflight,
            unmatchedConfig: "off",
            policyConsistencyConfig: "off",
            coverageConfig: "off",
            enforceUnmatchedIgnoredViolationsPolicy: false,
            includeAsmdefContracts: true,
            new FakeContractExecutor(),
            new FakeContractHandlerRegistry(),
            policyCompositions: 1,
            projectGraphEvaluations: 1,
            assemblyLoads: 0,
            cacheContext: cacheContext,
            preparedRepositoryRoot: preparedRepositoryRoot,
            preparedArtifactPaths: preparedArtifactPaths,
            preparedArtifactContentDigests: preparedArtifactContentDigests,
            preparedProjectPaths: preparedProjectPaths,
            preparedArtifactClosureComplete: preparedArtifactClosureComplete,
            materializeSetup: materializeSetup);
    }

    [Test]
    public void Evaluate_NoMaterializeSetupAndNoSetup_WrapsObjectDisposedExceptionFromEnsureSetup()
    {
        // A snapshot can only be constructed this way through a production bug (both setup and its
        // lazy factory absent) — EnsureSetup's own defensive throw is what a real ObjectDisposedException
        // would look like if a caller somehow evaluated a snapshot after disposal raced construction.
        using ArchitectureAnalysisSnapshot snapshot =
            BuildSnapshot(CreateDocument(), "/fake/repo", materializeSetup: null);

        ArchitectureAnalysisEvaluationException? exception = Assert.Throws<ArchitectureAnalysisEvaluationException>(
            () => snapshot.Evaluate("strict"));

        Assert.That(exception!.InnerException, Is.InstanceOf<ObjectDisposedException>());
    }

    [Test]
    public void GetProfileInputPaths_SetupStillNull_FallsBackToPreparedArtifactAndProjectPaths()
    {
        // Proves the _prepared* fallbacks (GetSelectedAssemblyArtifactPaths/GetDiscoveredProjectPaths)
        // work from prepared metadata alone, and that reading profile input paths never triggers
        // materialization — the materializeSetup factory here throws if invoked at all.
        string repositoryRoot = "/fake/repo";
        string artifactPath = Path.GetFullPath(Path.Combine(repositoryRoot, "bin", "Root.dll"));
        string projectPath = Path.GetFullPath(Path.Combine(repositoryRoot, "src", "Root.csproj"));

        using ArchitectureAnalysisSnapshot snapshot = BuildSnapshot(
            CreateDocument(),
            repositoryRoot,
            materializeSetup: () => throw new InvalidOperationException("must not materialize for a profile-path read"),
            preparedArtifactPaths: new[] { artifactPath },
            preparedProjectPaths: new[] { projectPath });

        IReadOnlyList<string> profileInputPaths = snapshot.GetProfileInputPaths();

        Assert.Multiple(() =>
        {
            Assert.That(profileInputPaths, Has.Member(artifactPath));
            Assert.That(profileInputPaths, Has.Member(BuildReceiptStore.ReceiptPathFor(artifactPath)));
            Assert.That(profileInputPaths, Has.Member(projectPath));
        });
    }

    [Test]
    public void Evaluate_PreparedArtifactClosureIncomplete_RejectsBeforeAnyRealCacheIO()
    {
        string cacheRoot = Path.Combine(
            Path.GetTempPath(), $"arch-linter-snapshot-lazy-incomplete-{Guid.NewGuid():N}");
        var cacheContext = new AnalysisSnapshotCacheContext(
            new AnalysisCacheLocation(cacheRoot, AnalysisCacheMode.ExplicitPath),
            ConditionSetName: null, ContractIds: Array.Empty<string>(),
            Configuration: null, TargetFramework: null, Platform: null, RuntimeIdentifier: null);
        var document = CreateDocument();
        string repositoryRoot = "/fake/repo";

        try
        {
            using ArchitectureAnalysisSnapshot snapshot = BuildSnapshot(
                document, repositoryRoot,
                materializeSetup: () => CreateWorkingSetup(document, repositoryRoot),
                cacheContext: cacheContext,
                preparedArtifactClosureComplete: false);

            ValidationOutcome outcome = snapshot.Evaluate("strict");

            Assert.Multiple(() =>
            {
                Assert.That(outcome.Passed, Is.True);
                Assert.That(snapshot.CacheStats.Rejects, Is.EqualTo(1));
                Assert.That(snapshot.CacheStats.RejectReasonCounts["IneligibleBuildInput"], Is.EqualTo(1));
                Assert.That(Directory.Exists(cacheRoot), Is.False,
                    "an incomplete prepared closure must fail closed before touching the cache store at all");
            });
        }
        finally
        {
            if (Directory.Exists(cacheRoot))
            {
                Directory.Delete(cacheRoot, recursive: true);
            }
        }
    }

    private static EvaluatedBuildInputManifestV1 AlwaysEligible(
        string projectPath, string repositoryRoot, string? configuration, string? targetFramework,
        string? platform, string? runtimeIdentifier, CancellationToken cancellationToken) =>
        new("fixed-digest", CacheEligibility.VerifiedCacheEligible, Array.Empty<string>(), Array.Empty<string>());

    [Test]
    public void Evaluate_CacheMissThenSecondSnapshotSameKey_PopulatesThenHitsWithoutEverMaterializingOnTheHit()
    {
        // One scenario, two snapshots against the same cache directory/key: the first Evaluate is a
        // genuine miss (nothing published yet) that must materialize the runner, capture a
        // before/after WorkSnapshot via CaptureWorkSnapshot/CreateWorkProvenance, and publish a real
        // entry to disk. A second, independently constructed snapshot built from the same prepared
        // inputs must then find that entry as a genuine Hit and reconstruct its outcome WITHOUT ever
        // invoking materializeSetup — proving the lazy-materialization promise this feature exists
        // for: a cache hit skips runner construction entirely.
        AnalysisCachePopulation.TestManifestCollectorOverride = AlwaysEligible;
        string cacheRoot = Path.Combine(
            Path.GetTempPath(), $"arch-linter-snapshot-lazy-roundtrip-{Guid.NewGuid():N}");
        string repositoryRoot = "/fake/repo";
        string projectPath = Path.GetFullPath(Path.Combine(repositoryRoot, "src", "Fixture.csproj"));
        // A prepared artifact that does not exist on disk: both prepare time and re-verification
        // time agree it is "missing", so CapturedArtifactsMatch trivially holds while still
        // exercising GetCacheArtifactEvidence's _preparedArtifactPaths/_preparedArtifactContentDigests
        // loops (the metadata closure kept even after a miss narrows the materialized root set).
        string preparedArtifactPath = Path.GetFullPath(Path.Combine(repositoryRoot, "bin", "Fixture.dll"));
        var preparedArtifactContentDigests = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [preparedArtifactPath] = "missing",
        };
        var cacheContext = new AnalysisSnapshotCacheContext(
            new AnalysisCacheLocation(cacheRoot, AnalysisCacheMode.ExplicitPath),
            ConditionSetName: null, ContractIds: Array.Empty<string>(),
            Configuration: null, TargetFramework: null, Platform: null, RuntimeIdentifier: null);
        var document = CreateDocument();

        try
        {
            int firstMaterializeCallCount = 0;
            using (ArchitectureAnalysisSnapshot firstSnapshot = BuildSnapshot(
                document, repositoryRoot,
                materializeSetup: () =>
                {
                    firstMaterializeCallCount++;
                    return CreateWorkingSetup(document, repositoryRoot);
                },
                cacheContext: cacheContext,
                preparedArtifactPaths: new[] { preparedArtifactPath },
                preparedArtifactContentDigests: preparedArtifactContentDigests,
                preparedProjectPaths: new[] { projectPath }))
            {
                ValidationOutcome firstOutcome = firstSnapshot.Evaluate("strict");

                // Evaluate() itself only captures the work-provenance-bearing authorization
                // (CreateWorkProvenance/AttachAuthorization) against the outcome's object identity;
                // it never writes to disk. Publication is the host's job, exactly as
                // ArchitectureValidationCacheSupport.TryPopulateCache does after a real Evaluate —
                // call the same real entry point here to prove the authorization Evaluate attached
                // is actually usable to complete a real write.
                AnalysisCachePopulation.Outcome populationOutcome =
                    AnalysisCachePopulation.TryPopulateCompletedOutcome(firstOutcome);

                Assert.Multiple(() =>
                {
                    Assert.That(firstOutcome.Passed, Is.True);
                    Assert.That(firstMaterializeCallCount, Is.EqualTo(1),
                        "a genuine miss must materialize the runner exactly once");
                    Assert.That(firstSnapshot.CacheStats.Misses, Is.EqualTo(1));
                    Assert.That(populationOutcome.RejectReason, Is.Null);
                    Assert.That(
                        Directory.Exists(cacheRoot)
                            && Directory.EnumerateFiles(cacheRoot, "*.json", SearchOption.AllDirectories).Any(),
                        Is.True,
                        "the miss path must actually publish a real cache entry (exercises CreateWorkProvenance/AttachAuthorization)");
                });
            }

            using ArchitectureAnalysisSnapshot secondSnapshot = BuildSnapshot(
                document, repositoryRoot,
                materializeSetup: () => throw new InvalidOperationException(
                    "a cache hit must never invoke materializeSetup"),
                cacheContext: cacheContext,
                preparedArtifactPaths: new[] { preparedArtifactPath },
                preparedArtifactContentDigests: preparedArtifactContentDigests,
                preparedProjectPaths: new[] { projectPath });

            ValidationOutcome secondOutcome = secondSnapshot.Evaluate("strict");

            Assert.Multiple(() =>
            {
                Assert.That(secondOutcome.Passed, Is.True);
                Assert.That(secondSnapshot.CacheStats.Hits, Is.EqualTo(1));
                Assert.That(secondSnapshot.CacheStats.Misses, Is.Zero);
            });
        }
        finally
        {
            AnalysisCachePopulation.TestManifestCollectorOverride = null;
            if (Directory.Exists(cacheRoot))
            {
                Directory.Delete(cacheRoot, recursive: true);
            }
        }
    }
}
