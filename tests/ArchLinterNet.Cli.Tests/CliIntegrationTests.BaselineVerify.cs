using NUnit.Framework;

namespace ArchLinterNet.Cli.Tests;

public partial class CliIntegrationTests
{
    /* baseline verify */

    [Test]
    public void BaselineVerify_InSyncBaseline_ExitsZero()
    {
        string baselinePath = Path.Combine(Path.GetTempPath(), $"baseline-{Guid.NewGuid():N}.yml");
        try
        {
            var (genExit, _, genStderr) = RunCli("baseline", "generate",
                "--config", _graphPolicy, "--output", baselinePath);
            Assert.That(genExit, Is.EqualTo(0), $"Baseline generation should succeed, stderr: {genStderr}");

            var (exitCode, stdout, stderr) = RunCli("baseline", "verify",
                "--config", _graphPolicy, "--baseline", baselinePath);

            Assert.Multiple(() =>
            {
                Assert.That(exitCode, Is.EqualTo(0), $"Verify should pass, stderr: {stderr}");
                Assert.That(stdout, Does.Contain("Baseline is in sync."));
            });
        }
        finally
        {
            if (File.Exists(baselinePath))
                File.Delete(baselinePath);
        }
    }

    [Test]
    public void BaselineVerify_ResolvedEntry_ExitsOne()
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
");

            var (exitCode, stdout, stderr) = RunCli("baseline", "verify",
                "--config", _passingWithIdsPolicy, "--baseline", baselinePath);

            Assert.Multiple(() =>
            {
                Assert.That(exitCode, Is.EqualTo(1), $"Verify should fail on stale entry, stderr: {stderr}");
                Assert.That(stdout, Does.Contain("Baseline is out of sync."));
                Assert.That(stdout, Does.Contain("Resolved baseline entries (debt fixed): 1"));
            });
        }
        finally
        {
            if (File.Exists(baselinePath))
                File.Delete(baselinePath);
        }
    }

    [Test]
    public void BaselineVerify_NewEntries_ReportOutOfSyncInHumanAndJson()
    {
        string baselinePath = Path.Combine(Path.GetTempPath(), $"baseline-{Guid.NewGuid():N}.yml");
        try
        {
            File.WriteAllText(baselinePath, "version: 2\nbaseline: {}\n");

            var (humanExit, humanStdout, humanStderr) = RunCli("baseline", "verify",
                "--config", _graphPolicy, "--baseline", baselinePath);
            var (jsonExit, jsonStdout, jsonStderr) = RunCli("baseline", "verify",
                "--config", _graphPolicy, "--baseline", baselinePath, "--json");

            using var json = System.Text.Json.JsonDocument.Parse(jsonStdout);
            Assert.Multiple(() =>
            {
                Assert.That(humanExit, Is.EqualTo(1), $"Verify should fail on new debt, stderr: {humanStderr}");
                Assert.That(humanStdout, Does.Match(@"New \(unbaselined\) violations: [1-9][0-9]*"));
                Assert.That(humanStdout, Does.Contain("Baseline is out of sync."));
                Assert.That(jsonExit, Is.EqualTo(1), $"JSON verify should fail on new debt, stderr: {jsonStderr}");
                Assert.That(json.RootElement.GetProperty("inSync").GetBoolean(), Is.False);
                Assert.That(json.RootElement.GetProperty("counts").GetProperty("new").GetInt32(), Is.GreaterThan(0));
            });
        }
        finally
        {
            if (File.Exists(baselinePath))
                File.Delete(baselinePath);
        }
    }

    [Test]
    public void BaselineVerify_ConfigurationError_ExitsOne()
    {
        string baselinePath = Path.Combine(Path.GetTempPath(), $"baseline-{Guid.NewGuid():N}.yml");
        try
        {
            File.WriteAllText(baselinePath, @"
version: 1
baseline:
  strict:
    - id: totally-unknown-id
      ignored_violations:
        - source_type: Fake.Source
          forbidden_reference: Fake.Target
          reason: bad id
");

            var (exitCode, stdout, stderr) = RunCli("baseline", "verify",
                "--config", _passingWithIdsPolicy, "--baseline", baselinePath);

            Assert.Multiple(() =>
            {
                Assert.That(exitCode, Is.EqualTo(1), $"Verify should fail on unknown contract id, stderr: {stderr}");
                Assert.That(stdout, Does.Contain("Stale baseline entries (contract no longer valid): 1"));
            });
        }
        finally
        {
            if (File.Exists(baselinePath))
                File.Delete(baselinePath);
        }
    }

    [Test]
    [CancelAfter(120_000)]
    public void BaselineVerify_Json_ProducesValidJsonWithInSyncField()
    {
        string baselinePath = Path.Combine(Path.GetTempPath(), $"baseline-{Guid.NewGuid():N}.yml");
        try
        {
            var (genExit, _, _) = RunCli("baseline", "generate",
                "--config", _graphPolicy, "--output", baselinePath);
            Assert.That(genExit, Is.EqualTo(0));

            var (exitCode, stdout, stderr) = RunCli("baseline", "verify",
                "--config", _graphPolicy, "--baseline", baselinePath, "--json");

            Assert.That(exitCode, Is.EqualTo(0), $"Verify should pass, stderr: {stderr}");

            using var doc = System.Text.Json.JsonDocument.Parse(stdout);
            Assert.That(doc.RootElement.GetProperty("inSync").GetBoolean(), Is.True);
        }
        finally
        {
            if (File.Exists(baselinePath))
                File.Delete(baselinePath);
        }
    }

    [Test]
    public void BaselineVerify_UnknownConditionSet_ExitsTwo()
    {
        string baselinePath = Path.Combine(Path.GetTempPath(), $"baseline-{Guid.NewGuid():N}.yml");
        try
        {
            File.WriteAllText(baselinePath, "version: 1\nbaseline: {}\n");

            var (exitCode, _, stderr) = RunCli("baseline", "verify",
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
    public void BaselineVerify_WithUnknownContract_ExitsTwo()
    {
        string baselinePath = Path.Combine(Path.GetTempPath(), $"baseline-{Guid.NewGuid():N}.yml");
        try
        {
            var (genExit, _, genStderr) = RunCli("baseline", "generate",
                "--config", _graphPolicy, "--output", baselinePath);
            Assert.That(genExit, Is.EqualTo(0), $"Baseline generation should succeed, stderr: {genStderr}");

            var (exitCode, _, stderr) = RunCli("baseline", "verify",
                "--config", _graphPolicy, "--baseline", baselinePath, "--contract", "missing-contract-id");

            Assert.Multiple(() =>
            {
                Assert.That(exitCode, Is.EqualTo(2));
                Assert.That(stderr, Does.Contain("Unknown contract IDs"));
                Assert.That(stderr, Does.Contain("missing-contract-id"));
            });
        }
        finally
        {
            if (File.Exists(baselinePath))
                File.Delete(baselinePath);
        }
    }

    [Test]
    public void BaselineVerify_WithPolicyAlias_ExitsZeroForInSyncBaseline()
    {
        string baselinePath = Path.Combine(Path.GetTempPath(), $"baseline-{Guid.NewGuid():N}.yml");
        try
        {
            var (genExit, _, genStderr) = RunCli("baseline", "generate",
                "--config", _graphPolicy, "--output", baselinePath);
            Assert.That(genExit, Is.EqualTo(0), $"Baseline generation should succeed, stderr: {genStderr}");

            var (exitCode, stdout, stderr) = RunCli("baseline", "verify",
                "--policy", _graphPolicy, "--baseline", baselinePath);

            Assert.Multiple(() =>
            {
                Assert.That(exitCode, Is.EqualTo(0), $"Verify should pass, stderr: {stderr}");
                Assert.That(stdout, Does.Contain("Baseline is in sync."));
            });
        }
        finally
        {
            if (File.Exists(baselinePath))
                File.Delete(baselinePath);
        }
    }

    [Test]
    public void BaselineVerify_MissingBaselineFile_ExitsTwo()
    {
        var (exitCode, _, stderr) = RunCli("baseline", "verify",
            "--config", _passingPolicy, "--baseline", "/nonexistent/baseline.yml");

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(2));
            Assert.That(stderr, Does.Contain("not found"));
        });
    }

    [Test]
    public void BaselineVerifyHelp_ShowsUsage()
    {
        var (exitCode, stdout, _) = RunCli("baseline", "verify", "--help");

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(0));
            Assert.That(stdout, Does.Contain("baseline verify"));
            Assert.That(stdout, Does.Contain("--baseline"));
        });
    }
}
