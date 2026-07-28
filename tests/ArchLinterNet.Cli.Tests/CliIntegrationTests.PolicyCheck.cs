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
            Assert.That(stdout, Does.Contain("\"valid\""));
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
