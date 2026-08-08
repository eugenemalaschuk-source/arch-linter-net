using System.Text.Json;
using NUnit.Framework;

namespace ArchLinterNet.Cli.Tests;

public partial class CliIntegrationTests
{
    [Test]
    public void PolicyCheck_ValidPolicy_ReportsValidStaticConfiguration()
    {
        var (exitCode, stdout, stderr) = RunCli("policy", "check", "--policy", _passingPolicy, "--format", "json");

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(0));
            Assert.That(stdout, Does.Contain("valid-with-deferred-checks"));
            Assert.That(stderr, Is.Empty);
        });
    }

    [Test]
    public void PolicyCheck_AllIssueCoverageScopes_ReportsValidStaticConfiguration()
    {
        var (exitCode, stdout, stderr) = RunCli(
            "policy", "check", "--policy", _allCoverageScopesPolicy, "--format", "json");

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(0));
            Assert.That(stdout, Does.Contain("valid-with-deferred-checks"));
            Assert.That(stderr, Is.Empty);
        });
    }

    [Test]
    public void PolicyCheck_MissingPolicy_ExitsTwo()
    {
        var (exitCode, _, stderr) = RunCli("policy", "check", "--policy", "missing-policy.yml");

        Assert.That(exitCode, Is.EqualTo(2));
        Assert.Multiple(() =>
        {
            Assert.That(stderr, Does.Contain("Policy check error"));
            Assert.That(stderr, Does.Contain("policy:"));
            Assert.That(stderr, Does.Contain("Import chain:"));
        });
    }

    [Test]
    public void PolicyCheck_MissingPolicyAsJson_PreservesFailureMessageAndProvenance()
    {
        var (exitCode, stdout, stderr) = RunCli("policy", "check", "--policy", "missing-policy.yml", "--format", "json");

        using JsonDocument document = JsonDocument.Parse(stdout);
        JsonElement failure = document.RootElement.GetProperty("failure");
        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(2));
            Assert.That(failure.GetProperty("message").GetString(), Does.Contain("not found"));
            Assert.That(failure.GetProperty("policy_location").ValueKind, Is.EqualTo(JsonValueKind.Object));
            Assert.That(stderr, Is.Empty);
        });
    }

    [Test]
    public void PolicyCheck_MissingPolicyAsSarif_WritesParseableSarifToStdout()
    {
        var (exitCode, stdout, stderr) = RunCli("policy", "check", "--policy", "missing-policy.yml", "--format", "sarif");

        using JsonDocument document = JsonDocument.Parse(stdout);
        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(2));
            Assert.That(document.RootElement.GetProperty("version").GetString(), Is.EqualTo("2.1.0"));
            Assert.That(document.RootElement.GetProperty("runs")[0].GetProperty("results").GetArrayLength(), Is.EqualTo(1));
            Assert.That(document.RootElement.GetProperty("runs")[0].GetProperty("results")[0]
                .GetProperty("locations").GetArrayLength(), Is.EqualTo(1));
            Assert.That(stderr, Is.Empty);
        });
    }

    [Test]
    public void PolicyCheck_DeferredSarifResult_HasPrimaryPolicyLocation()
    {
        var (exitCode, stdout, stderr) = RunCli("policy", "check", "--policy", _passingPolicy, "--format", "sarif");

        using JsonDocument document = JsonDocument.Parse(stdout);
        JsonElement result = document.RootElement.GetProperty("runs")[0].GetProperty("results")[0];
        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(0));
            Assert.That(result.GetProperty("ruleId").GetString(), Is.EqualTo("architecture-policy-deferred"));
            Assert.That(result.GetProperty("locations").GetArrayLength(), Is.EqualTo(1));
            Assert.That(stderr, Is.Empty);
        });
    }

    [Test]
    public void PolicyCheck_MalformedImportedFragmentAsSarif_WritesParseableSarifToStdout()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"arch-linter-policy-check-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        string root = Path.Combine(directory, "root.yml");
        try
        {
            File.WriteAllText(root, "version: 1\nname: Import Test\nimports: [fragment.yml]\n");
            File.WriteAllText(Path.Combine(directory, "fragment.yml"), "layers: []\n");

            var (exitCode, stdout, stderr) = RunCli("policy", "check", "--policy", root, "--format", "sarif");

            using JsonDocument document = JsonDocument.Parse(stdout);
            Assert.Multiple(() =>
            {
                Assert.That(exitCode, Is.EqualTo(2));
                Assert.That(document.RootElement.GetProperty("runs")[0].GetProperty("results").GetArrayLength(), Is.EqualTo(1));
                Assert.That(stderr, Is.Empty);
            });
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
