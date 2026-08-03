using ArchLinterNet.Core.BuildState;
using ArchLinterNet.Core.Caching;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;
using ArchLinterNet.Core.Validation;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class AnalysisCachePopulationTests
{
    private string _repoRoot = null!;
    private string _projectPath = null!;
    private string _cacheRoot = null!;

    [SetUp]
    public void SetUp()
    {
        _repoRoot = Path.Combine(Path.GetTempPath(), "arch-linter-net-cache-population-tests", Guid.NewGuid().ToString("N"));
        string projectDir = Path.Combine(_repoRoot, "src", "Sample");
        Directory.CreateDirectory(projectDir);
        _projectPath = Path.Combine(projectDir, "Sample.csproj");
        File.WriteAllText(_projectPath, "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>");
        _cacheRoot = Path.Combine(_repoRoot, ".cache");
    }

    [TearDown]
    public void TearDown()
    {
        AnalysisCachePopulation.TestManifestCollectorOverride = null;

        if (Directory.Exists(_repoRoot))
        {
            Directory.Delete(_repoRoot, recursive: true);
        }
    }

    private static AnalysisCacheKey CreateKey() => new(
        "policy", "strict", null, "contracts", "workspace", null, null, null, null);

    private static AnalysisCacheOutcomeV1 SampleOutcome() => new(
        true, Array.Empty<ArchitectureViolation>(), Array.Empty<string>(), Array.Empty<ArchitectureViolation>(), "off",
        Array.Empty<ArchitectureUnmatchedIgnoredViolation>(), "off", Array.Empty<PolicyConsistencyDiagnostic>(), "off",
        Array.Empty<ArchitectureClassificationConflict>(), Array.Empty<ArchitectureClassificationMetadataFailure>());

    private static ValidationOutcome SampleValidationOutcome() => new(
        true, Array.Empty<ArchitectureViolation>(), Array.Empty<string>(), Array.Empty<ArchitectureViolation>(), "off",
        Array.Empty<ArchitectureUnmatchedIgnoredViolation>(), "off", Array.Empty<PolicyConsistencyDiagnostic>(), "off",
        Array.Empty<ArchitectureCoverageSummary>(), Array.Empty<ArchitectureClassificationConflict>(),
        Array.Empty<ArchitectureClassificationMetadataFailure>());

    // Documents the current, intentional state (see design.md): #406's
    // EvaluatedBuildInputManifestCollector always reports CacheIneligible for real MSBuild
    // evidence today, so populating from an actual discovered project is always rejected — a
    // cache entry is never fabricated from unproven build-input evidence.
    [Test]
    public void TryPopulate_RealProject_IsIneligibleBuildInputToday()
    {
        AnalysisCacheLocation location = new(_cacheRoot, AnalysisCacheMode.ExplicitPath);

        AnalysisCachePopulation.Outcome outcome = AnalysisCachePopulation.TryPopulate(
            location, CreateKey(), new[] { _projectPath }, _repoRoot,
            null, null, null, null, SampleOutcome());

        Assert.That(outcome.RejectReason, Is.EqualTo(AnalysisCacheRejectReason.IneligibleBuildInput));
        Assert.That(outcome.ProjectsEvaluated, Is.EqualTo(1));
        Assert.That(outcome.IneligibleProjectCount, Is.EqualTo(1));
        Assert.That(outcome.BytesWritten, Is.EqualTo(0));
        Assert.That(Directory.Exists(_cacheRoot) && Directory.EnumerateFiles(_cacheRoot, "*.json", SearchOption.AllDirectories).Any(), Is.False);
    }

    [Test]
    public void TryPopulate_Disabled_ReturnsDisabledReason()
    {
        AnalysisCachePopulation.Outcome outcome = AnalysisCachePopulation.TryPopulate(
            location: null, CreateKey(), new[] { _projectPath }, _repoRoot, null, null, null, null, SampleOutcome());

        Assert.That(outcome.RejectReason, Is.EqualTo(AnalysisCacheRejectReason.Disabled));
    }

    [Test]
    public void TryPopulate_NoDiscoveredProjects_IsIneligible()
    {
        AnalysisCacheLocation location = new(_cacheRoot, AnalysisCacheMode.ExplicitPath);

        AnalysisCachePopulation.Outcome outcome = AnalysisCachePopulation.TryPopulate(
            location, CreateKey(), Array.Empty<string>(), _repoRoot, null, null, null, null, SampleOutcome());

        Assert.That(outcome.RejectReason, Is.EqualTo(AnalysisCacheRejectReason.IneligibleBuildInput));
        Assert.That(outcome.ProjectsEvaluated, Is.EqualTo(0));
    }

    [Test]
    public void TryPopulateCompletedOutcome_PreflightBlocked_IsNeverPublished()
    {
        ValidationOutcome blocked = new(
            false, Array.Empty<ArchitectureViolation>(), Array.Empty<string>(), Array.Empty<ArchitectureViolation>(), "off",
            Array.Empty<ArchitectureUnmatchedIgnoredViolation>(), "off", Array.Empty<PolicyConsistencyDiagnostic>(), "off",
            Array.Empty<ArchitectureCoverageSummary>(), Array.Empty<ArchitectureClassificationConflict>(),
            Array.Empty<ArchitectureClassificationMetadataFailure>())
        {
            PreflightBlocked = true,
        };

        AnalysisCachePopulation.Outcome outcome = AnalysisCachePopulation.TryPopulateCompletedOutcome(blocked);

        Assert.Multiple(() =>
        {
            Assert.That(outcome.RejectReason, Is.EqualTo(AnalysisCacheRejectReason.IncompleteOriginalRun));
            Assert.That(outcome.PopulationAttempted, Is.True);
            Assert.That(Directory.Exists(_cacheRoot), Is.False);
        });
    }

    [Test]
    public void TryLookupWithAuthorization_ExplicitSourceInputs_IsIneligibleAndDoesNotAuthorizeReuse()
    {
        AnalysisCachePopulation.TestManifestCollectorOverride =
            (_, _, _, _, _, _, _) => new EvaluatedBuildInputManifestV1(
                "verified-manifest", CacheEligibility.VerifiedCacheEligible,
                Array.Empty<string>(), Array.Empty<string>());
        AnalysisCacheLocation location = new(_cacheRoot, AnalysisCacheMode.ExplicitPath);

        AnalysisCachePopulation.LookupPreparation preparation = AnalysisCachePopulation.TryLookupWithAuthorization(
            location, CreateKey(), new[] { _projectPath }, Array.Empty<string>(), _repoRoot,
            null, null, null, null, hasUnfingerprintedSourceInputs: true);

        Assert.Multiple(() =>
        {
            Assert.That(preparation.Lookup.Outcome, Is.EqualTo(AnalysisCacheLookupOutcome.Reject));
            Assert.That(preparation.Lookup.Reason, Is.EqualTo(AnalysisCacheRejectReason.IneligibleBuildInput));
            Assert.That(preparation.Authorization, Is.Null);
        });
    }

    [Test]
    public void TryPopulateCompletedOutcome_CompletionScopeReference_IsIncludedInArtifactManifest()
    {
        AnalysisCachePopulation.TestManifestCollectorOverride =
            (_, _, _, _, _, _, _) => new EvaluatedBuildInputManifestV1(
                "verified-manifest", CacheEligibility.VerifiedCacheEligible,
                Array.Empty<string>(), Array.Empty<string>());
        AnalysisCacheLocation location = new(_cacheRoot, AnalysisCacheMode.ExplicitPath);
        string selectedArtifact = Path.Combine(_repoRoot, "bin", "Selected.dll");
        string loadedReference = Path.Combine(_repoRoot, "bin", "Referenced.dll");
        Directory.CreateDirectory(Path.GetDirectoryName(selectedArtifact)!);
        File.WriteAllText(selectedArtifact, "selected-v1");
        File.WriteAllText(loadedReference, "reference-v1");

        AnalysisCachePopulation.LookupPreparation preparation = AnalysisCachePopulation.TryLookupWithAuthorization(
            location, CreateKey(), new[] { _projectPath }, new[] { selectedArtifact }, _repoRoot,
            null, null, null, null, hasUnfingerprintedSourceInputs: false);
        Assert.That(preparation.Authorization, Is.Not.Null);

        ValidationOutcome outcome = SampleValidationOutcome();
        AnalysisCachePopulation.AttachAuthorization(
            outcome, preparation.Authorization!, new[] { selectedArtifact, loadedReference });

        AnalysisCachePopulation.Outcome population = AnalysisCachePopulation.TryPopulateCompletedOutcome(outcome);
        AnalysisCachePopulation.LookupPreparation hit = AnalysisCachePopulation.TryLookupWithAuthorization(
            location, CreateKey(), new[] { _projectPath }, new[] { selectedArtifact, loadedReference }, _repoRoot,
            null, null, null, null, hasUnfingerprintedSourceInputs: false);

        Assert.Multiple(() =>
        {
            Assert.That(population.RejectReason, Is.Null);
            Assert.That(hit.Lookup.Outcome, Is.EqualTo(AnalysisCacheLookupOutcome.Hit));
        });

        File.WriteAllText(loadedReference, "reference-v2");
        AnalysisCachePopulation.LookupPreparation changedReference = AnalysisCachePopulation.TryLookupWithAuthorization(
            location, CreateKey(), new[] { _projectPath }, new[] { selectedArtifact, loadedReference }, _repoRoot,
            null, null, null, null, hasUnfingerprintedSourceInputs: false);

        Assert.That(changedReference.Lookup.Reason, Is.EqualTo(AnalysisCacheRejectReason.ArtifactSetMismatch));
    }

    [Test]
    public void AttachAuthorization_DoesNotChangeValidationOutcomeValueEquality()
    {
        AnalysisCachePopulation.TestManifestCollectorOverride =
            (_, _, _, _, _, _, _) => new EvaluatedBuildInputManifestV1(
                "verified-manifest", CacheEligibility.VerifiedCacheEligible,
                Array.Empty<string>(), Array.Empty<string>());
        AnalysisCacheLocation location = new(_cacheRoot, AnalysisCacheMode.ExplicitPath);
        AnalysisCachePopulation.LookupPreparation preparation = AnalysisCachePopulation.TryLookupWithAuthorization(
            location, CreateKey(), new[] { _projectPath }, Array.Empty<string>(), _repoRoot,
            null, null, null, null, hasUnfingerprintedSourceInputs: false);
        Assert.That(preparation.Authorization, Is.Not.Null);

        ValidationOutcome cacheEnabled = SampleValidationOutcome();
        ValidationOutcome cacheDisabled = SampleValidationOutcome();
        AnalysisCachePopulation.AttachAuthorization(cacheEnabled, preparation.Authorization!, Array.Empty<string>());

        Assert.Multiple(() =>
        {
            Assert.That(cacheEnabled, Is.EqualTo(cacheDisabled));
            Assert.That(cacheEnabled.GetHashCode(), Is.EqualTo(cacheDisabled.GetHashCode()));
        });
    }
}
