using NUnit.Framework;

namespace ArchLinterNet.Cli.Tests;

public partial class CliIntegrationTests
{
    /* validate --baseline */

    [Test]
    public void ValidateWithBaseline_EmptyBaseline_StillPasses()
    {
        string baselinePath = Path.Combine(Path.GetTempPath(), $"baseline-{Guid.NewGuid():N}.yml");
        try
        {
            var (genExit, _, _) = RunCli("baseline", "generate",
                "--config", _passingPolicy,
                "--output", baselinePath);
            Assert.That(genExit, Is.EqualTo(0), "Baseline generation should succeed");

            var (exitCode, _, stderr) = RunCli("--policy", _passingPolicy, "--strict",
                "--baseline", baselinePath);

            Assert.Multiple(() =>
            {
                Assert.That(exitCode, Is.EqualTo(0),
                    $"Validate with baseline should pass, stderr: {stderr}");
            });
        }
        finally
        {
            if (File.Exists(baselinePath))
                File.Delete(baselinePath);
        }
    }

    [Test]
    public void ValidateWithBaseline_UnknownContractId_ExitsTwo()
    {
        string baselinePath = Path.Combine(Path.GetTempPath(), $"bad-baseline-{Guid.NewGuid():N}.yml");
        try
        {
            File.WriteAllText(baselinePath, @"
version: 1
baseline:
  strict:
    - id: nonexistent-contract
      ignored_violations:
        - source_type: Some.Type
          forbidden_reference: Bad.Type
          reason: stale
");

            var (exitCode, _, stderr) = RunCli("--policy", _passingPolicy, "--strict",
                "--baseline", baselinePath);

            Assert.Multiple(() =>
            {
                Assert.That(exitCode, Is.EqualTo(2));
                Assert.That(stderr, Does.Contain("unknown contract"));
                Assert.That(stderr, Does.Contain("nonexistent-contract"));
            });
        }
        finally
        {
            if (File.Exists(baselinePath))
                File.Delete(baselinePath);
        }
    }

    [Test]
    public void ValidateWithBaseline_MissingBaselineFile_ExitsTwo()
    {
        var (exitCode, _, stderr) = RunCli("--policy", _passingPolicy, "--strict",
            "--baseline", "/nonexistent/baseline.yml");

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(2));
            Assert.That(stderr, Does.Contain("not found"));
        });
    }
}
