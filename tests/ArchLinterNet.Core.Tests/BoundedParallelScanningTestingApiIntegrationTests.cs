using ArchLinterNet.Core.Caching;
using ArchLinterNet.Core.Profiling;
using ArchLinterNet.Testing;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

// Issue #408: Testing API mirror of the CLI's --max-parallelism option, exercised end to end
// against the real ArchLinterNet.Core assembly. See
// openspec/specs/bounded-parallel-scanning/spec.md.
[TestFixture]
public sealed class BoundedParallelScanningTestingApiIntegrationTests
{
    private static string WriteHarmlessPolicy()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"arch-linter-bounded-parallel-testing-{Guid.NewGuid():N}");
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
    public void ValidateStrict_MaxParallelismOneVersusDefault_ProducesIdenticalOutcome()
    {
        string policyPath = WriteHarmlessPolicy();

        ArchitectureValidationResult sequential =
            new ArchitectureValidationBuilder(policyPath).WithMaxParallelism(1).ValidateStrict();
        ArchitectureValidationResult parallel =
            new ArchitectureValidationBuilder(policyPath).WithMaxParallelism(4).ValidateStrict();

        Assert.Multiple(() =>
        {
            Assert.That(parallel.Passed, Is.EqualTo(sequential.Passed));
            Assert.That(parallel.Violations.Select(v => v.Identity), Is.EqualTo(sequential.Violations.Select(v => v.Identity)));
        });
    }

    [Test]
    public void ValidateStrict_ZeroMaxParallelism_ThrowsArgumentException()
    {
        var builder = new ArchitectureValidationBuilder(WriteHarmlessPolicy()).WithMaxParallelism(0);

        Assert.Throws<ArgumentException>(() => builder.ValidateStrict());
    }

    [Test]
    public void ValidateStrict_NegativeMaxParallelism_ThrowsArgumentException()
    {
        var builder = new ArchitectureValidationBuilder(WriteHarmlessPolicy()).WithMaxParallelism(-1);

        Assert.Throws<ArgumentException>(() => builder.ValidateStrict());
    }

    [Test]
    public void ValidateStrict_WithProfileAndDefaultMaxParallelism_ReportsResolvedMaxParallelism()
    {
        ArchitectureValidationResult result =
            new ArchitectureValidationBuilder(WriteHarmlessPolicy()).WithProfile().ValidateStrict();

        Assert.That(result.Profile, Is.Not.Null);
        Assert.That(
            result.Profile!.Counters.Concurrency.MaxParallelism,
            Is.EqualTo(Math.Max(1, Math.Min(Environment.ProcessorCount, 4))));
    }

    [Test]
    public void ValidateStrict_WithProfileAndMaxParallelismOne_ReportsNotApplicableConcurrency()
    {
        ArchitectureValidationResult result = new ArchitectureValidationBuilder(WriteHarmlessPolicy())
            .WithProfile()
            .WithMaxParallelism(1)
            .ValidateStrict();

        Assert.That(result.Profile, Is.Not.Null);
        Assert.That(
            result.Profile!.Counters.Concurrency.Status,
            Is.EqualTo(AnalysisProfileReservedFieldStatus.NotApplicable));
    }

    // Bounded parallel scanning must not change the existing per-mode cache-lookup-then-scan
    // ordering: a cache-enabled-but-missing run uses the same scanning phases (now potentially
    // parallel) as a cache-disabled run and produces the same outcome. See
    // openspec/specs/bounded-parallel-scanning/spec.md, "Cached and uncached execution remain
    // equivalent under bounded parallel scanning".
    [Test]
    public void ValidateStrict_CacheEnabledMissVersusCacheDisabled_ProduceIdenticalOutcomeUnderParallelScanning()
    {
        string policyPath = WriteHarmlessPolicy();
        string cacheRoot = Path.Combine(Path.GetTempPath(), $"arch-linter-bounded-parallel-cache-{Guid.NewGuid():N}");

        ArchitectureValidationResult cacheDisabled =
            new ArchitectureValidationBuilder(policyPath).WithMaxParallelism(4).ValidateStrict();
        ArchitectureValidationResult cacheEnabledMiss = new ArchitectureValidationBuilder(policyPath)
            .WithMaxParallelism(4)
            .WithCache(AnalysisCacheOptions.AtPath(cacheRoot))
            .ValidateStrict();

        Assert.Multiple(() =>
        {
            Assert.That(cacheEnabledMiss.Passed, Is.EqualTo(cacheDisabled.Passed));
            Assert.That(
                cacheEnabledMiss.Violations.Select(v => v.Identity),
                Is.EqualTo(cacheDisabled.Violations.Select(v => v.Identity)));
        });
    }
}
