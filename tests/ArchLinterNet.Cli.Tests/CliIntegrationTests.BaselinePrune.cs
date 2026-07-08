using System.Text.Json;
using NUnit.Framework;

namespace ArchLinterNet.Cli.Tests;

public partial class CliIntegrationTests
{
    /* baseline prune */

    [Test]
    public void BaselinePrune_ResolvedAndUnknownContractId_RemovesBothAndReportsThem()
    {
        string baselinePath = Path.Combine(Path.GetTempPath(), $"baseline-{Guid.NewGuid():N}.yml");
        string prunedPath = Path.Combine(Path.GetTempPath(), $"pruned-{Guid.NewGuid():N}.yml");
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

            var (exitCode, stdout, stderr) = RunCli("baseline", "prune",
                "--config", _passingWithIdsPolicy, "--baseline", baselinePath, "--output", prunedPath, "--json");

            Assert.That(exitCode, Is.EqualTo(0), $"Prune should succeed, stderr: {stderr}");

            using var doc = JsonDocument.Parse(stdout);
            JsonElement removed = doc.RootElement.GetProperty("removed");
            Assert.That(removed.GetArrayLength(), Is.EqualTo(2));

            string pruned = File.ReadAllText(prunedPath);
            Assert.That(pruned, Does.Not.Contain("Fake.Source"));
            Assert.That(pruned, Does.Not.Contain("totally-unknown-id"));
        }
        finally
        {
            if (File.Exists(baselinePath))
                File.Delete(baselinePath);
            if (File.Exists(prunedPath))
                File.Delete(prunedPath);
        }
    }

    [Test]
    public void BaselinePrune_NoStaleEntries_IsANoOp()
    {
        string baselinePath = Path.Combine(Path.GetTempPath(), $"baseline-{Guid.NewGuid():N}.yml");
        string prunedPath = Path.Combine(Path.GetTempPath(), $"pruned-{Guid.NewGuid():N}.yml");
        try
        {
            var (genExit, _, _) = RunCli("baseline", "generate",
                "--config", _graphPolicy, "--output", baselinePath);
            Assert.That(genExit, Is.EqualTo(0));

            var (exitCode, stdout, stderr) = RunCli("baseline", "prune",
                "--config", _graphPolicy, "--baseline", baselinePath, "--output", prunedPath);

            Assert.Multiple(() =>
            {
                Assert.That(exitCode, Is.EqualTo(0), $"Prune should succeed, stderr: {stderr}");
                Assert.That(stdout, Does.Contain("removed 0 entries"));
            });

            Assert.That(File.ReadAllText(prunedPath), Is.EqualTo(File.ReadAllText(baselinePath)));
        }
        finally
        {
            if (File.Exists(baselinePath))
                File.Delete(baselinePath);
            if (File.Exists(prunedPath))
                File.Delete(prunedPath);
        }
    }

    [Test]
    public void BaselinePrune_JsonShortFlag_IsAccepted()
    {
        string baselinePath = Path.Combine(Path.GetTempPath(), $"baseline-{Guid.NewGuid():N}.yml");
        string prunedPath = Path.Combine(Path.GetTempPath(), $"pruned-{Guid.NewGuid():N}.yml");
        try
        {
            var (genExit, _, genStderr) = RunCli("baseline", "generate",
                "--config", _graphPolicy, "--output", baselinePath);
            Assert.That(genExit, Is.EqualTo(0), $"Baseline generation should succeed, stderr: {genStderr}");

            var (exitCode, stdout, stderr) = RunCli("baseline", "prune",
                "--config", _graphPolicy, "--baseline", baselinePath, "--output", prunedPath, "-f");

            Assert.That(exitCode, Is.EqualTo(0), $"Prune should succeed, stderr: {stderr}");

            using var doc = JsonDocument.Parse(stdout);
            Assert.That(doc.RootElement.TryGetProperty("removed", out _), Is.True);
        }
        finally
        {
            if (File.Exists(baselinePath))
                File.Delete(baselinePath);
            if (File.Exists(prunedPath))
                File.Delete(prunedPath);
        }
    }

    [Test]
    public void BaselinePrune_MissingBaselineFlag_ExitsTwo()
    {
        var (exitCode, _, stderr) = RunCli("baseline", "prune",
            "--config", _passingPolicy, "--output", "out.yml");

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(2));
            Assert.That(stderr, Does.Contain("--baseline"));
        });
    }

    [Test]
    public void BaselinePruneHelp_ShowsUsage()
    {
        var (exitCode, stdout, _) = RunCli("baseline", "prune", "--help");

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(0));
            Assert.That(stdout, Does.Contain("baseline prune"));
            Assert.That(stdout, Does.Contain("--baseline"));
        });
    }
}
