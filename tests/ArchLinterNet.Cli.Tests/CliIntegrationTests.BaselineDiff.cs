using NUnit.Framework;

namespace ArchLinterNet.Cli.Tests;

public partial class CliIntegrationTests
{
    /* baseline diff */

    [Test]
    public void BaselineDiff_InSyncBaseline_ReportsNoNewResolvedOrConfigurationErrors()
    {
        string baselinePath = Path.Combine(Path.GetTempPath(), $"baseline-{Guid.NewGuid():N}.yml");
        try
        {
            var (genExit, _, genStderr) = RunCli("baseline", "generate",
                "--config", _graphPolicy, "--output", baselinePath);
            Assert.That(genExit, Is.EqualTo(0), $"Baseline generation should succeed, stderr: {genStderr}");

            var (exitCode, stdout, stderr) = RunCli("baseline", "diff",
                "--config", _graphPolicy, "--baseline", baselinePath);

            Assert.Multiple(() =>
            {
                Assert.That(exitCode, Is.EqualTo(0), $"Diff should succeed, stderr: {stderr}");
                Assert.That(stdout, Does.Contain("New (unbaselined) violations: 0"));
                Assert.That(stdout, Does.Contain("Resolved (stale) baseline entries: 0"));
                Assert.That(stdout, Does.Contain("Configuration errors (unknown contract id): 0"));
                Assert.That(stdout, Does.Not.Contain("Existing (frozen) baseline entries: 0"));
            });
        }
        finally
        {
            if (File.Exists(baselinePath))
                File.Delete(baselinePath);
        }
    }

    [Test]
    public void BaselineDiff_ResolvedAndUnknownContractId_ReportsBothCategories()
    {
        string baselinePath = Path.Combine(Path.GetTempPath(), $"baseline-{Guid.NewGuid():N}.yml");
        try
        {
            File.WriteAllText(baselinePath, @"
version: 1
baseline:
  strict:
    - id: core-no-forbidden
      ignored_violations:
        - source_type: Fake.Source
          forbidden_reference: Fake.Target
          reason: stale entry
    - id: totally-unknown-id
      ignored_violations:
        - source_type: Fake.Source2
          forbidden_reference: Fake.Target2
          reason: bad id
");

            var (exitCode, stdout, stderr) = RunCli("baseline", "diff",
                "--config", _passingWithIdsPolicy, "--baseline", baselinePath);

            Assert.Multiple(() =>
            {
                Assert.That(exitCode, Is.EqualTo(0), $"Diff should succeed, stderr: {stderr}");
                Assert.That(stdout, Does.Contain("Resolved (stale) baseline entries: 1"));
                Assert.That(stdout, Does.Contain("Configuration errors (unknown contract id): 1"));
                Assert.That(stdout, Does.Contain("Fake.Source -> Fake.Target"));
                Assert.That(stdout, Does.Contain("totally-unknown-id"));
            });
        }
        finally
        {
            if (File.Exists(baselinePath))
                File.Delete(baselinePath);
        }
    }

    [Test]
    public void BaselineDiff_SelectedContract_ScopesComparisonToThatContractOnly()
    {
        string baselinePath = Path.Combine(Path.GetTempPath(), $"baseline-{Guid.NewGuid():N}.yml");
        try
        {
            File.WriteAllText(baselinePath, @"
version: 1
baseline:
  strict:
    - id: core-no-forbidden
      ignored_violations:
        - source_type: Fake.Source
          forbidden_reference: Fake.Target
          reason: stale entry
    - id: no-non-existent
      ignored_violations:
        - source_type: Fake.Source2
          forbidden_reference: Fake.Target2
          reason: stale entry two
");

            var (exitCode, stdout, stderr) = RunCli("baseline", "diff",
                "--config", _passingWithIdsPolicy, "--baseline", baselinePath,
                "--contract", "core-no-forbidden");

            Assert.Multiple(() =>
            {
                Assert.That(exitCode, Is.EqualTo(0), $"Diff should succeed, stderr: {stderr}");
                Assert.That(stdout, Does.Contain("Resolved (stale) baseline entries: 1"));
                Assert.That(stdout, Does.Contain("Fake.Source -> Fake.Target"));
                Assert.That(stdout, Does.Not.Contain("Fake.Source2"));
            });
        }
        finally
        {
            if (File.Exists(baselinePath))
                File.Delete(baselinePath);
        }
    }

    [Test]
    public void BaselineDiff_UnknownConditionSet_ExitsTwo()
    {
        string baselinePath = Path.Combine(Path.GetTempPath(), $"baseline-{Guid.NewGuid():N}.yml");
        try
        {
            File.WriteAllText(baselinePath, "version: 1\nbaseline: {}\n");

            var (exitCode, _, stderr) = RunCli("baseline", "diff",
                "--config", _passingPolicy, "--baseline", baselinePath, "--condition-set", "nonexistent");

            Assert.Multiple(() =>
            {
                Assert.That(exitCode, Is.EqualTo(2));
                Assert.That(stderr, Does.Contain("condition set"));
                Assert.That(stderr, Does.Contain("nonexistent"));
            });
        }
        finally
        {
            if (File.Exists(baselinePath))
                File.Delete(baselinePath);
        }
    }

    [Test]
    public void BaselineDiff_MissingBaselineFile_ExitsTwo()
    {
        var (exitCode, _, stderr) = RunCli("baseline", "diff",
            "--config", _passingPolicy, "--baseline", "/nonexistent/baseline.yml");

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(2));
            Assert.That(stderr, Does.Contain("not found"));
        });
    }

    [Test]
    public void BaselineDiff_MissingBaselineFlag_ExitsTwo()
    {
        var (exitCode, _, stderr) = RunCli("baseline", "diff", "--config", _passingPolicy);

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(2));
            Assert.That(stderr, Does.Contain("--baseline"));
        });
    }

    [Test]
    public void BaselineDiffHelp_ShowsUsage()
    {
        var (exitCode, stdout, _) = RunCli("baseline", "diff", "--help");

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(0));
            Assert.That(stdout, Does.Contain("baseline diff"));
            Assert.That(stdout, Does.Contain("--baseline"));
            Assert.That(stdout, Does.Contain("--json"));
        });
    }
}
