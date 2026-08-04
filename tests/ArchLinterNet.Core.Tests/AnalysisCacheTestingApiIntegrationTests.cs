using ArchLinterNet.Core.Caching;
using ArchLinterNet.Core.Profiling;
using ArchLinterNet.Testing;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

// Issue #365: Testing API mirror of the CLI's --cache option. See
// openspec/specs/analysis-cache/spec.md, "CLI, Testing API and generic CI guidance agree".
[TestFixture]
public sealed class AnalysisCacheTestingApiIntegrationTests
{
    private static string WriteHarmlessPolicy()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"arch-linter-analysis-cache-testing-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        string policyPath = Path.Combine(tempDir, "dependencies.arch.yml");
        File.WriteAllText(policyPath, """
            version: 1
            name: Test

            layers:
              execution:
                namespace: ArchLinterNet.Core.Execution

            analysis:
              target_assemblies: [ArchLinterNet.Core]
            """);
        return policyPath;
    }

    [Test]
    public void ValidateStrict_WithoutWithCache_ProfileCacheStaysNotApplicable()
    {
        var builder = new ArchitectureValidationBuilder(WriteHarmlessPolicy()).WithProfile();

        ArchitectureValidationResult result = builder.ValidateStrict();

        Assert.That(result.Profile, Is.Not.Null);
        Assert.That(result.Profile!.Counters.Cache.Status, Is.EqualTo(AnalysisProfileReservedFieldStatus.NotApplicable));
    }

    [Test]
    public void ValidateStrict_WithCacheAndNoDiscoveredProjects_ReportsActiveAndIneligibleReject()
    {
        string cacheRoot = Path.Combine(Path.GetTempPath(), $"arch-linter-analysis-cache-testing-root-{Guid.NewGuid():N}");
        try
        {
            var builder = new ArchitectureValidationBuilder(WriteHarmlessPolicy())
                .WithProfile()
                .WithCache(AnalysisCacheOptions.AtPath(cacheRoot));

            ArchitectureValidationResult result = builder.ValidateStrict();

            Assert.That(result.Profile, Is.Not.Null);
            AnalysisProfileCacheCounters cache = result.Profile!.Counters.Cache;
            Assert.Multiple(() =>
            {
                Assert.That(cache.Status, Is.EqualTo(AnalysisProfileReservedFieldStatus.Active));
                Assert.That(cache.Mode, Is.EqualTo("path"));
                Assert.That(cache.Writes, Is.EqualTo(0));
                Assert.That(cache.Rejects, Is.EqualTo(1));
                Assert.That(cache.RejectReasonCounts["IneligibleBuildInput"], Is.EqualTo(1));
            });
            Assert.That(Directory.Exists(cacheRoot) && Directory.EnumerateFiles(cacheRoot, "*.json", SearchOption.AllDirectories).Any(), Is.False);
        }
        finally
        {
            if (Directory.Exists(cacheRoot))
            {
                Directory.Delete(cacheRoot, recursive: true);
            }
        }
    }

    [Test]
    public void ValidateStrict_WithUnsafeCachePath_ReportsTypedPathUnsafeReject()
    {
        string unsafeRoot = OperatingSystem.IsWindows() ? Path.GetPathRoot(Environment.SystemDirectory)! : "/";
        var builder = new ArchitectureValidationBuilder(WriteHarmlessPolicy())
            .WithProfile()
            .WithCache(AnalysisCacheOptions.AtPath(unsafeRoot));

        ArchitectureValidationResult result = builder.ValidateStrict();

        Assert.That(result.Profile, Is.Not.Null);
        Core.Profiling.AnalysisProfileCacheCounters cache = result.Profile!.Counters.Cache;
        Assert.Multiple(() =>
        {
            Assert.That(cache.Status, Is.EqualTo(AnalysisProfileReservedFieldStatus.Active));
            Assert.That(cache.Rejects, Is.EqualTo(1));
            Assert.That(cache.RejectReasonCounts["PathUnsafe"], Is.EqualTo(1));
        });
    }
}
