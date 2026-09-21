using NUnit.Framework;

namespace ArchLinterNet.Cli.Tests;

[TestFixture]
internal sealed class CliProfilePublicationCollisionTests : CliIntegrationTestBase
{
    [Test]
    public void Gate_ProfileCannotOverwriteAnImportedPolicyConsumedDuringAnalysis()
    {
        string baselinePath = Path.Combine(Path.GetTempPath(), $"gate-profile-collision-{Guid.NewGuid():N}.yml");
        string importedPolicyPath = Path.Combine(Path.GetDirectoryName(GraphPolicy)!, "graph-policy-fragment.yml");
        string originalContents = File.ReadAllText(importedPolicyPath);
        try
        {
            var (baselineExit, _, baselineError) = RunCli(
                "baseline", "generate", "--policy", GraphPolicy, "--output", baselinePath);
            Assert.That(baselineExit, Is.EqualTo(0), $"stderr: {baselineError}");

            var (exitCode, _, error) = RunCli(
                "gate", "--policy", GraphPolicy, "--baseline", baselinePath, "--profile", importedPolicyPath);

            Assert.Multiple(() =>
            {
                Assert.That(exitCode, Is.EqualTo(2));
                Assert.That(error, Does.Contain("--profile destination").And.Contain("imported policy"));
                Assert.That(File.ReadAllText(importedPolicyPath), Is.EqualTo(originalContents));
            });
        }
        finally
        {
            DeleteIfPresent(baselinePath);
        }
    }

    [Test]
    public void Health_ProfileCannotOverwriteAnImportedPolicyConsumedDuringAnalysis()
    {
        string baselinePath = Path.Combine(Path.GetTempPath(), $"health-profile-collision-{Guid.NewGuid():N}.yml");
        string importedPolicyPath = Path.Combine(Path.GetDirectoryName(GraphPolicy)!, "graph-policy-fragment.yml");
        string originalContents = File.ReadAllText(importedPolicyPath);
        try
        {
            var (baselineExit, _, baselineError) = RunCli(
                "baseline", "generate", "--policy", GraphPolicy, "--output", baselinePath);
            Assert.That(baselineExit, Is.EqualTo(0), $"stderr: {baselineError}");

            var (exitCode, _, error) = RunCli(
                "health", "--policy", GraphPolicy, "--baseline", baselinePath, "--profile", importedPolicyPath);

            Assert.Multiple(() =>
            {
                Assert.That(exitCode, Is.EqualTo(2));
                Assert.That(error, Does.Contain("--profile destination").And.Contain("imported policy"));
                Assert.That(File.ReadAllText(importedPolicyPath), Is.EqualTo(originalContents));
            });
        }
        finally
        {
            DeleteIfPresent(baselinePath);
        }
    }

    [Test]
    public void Measure_ProfileCannotOverwriteAnImportedPolicyConsumedDuringAnalysis()
    {
        string policyPath = Path.Combine(
            RepoRoot, "tests", "ArchLinterNet.Cli.Tests", "TestPolicies", "imported-metrics-root.yml");
        string importedPolicyPath = Path.Combine(Path.GetDirectoryName(policyPath)!, "imported-metrics-fragment.yml");
        string originalContents = File.ReadAllText(importedPolicyPath);

        var (exitCode, _, error) = RunCli(
            "measure", "--policy", policyPath, "--profile", importedPolicyPath);

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(2));
            Assert.That(error, Does.Contain("--profile destination").And.Contain("imported policy"));
            Assert.That(File.ReadAllText(importedPolicyPath), Is.EqualTo(originalContents));
        });
    }
}
