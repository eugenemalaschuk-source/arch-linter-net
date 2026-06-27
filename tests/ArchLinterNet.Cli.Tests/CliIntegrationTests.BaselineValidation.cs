using NUnit.Framework;

namespace ArchLinterNet.Cli.Tests;

public partial class CliIntegrationTests
{
    /* validate --baseline (coverage contracts) */

    [Test]
    public void ValidateWithBaseline_CoveragePolicy_SuppressesBaselinedUncoveredNamespace()
    {
        string baselinePath = Path.Combine(Path.GetTempPath(), $"baseline-{Guid.NewGuid():N}.yml");
        try
        {
            var (genExit, genStdout, genStderr) = RunCli("baseline", "generate",
                "--config", _coveragePolicy,
                "--output", baselinePath);
            Assert.That(genExit, Is.EqualTo(0), $"Baseline generation should succeed, stderr: {genStderr}");
            Assert.That(File.ReadAllText(baselinePath), Does.Contain("strict_coverage:"),
                $"Expected coverage entries in generated baseline, stdout: {genStdout}");

            var (exitCode, _, stderr) = RunCli("--policy", _coveragePolicy, "--strict",
                "--baseline", baselinePath);

            Assert.That(exitCode, Is.EqualTo(0),
                $"Validate with coverage baseline should pass, stderr: {stderr}");
        }
        finally
        {
            if (File.Exists(baselinePath))
                File.Delete(baselinePath);
        }
    }

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
