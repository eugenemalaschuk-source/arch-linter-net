using ArchLinterNet.Core.BuildState;
using ArchLinterNet.Core.Caching;
using ArchLinterNet.Core.Composition;
using ArchLinterNet.Core.Validation;
using ArchLinterNet.Testing;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

// The policy below deliberately uses a project-metadata-only contract (analysis.target_assemblies
// left empty) so build-state preflight never blocks the run. It has no isolated assembly scope,
// and therefore must remain cache-ineligible until ordinary resolution supplies the equivalent
// exact-byte root/reference inventory.
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
    public void CreateSnapshot_Evaluate_WithoutArtifactInventoryIsCacheIneligible()
    {
        AnalysisCachePopulation.TestManifestCollectorOverride = AlwaysEligible;

        using ArchitectureValidationSnapshotSession session =
            new ArchitectureValidationBuilder(_policyPath).WithCache(AnalysisCacheOptions.AtPath(_cacheRoot)).CreateSnapshot();

        ArchitectureValidationResult result = session.ValidateStrict();
        AnalysisCacheLookupStats lookup = session.Counters.CacheLookups!;

        Assert.Multiple(() =>
        {
            Assert.That(result.PreflightBlocked, Is.False);
            Assert.That(lookup.Rejects, Is.EqualTo(1));
            Assert.That(lookup.RejectReasonCounts["IneligibleBuildInput"], Is.EqualTo(1));
            Assert.That(Directory.Exists(_cacheRoot), Is.False);
        });
    }

    [Test]
    public void ValidateStrict_WithoutArtifactInventory_DoesNotReadExistingCacheEntry()
    {
        AnalysisCachePopulation.TestManifestCollectorOverride = AlwaysEligible;

        Directory.CreateDirectory(_cacheRoot);
        File.WriteAllText(Path.Combine(_cacheRoot, "untrusted.json"), "{ not valid json");

        ArchitectureValidationResult result = new ArchitectureValidationBuilder(_policyPath)
            .WithProfile()
            .WithCache(AnalysisCacheOptions.AtPath(_cacheRoot))
            .ValidateStrict();

        Assert.That(result.Profile, Is.Not.Null);
        Core.Profiling.AnalysisProfileCacheCounters cache = result.Profile!.Counters.Cache;
        Assert.Multiple(() =>
        {
            Assert.That(cache.RejectReasonCounts.GetValueOrDefault("IneligibleBuildInput"), Is.EqualTo(1));
            Assert.That(cache.CorruptionEvents, Is.Zero);
            Assert.That(cache.Rejects, Is.EqualTo(cache.RejectReasonCounts.Values.Sum()));
            Assert.That(cache.Rejects, Is.EqualTo(1));
        });
    }

    [Test]
    public void Evaluate_CancelledBeforeCacheEligibilityIsResolved_Throws()
    {
        AnalysisCachePopulation.TestManifestCollectorOverride = AlwaysEligible;

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
        // cache setup, rather than during policy/project setup.
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

    [Test]
    public void CreateSnapshot_ExplicitSourceRoots_IsIneligibleEvenWhenProjectInputsAreEligible()
    {
        File.WriteAllText(_policyPath, """
            version: 1
            name: Test
            layers: {}
            analysis:
              target_assemblies: []
              projects: ["src/Fixture/Fixture.csproj"]
              source_roots: ["src"]
            contracts:
              strict_project_metadata:
                - name: project-metadata
                  projects:
                    - src/Fixture/Fixture.csproj
                  required_properties:
                    TargetFramework: net10.0
            """);
        AnalysisCachePopulation.TestManifestCollectorOverride = AlwaysEligible;

        using ArchitectureValidationSnapshotSession session =
            new ArchitectureValidationBuilder(_policyPath).WithCache(AnalysisCacheOptions.AtPath(_cacheRoot)).CreateSnapshot();

        session.ValidateStrict();

        AnalysisCacheLookupStats lookup = session.Counters.CacheLookups!;
        Assert.Multiple(() =>
        {
            Assert.That(lookup.Rejects, Is.EqualTo(1));
            Assert.That(lookup.RejectReasonCounts["IneligibleBuildInput"], Is.EqualTo(1));
            Assert.That(Directory.Exists(_cacheRoot) && Directory.EnumerateFiles(_cacheRoot, "*.json", SearchOption.AllDirectories).Any(), Is.False);
        });
    }

    [Test]
    public void ValidateStrict_SelectedAsmdefContract_IsCacheIneligible()
    {
        string assetsDirectory = Path.Combine(_tempDir, "Assets");
        Directory.CreateDirectory(assetsDirectory);
        File.WriteAllText(Path.Combine(assetsDirectory, "Runtime.asmdef"), """
            { "name": "Runtime", "references": ["Editor"] }
            """);
        File.WriteAllText(Path.Combine(assetsDirectory, "Editor.asmdef"), """
            { "name": "Editor", "includePlatforms": ["Editor"] }
            """);
        File.WriteAllText(_policyPath, """
            version: 1
            name: Test
            layers: {}
            analysis:
              target_assemblies: [ArchLinterNet.Core]
              projects: ["src/Fixture/Fixture.csproj"]
            contracts:
              strict_project_metadata:
                - id: project-metadata
                  name: project-metadata
                  projects:
                    - src/Fixture/Fixture.csproj
                  required_properties:
                    TargetFramework: net10.0
              strict_asmdef:
                - id: runtime-no-editor
                  name: runtime-no-editor
                  source_assemblies: [Runtime]
                  forbidden_editor_refs: true
            """);
        AnalysisCachePopulation.TestManifestCollectorOverride = AlwaysEligible;

        ArchitectureValidationResult result = new ArchitectureValidationBuilder(_policyPath)
            .WithContracts("runtime-no-editor")
            .WithProfile()
            .WithCache(AnalysisCacheOptions.AtPath(_cacheRoot))
            .ValidateStrict();

        Assert.Multiple(() =>
        {
            Assert.That(result.PreflightBlocked, Is.False);
            Assert.That(result.Profile, Is.Not.Null);
            Assert.That(result.Profile!.Counters.Cache.RejectReasonCounts["IneligibleBuildInput"], Is.EqualTo(1));
            Assert.That(Directory.Exists(_cacheRoot), Is.False);
        });
    }

    [Test]
    public void CreateSnapshot_WithoutArtifactInventory_DoesNotCollectBuildInputManifests()
    {
        int collectionCount = 0;
        AnalysisCachePopulation.TestManifestCollectorOverride =
            (projectPath, repositoryRoot, configuration, targetFramework, platform, runtimeIdentifier, cancellationToken) =>
                new(
                    Interlocked.Increment(ref collectionCount) == 1 ? "before-analysis" : "after-analysis",
                    CacheEligibility.VerifiedCacheEligible,
                    Array.Empty<string>(),
                    Array.Empty<string>());

        using ArchitectureValidationSnapshotSession session =
            new ArchitectureValidationBuilder(_policyPath).WithCache(AnalysisCacheOptions.AtPath(_cacheRoot)).CreateSnapshot();

        session.ValidateStrict();

        Assert.Multiple(() =>
        {
            Assert.That(collectionCount, Is.Zero,
                "runs without an exact artifact inventory must fail closed before build-input fingerprinting");
            Assert.That(Directory.Exists(_cacheRoot) && Directory.EnumerateFiles(_cacheRoot, "*.json", SearchOption.AllDirectories).Any(), Is.False);
        });
    }
}
