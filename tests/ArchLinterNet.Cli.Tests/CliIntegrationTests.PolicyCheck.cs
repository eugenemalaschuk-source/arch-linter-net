using NUnit.Framework;

namespace ArchLinterNet.Cli.Tests;

public partial class CliIntegrationTests
{
    [Test]
    public void PolicyCheck_ValidPolicy_ReportsDeferredArchitectureEvaluation()
    {
        var (exitCode, stdout, stderr) = RunCli("policy", "check", "--policy", _passingPolicy, "--format", "json");

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(0));
            Assert.That(stdout, Does.Contain("valid-with-deferred-checks"));
            Assert.That(stdout, Does.Contain("architecture-evaluation"));
            Assert.That(stderr, Is.Empty);
        });
    }

    [Test]
    public void PolicyCheck_MissingPolicy_ExitsTwo()
    {
        var (exitCode, _, stderr) = RunCli("policy", "check", "--policy", "missing-policy.yml");

        Assert.That(exitCode, Is.EqualTo(2));
        Assert.That(stderr, Does.Contain("Policy check error"));
    }
}
