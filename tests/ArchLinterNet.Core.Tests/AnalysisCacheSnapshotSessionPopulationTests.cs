using ArchLinterNet.Core.BuildState;
using ArchLinterNet.Core.Caching;
using ArchLinterNet.Core.Composition;
using ArchLinterNet.Core.Validation;
using ArchLinterNet.Testing;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

// Finding #7: ArchitectureValidationBuilder.CreateSnapshot()'s session previously only ever
// consulted the cache for lookups — ArchitectureValidationSnapshotSession.Evaluate never called
// AnalysisCachePopulation.TryPopulate, so a Testing snapshot miss could never seed a later
// snapshot's hit. This exercises the real engine (ArchitectureValidationBuilder ->
// ArchitectureAnalysisSnapshot.Evaluate), with AnalysisCachePopulation.TestManifestCollectorOverride
// substituted for the real EvaluatedBuildInputManifestCollector — which always reports
// CacheIneligible for this repository's own MSBuild evidence today (see design.md) — so the test
// can prove a genuine write-then-hit round trip instead of only ever observing IneligibleBuildInput.
//
// The policy below deliberately uses a project-metadata-only contract (analysis.target_assemblies
// left empty) so build-state preflight never blocks the run (see
// ArchitectureRunnerSetupServiceDiscoveryTests.BuildRunner_ProjectMetadataOnlyPolicy_...) while
// analysis.projects still drives real project discovery, giving genuinely non-empty
// DiscoveredProjectPaths to key the cache on.
[TestFixture]
public sealed class AnalysisCacheSnapshotSessionPopulationTests
{
    private string _tempDir = null!;
    private string _policyPath = null!;
    private string _cacheRoot = null!;

    [SetUp]
    public void SetUp()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"arch-linter-snapshot-cache-population-{Guid.NewGuid():N}");
        string projectDir = Path.Combine(_tempDir, "src", "Fixture");
        Directory.CreateDirectory(projectDir);
        File.WriteAllText(Path.Combine(projectDir, "Fixture.csproj"), """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
            </Project>
            """);

        _policyPath = Path.Combine(_tempDir, "dependencies.arch.yml");
        File.WriteAllText(_policyPath, """
            version: 1
            name: Test
            layers: {}
            analysis:
              target_assemblies: []
              projects: ["src/Fixture/Fixture.csproj"]
            contracts:
              strict_project_metadata:
                - name: project-metadata
                  projects:
                    - src/Fixture/Fixture.csproj
                  required_properties:
                    TargetFramework: net10.0
            """);

        _cacheRoot = Path.Combine(_tempDir, ".cache");
    }

    [TearDown]
    public void TearDown()
    {
        AnalysisCachePopulation.TestManifestCollectorOverride = null;
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    private static EvaluatedBuildInputManifestV1 AlwaysEligible(
        string projectPath, string repositoryRoot, string? configuration, string? targetFramework,
        string? platform, string? runtimeIdentifier, CancellationToken cancellationToken) =>
        new("fixed-digest", CacheEligibility.VerifiedCacheEligible, Array.Empty<string>(), Array.Empty<string>());

    [Test]
    public void CreateSnapshot_Evaluate_PopulatesCacheOnMissThenHitsOnSecondSnapshot()
    {
        AnalysisCachePopulation.TestManifestCollectorOverride = AlwaysEligible;

        using (ArchitectureValidationSnapshotSession session1 =
               new ArchitectureValidationBuilder(_policyPath).WithCache(AnalysisCacheOptions.AtPath(_cacheRoot)).CreateSnapshot())
        {
            ArchitectureValidationResult result1 = session1.ValidateStrict();

            Assert.That(result1.PreflightBlocked, Is.False);
            Assert.That(session1.Counters.CacheLookups, Is.Not.Null);
            Assert.That(session1.Counters.CacheLookups!.Misses, Is.EqualTo(1));
        }

        Assert.That(
            Directory.Exists(_cacheRoot) && Directory.EnumerateFiles(_cacheRoot, "*.json", SearchOption.AllDirectories).Any(),
            Is.True,
            "A completed, non-cancelled snapshot miss must populate a reusable cache entry.");

        using (ArchitectureValidationSnapshotSession session2 =
               new ArchitectureValidationBuilder(_policyPath).WithCache(AnalysisCacheOptions.AtPath(_cacheRoot)).CreateSnapshot())
        {
            ArchitectureValidationResult result2 = session2.ValidateStrict();

            Assert.That(result2.PreflightBlocked, Is.False);
            Assert.That(session2.Counters.CacheLookups, Is.Not.Null);
            Assert.That(session2.Counters.CacheLookups!.Hits, Is.EqualTo(1));
        }
    }

    // Finding #8: the Testing host previously left CorruptionEvents at zero entirely and never
    // added lookups.Rejects into the scalar Rejects total — a corrupt lookup on this run must show
    // up in both, and Rejects must always equal the sum of RejectReasonCounts. This exercises the
    // real engine end to end via ArchitectureValidationBuilder (not the snapshot session): a first
    // Validate() populates a real entry (via the fake eligible-manifest collector), the entry file
    // is hand-corrupted, and a second Validate() must observe a corrupt lookup reject while its own
    // population attempt succeeds again (overwriting the corrupt entry) — Rejects must equal
    // lookups.Rejects (1) + population rejects (0) = 1.
    [Test]
    public void ValidateStrict_WithCorruptedExistingEntry_AggregatesRejectsAndCorruptionEventsAcrossBothSides()
    {
        AnalysisCachePopulation.TestManifestCollectorOverride = AlwaysEligible;

        ArchitectureValidationResult first = new ArchitectureValidationBuilder(_policyPath)
            .WithProfile()
            .WithCache(AnalysisCacheOptions.AtPath(_cacheRoot))
            .ValidateStrict();
        Assert.That(first.PreflightBlocked, Is.False);

        string entryPath = Directory.EnumerateFiles(_cacheRoot, "*.json", SearchOption.AllDirectories).Single();
        File.WriteAllText(entryPath, "{ not valid json");

        ArchitectureValidationResult second = new ArchitectureValidationBuilder(_policyPath)
            .WithProfile()
            .WithCache(AnalysisCacheOptions.AtPath(_cacheRoot))
            .ValidateStrict();

        Assert.That(second.Profile, Is.Not.Null);
        Core.Profiling.AnalysisProfileCacheCounters cache = second.Profile!.Counters.Cache;
        Assert.Multiple(() =>
        {
            Assert.That(cache.RejectReasonCounts.GetValueOrDefault("Corrupt"), Is.EqualTo(1));
            Assert.That(cache.CorruptionEvents, Is.EqualTo(1));
            Assert.That(cache.Rejects, Is.EqualTo(cache.RejectReasonCounts.Values.Sum()));
            Assert.That(cache.Rejects, Is.EqualTo(1));
        });
    }

    // Finding #3: a cache hit must never be accepted once cancellation has been observed — an
    // already-populated entry exists here (via the same fake-eligible-manifest harness as the test
    // above), but the snapshot's session cancellation token is cancelled before Evaluate() runs.
    // Before the fix, TryEvaluateFromCache never checked cancellation at all and could return a
    // successful cached outcome instead of surfacing OperationCanceledException.
    [Test]
    public void Evaluate_CancelledBeforeLookupAccepted_ThrowsInsteadOfReturningCachedHit()
    {
        AnalysisCachePopulation.TestManifestCollectorOverride = AlwaysEligible;

        // Seed a real, hit-eligible entry via the Testing builder's independent-run path
        // (uncancelled).
        new ArchitectureValidationBuilder(_policyPath).WithCache(AnalysisCacheOptions.AtPath(_cacheRoot)).ValidateStrict();

        Assert.That(Directory.Exists(_cacheRoot) && Directory.EnumerateFiles(_cacheRoot, "*.json", SearchOption.AllDirectories).Any(), Is.True);

        using ArchitectureEngine engine = new ArchitectureEngineBuilder().AddArchLinterNetCore().Build();
        using CancellationTokenSource cts = new();
        AnalysisSnapshotRequest request = new()
        {
            PolicyPath = _policyPath,
            CacheLocation = AnalysisCacheLocationResolver.Resolve(AnalysisCacheOptions.AtPath(_cacheRoot)),
            CancellationToken = cts.Token,
        };

        using ArchitectureAnalysisSnapshot snapshot = engine.CreateSnapshot(request);

        // Cancel only after construction succeeded — this simulates cancellation observed during
        // (or immediately before) the lookup itself, not during policy/project setup.
        cts.Cancel();

        Assert.Throws<OperationCanceledException>(() => snapshot.Evaluate("strict"));
    }

    [Test]
    public void CreateSnapshot_WithoutWithCache_NeverAttemptsPopulation()
    {
        AnalysisCachePopulation.TestManifestCollectorOverride = AlwaysEligible;

        using ArchitectureValidationSnapshotSession session =
            new ArchitectureValidationBuilder(_policyPath).CreateSnapshot();
        session.ValidateStrict();

        Assert.That(Directory.Exists(_cacheRoot), Is.False);
    }
}
