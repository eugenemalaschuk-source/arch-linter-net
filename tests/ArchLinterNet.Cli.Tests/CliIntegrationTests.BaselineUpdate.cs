using NUnit.Framework;

namespace ArchLinterNet.Cli.Tests;

public partial class CliIntegrationTests
{
    /* baseline update */

    [Test]
    public void BaselineUpdate_UnchangedCodebase_RoundTripsIdentically()
    {
        string baselinePath = Path.Combine(Path.GetTempPath(), $"baseline-{Guid.NewGuid():N}.yml");
        string updatedPath = Path.Combine(Path.GetTempPath(), $"updated-{Guid.NewGuid():N}.yml");
        try
        {
            var (genExit, _, genStderr) = RunCli("baseline", "generate",
                "--config", _graphPolicy, "--output", baselinePath);
            Assert.That(genExit, Is.EqualTo(0), $"Baseline generation should succeed, stderr: {genStderr}");

            var (exitCode, stdout, stderr) = RunCli("baseline", "update",
                "--config", _graphPolicy, "--baseline", baselinePath, "--output", updatedPath);

            Assert.Multiple(() =>
            {
                Assert.That(exitCode, Is.EqualTo(0), $"Update should succeed, stderr: {stderr}");
                Assert.That(stdout, Does.Contain("added 0 new entries"));
            });

            Assert.That(File.ReadAllText(updatedPath), Is.EqualTo(File.ReadAllText(baselinePath)));
        }
        finally
        {
            if (File.Exists(baselinePath))
                File.Delete(baselinePath);
            if (File.Exists(updatedPath))
                File.Delete(updatedPath);
        }
    }

    [Test]
    public void BaselineUpdate_PreservesCustomReasonOnStillValidEntries()
    {
        string baselinePath = Path.Combine(Path.GetTempPath(), $"baseline-{Guid.NewGuid():N}.yml");
        string updatedPath = Path.Combine(Path.GetTempPath(), $"updated-{Guid.NewGuid():N}.yml");
        try
        {
            var (genExit, genStdout, _) = RunCli("baseline", "generate",
                "--config", _graphPolicy, "--output", baselinePath);
            Assert.That(genExit, Is.EqualTo(0));

            string generated = File.ReadAllText(baselinePath);
            string withCustomReason = generated.Replace("reason: generated baseline", "reason: my custom reason");
            File.WriteAllText(baselinePath, withCustomReason);

            var (exitCode, _, stderr) = RunCli("baseline", "update",
                "--config", _graphPolicy, "--baseline", baselinePath, "--output", updatedPath);

            Assert.That(exitCode, Is.EqualTo(0), $"Update should succeed, stderr: {stderr}");
            Assert.That(File.ReadAllText(updatedPath), Does.Contain("reason: my custom reason"));
        }
        finally
        {
            if (File.Exists(baselinePath))
                File.Delete(baselinePath);
            if (File.Exists(updatedPath))
                File.Delete(updatedPath);
        }
    }

    [Test]
    public void BaselineUpdate_AddsNewEntriesWithReasonOverride()
    {
        string baselinePath = Path.Combine(Path.GetTempPath(), $"baseline-{Guid.NewGuid():N}.yml");
        string updatedPath = Path.Combine(Path.GetTempPath(), $"updated-{Guid.NewGuid():N}.yml");
        try
        {
            File.WriteAllText(baselinePath, "version: 1\nbaseline: {}\n");

            var (exitCode, stdout, stderr) = RunCli("baseline", "update",
                "--config", _graphPolicy, "--baseline", baselinePath, "--output", updatedPath,
                "--reason", "custom update reason");

            Assert.Multiple(() =>
            {
                Assert.That(exitCode, Is.EqualTo(0), $"Update should succeed, stderr: {stderr}");
                Assert.That(stdout, Does.Contain("preserved 0"));
            });

            string updated = File.ReadAllText(updatedPath);
            Assert.That(updated, Does.Contain("no-execution-to-contracts"));
            Assert.That(updated, Does.Contain("reason: custom update reason"));
        }
        finally
        {
            if (File.Exists(baselinePath))
                File.Delete(baselinePath);
            if (File.Exists(updatedPath))
                File.Delete(updatedPath);
        }
    }

    [Test]
    public void BaselineUpdate_MissingBaselineFlag_ExitsTwo()
    {
        var (exitCode, _, stderr) = RunCli("baseline", "update",
            "--config", _passingPolicy, "--output", "out.yml");

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(2));
            Assert.That(stderr, Does.Contain("--baseline"));
        });
    }

    [Test]
    public void BaselineUpdate_MissingOutputFlag_PreviewsToStdout()
    {
        string baselinePath = Path.Combine(Path.GetTempPath(), $"baseline-{Guid.NewGuid():N}.yml");
        try
        {
            File.WriteAllText(baselinePath, "version: 1\nbaseline: {}\n");

            var (exitCode, stdout, stderr) = RunCli("baseline", "update",
                "--config", _passingPolicy, "--baseline", baselinePath);

            Assert.Multiple(() =>
            {
                Assert.That(exitCode, Is.EqualTo(0), $"Baseline update failed, stderr: {stderr}");
                Assert.That(stdout, Does.Contain("version: 1"));
                Assert.That(File.ReadAllText(baselinePath), Is.EqualTo("version: 1\nbaseline: {}\n"));
            });
        }
        finally
        {
            if (File.Exists(baselinePath))
                File.Delete(baselinePath);
        }
    }

    [Test]
    public void BaselineUpdate_PreservesLeadingCommentHeader()
    {
        string baselinePath = Path.Combine(Path.GetTempPath(), $"baseline-{Guid.NewGuid():N}.yml");
        try
        {
            File.WriteAllText(baselinePath, "# Reviewed baseline — owned by platform\n# Tracked in #123\nversion: 2\nbaseline: {}\n");

            var (exitCode, _, stderr) = RunCli("baseline", "update",
                "--config", _passingPolicy, "--baseline", baselinePath, "--output", baselinePath);

            string updated = File.ReadAllText(baselinePath);
            Assert.Multiple(() =>
            {
                Assert.That(exitCode, Is.EqualTo(0), $"Baseline update failed, stderr: {stderr}");
                Assert.That(updated, Does.Contain("# Reviewed baseline — owned by platform"));
                Assert.That(updated, Does.Contain("# Tracked in #123"));
                Assert.That(updated, Does.Contain("version: 2"));
            });
        }
        finally
        {
            if (File.Exists(baselinePath))
                File.Delete(baselinePath);
        }
    }

    [Test]
    public void BaselineUpdate_InteriorComments_RefusesAndReportsLines()
    {
        string baselinePath = Path.Combine(Path.GetTempPath(), $"baseline-{Guid.NewGuid():N}.yml");
        const string original = "version: 2\n# a note about the entry below\nbaseline: {}\n";
        try
        {
            File.WriteAllText(baselinePath, original);

            var (exitCode, _, stderr) = RunCli("baseline", "update",
                "--config", _passingPolicy, "--baseline", baselinePath, "--output", baselinePath);

            Assert.Multiple(() =>
            {
                Assert.That(exitCode, Is.EqualTo(2));
                Assert.That(stderr, Does.Contain("line(s) 2"));
                Assert.That(stderr, Does.Contain("--dry-run"));
                Assert.That(File.ReadAllText(baselinePath), Is.EqualTo(original));
            });

            var (dryRunExit, dryRunStdout, dryRunStderr) = RunCli("baseline", "update",
                "--config", _passingPolicy, "--baseline", baselinePath, "--output", baselinePath, "--dry-run");

            Assert.Multiple(() =>
            {
                Assert.That(dryRunExit, Is.EqualTo(0), $"Dry run failed, stderr: {dryRunStderr}");
                Assert.That(dryRunStdout, Does.Contain("version: 2"));
                Assert.That(File.ReadAllText(baselinePath), Is.EqualTo(original));
            });
        }
        finally
        {
            if (File.Exists(baselinePath))
                File.Delete(baselinePath);
        }
    }

    [Test]
    public void BaselineUpdateHelp_ShowsUsage()
    {
        var (exitCode, stdout, _) = RunCli("baseline", "update", "--help");

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(0));
            Assert.That(stdout, Does.Contain("baseline update"));
            Assert.That(stdout, Does.Contain("--baseline"));
            Assert.That(stdout, Does.Contain("--reason"));
        });
    }
}
