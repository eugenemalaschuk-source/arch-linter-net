using NUnit.Framework;

namespace ArchLinterNet.Cli.Tests;

public partial class CliIntegrationTests
{
    /* baseline migrate */

    [Test]
    public void BaselineMigrate_StaleLegacyEntry_MigratesCleanlyWithZeroMatches()
    {
        string baselinePath = Path.Combine(Path.GetTempPath(), $"legacy-{Guid.NewGuid():N}.yml");
        string outputPath = Path.Combine(Path.GetTempPath(), $"migrated-{Guid.NewGuid():N}.yml");
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
");

            var (exitCode, stdout, stderr) = RunCli("baseline", "migrate",
                "--config", _passingWithIdsPolicy, "--baseline", baselinePath, "--output", outputPath);

            Assert.Multiple(() =>
            {
                Assert.That(exitCode, Is.EqualTo(0), $"Migrate should succeed, stderr: {stderr}");
                Assert.That(stdout, Does.Contain("Matched (migrated to version 2): 0"));
                Assert.That(stdout, Does.Contain("Stale (no current match, dropped): 1"));
                Assert.That(stdout, Does.Contain("Ambiguous (multiple current matches, requires manual review): 0"));
                Assert.That(File.Exists(outputPath), Is.True);
            });

            string content = File.ReadAllText(outputPath);
            Assert.That(content, Does.Contain("version: 2"));
            Assert.That(content, Does.Not.Contain("Fake.Source"));
        }
        finally
        {
            if (File.Exists(baselinePath))
                File.Delete(baselinePath);
            if (File.Exists(outputPath))
                File.Delete(outputPath);
        }
    }

    [Test]
    public void BaselineMigrate_DryRun_NeverWritesOutputFile()
    {
        string baselinePath = Path.Combine(Path.GetTempPath(), $"legacy-{Guid.NewGuid():N}.yml");
        string outputPath = Path.Combine(Path.GetTempPath(), $"migrated-{Guid.NewGuid():N}.yml");
        try
        {
            File.WriteAllText(baselinePath, "version: 1\nbaseline: {}\n");

            var (exitCode, stdout, stderr) = RunCli("baseline", "migrate",
                "--config", _passingPolicy, "--baseline", baselinePath, "--output", outputPath, "--dry-run");

            Assert.Multiple(() =>
            {
                Assert.That(exitCode, Is.EqualTo(0), $"Dry run on an empty baseline should succeed, stderr: {stderr}");
                Assert.That(stdout, Does.Contain("Dry run"));
                Assert.That(File.Exists(outputPath), Is.False, "Dry run must never write the output file");
            });
        }
        finally
        {
            if (File.Exists(baselinePath))
                File.Delete(baselinePath);
            if (File.Exists(outputPath))
                File.Delete(outputPath);
        }
    }

    [Test]
    public void BaselineMigrate_OutputEqualsBaseline_RefusesToOverwriteSource()
    {
        string baselinePath = Path.Combine(Path.GetTempPath(), $"legacy-{Guid.NewGuid():N}.yml");
        try
        {
            File.WriteAllText(baselinePath, "version: 1\nbaseline: {}\n");

            var (exitCode, _, stderr) = RunCli("baseline", "migrate",
                "--config", _passingPolicy, "--baseline", baselinePath, "--output", baselinePath);

            Assert.Multiple(() =>
            {
                Assert.That(exitCode, Is.EqualTo(2));
                Assert.That(stderr, Does.Contain("never overwrites the source file"));
            });
        }
        finally
        {
            if (File.Exists(baselinePath))
                File.Delete(baselinePath);
        }
    }

    [Test]
    public void BaselineMigrate_MissingOutputWithoutDryRun_ExitsTwo()
    {
        string baselinePath = Path.Combine(Path.GetTempPath(), $"legacy-{Guid.NewGuid():N}.yml");
        try
        {
            File.WriteAllText(baselinePath, "version: 1\nbaseline: {}\n");

            var (exitCode, _, stderr) = RunCli("baseline", "migrate",
                "--config", _passingPolicy, "--baseline", baselinePath);

            Assert.Multiple(() =>
            {
                Assert.That(exitCode, Is.EqualTo(2));
                Assert.That(stderr, Does.Contain("--output is required"));
            });
        }
        finally
        {
            if (File.Exists(baselinePath))
                File.Delete(baselinePath);
        }
    }

    [Test]
    public void BaselineMigrate_AlreadyVersion2Baseline_Refuses()
    {
        string baselinePath = Path.Combine(Path.GetTempPath(), $"v2-{Guid.NewGuid():N}.yml");
        string outputPath = Path.Combine(Path.GetTempPath(), $"migrated-{Guid.NewGuid():N}.yml");
        try
        {
            var (genExit, _, genStderr) = RunCli("baseline", "generate",
                "--config", _passingPolicy, "--output", baselinePath);
            Assert.That(genExit, Is.EqualTo(0), $"Baseline generation should succeed, stderr: {genStderr}");

            var (exitCode, _, stderr) = RunCli("baseline", "migrate",
                "--config", _passingPolicy, "--baseline", baselinePath, "--output", outputPath);

            Assert.Multiple(() =>
            {
                Assert.That(exitCode, Is.EqualTo(2));
                Assert.That(stderr, Does.Contain("already version 2"));
            });
        }
        finally
        {
            if (File.Exists(baselinePath))
                File.Delete(baselinePath);
            if (File.Exists(outputPath))
                File.Delete(outputPath);
        }
    }

    [Test]
    public void BaselineMigrate_MissingBaselineFile_ExitsTwo()
    {
        var (exitCode, _, stderr) = RunCli("baseline", "migrate",
            "--config", _passingPolicy, "--baseline", "/nonexistent/baseline.yml", "--output", "/tmp/out.yml");

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(2));
            Assert.That(stderr, Does.Contain("not found"));
        });
    }

    [Test]
    public void BaselineMigrateHelp_ShowsUsage()
    {
        var (exitCode, stdout, _) = RunCli("baseline", "migrate", "--help");

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(0));
            Assert.That(stdout, Does.Contain("baseline migrate"));
            Assert.That(stdout, Does.Contain("--dry-run"));
            Assert.That(stdout, Does.Contain("--output"));
        });
    }
}
