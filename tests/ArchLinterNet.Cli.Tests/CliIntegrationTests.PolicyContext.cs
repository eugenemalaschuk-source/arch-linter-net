using System.Text.Json;
using NUnit.Framework;

namespace ArchLinterNet.Cli.Tests;

public partial class CliIntegrationTests
{
    [Test]
    public void PolicyContext_Json_ExportsOneVersionedToolDocument()
    {
        var (exitCode, stdout, stderr) = RunCli("policy", "context", "--policy", _passingPolicy, "--format", "json");

        using JsonDocument document = JsonDocument.Parse(stdout);
        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(0));
            Assert.That(stderr, Is.Empty);
            Assert.That(document.RootElement.GetProperty("schema_version").GetInt32(), Is.EqualTo(1));
            Assert.That(document.RootElement.GetProperty("kind").GetString(), Is.EqualTo("architecture-policy-context"));
            Assert.That(document.RootElement.GetProperty("contracts")[0].GetProperty("id").GetString(),
                Is.EqualTo("core-no-forbidden"));
        });
    }

    [Test]
    public void PolicyContext_DefaultMarkdown_DescribesEffectivePolicyWithoutClaimingValidation()
    {
        var (exitCode, stdout, stderr) = RunCli("policy", "context", "--policy", _passingPolicy);

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(0));
            Assert.That(stderr, Is.Empty);
            Assert.That(stdout, Does.StartWith("# Architecture policy context"));
            Assert.That(stdout, Does.Contain("does not build projects, analyze assemblies, or prove architecture compliance"));
            Assert.That(stdout, Does.Contain("core-no-forbidden"));
        });
    }

    [Test]
    public void PolicyContext_Help_StatesTheNoAnalysisBoundary()
    {
        var (exitCode, stdout, stderr) = RunCli("policy", "context", "--help");

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(0));
            Assert.That(stderr, Is.Empty);
            Assert.That(stdout, Does.Contain("does not build projects, load target assemblies, or validate architecture results"));
        });
    }

    [Test]
    public void PolicyContext_MissingPolicyAsJson_PreservesStructuredFailureOutput()
    {
        var (exitCode, stdout, stderr) = RunCli("policy", "context", "--policy", "missing-policy.yml", "--format", "json");

        using JsonDocument document = JsonDocument.Parse(stdout);
        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(2));
            Assert.That(stderr, Is.Empty);
            Assert.That(document.RootElement.TryGetProperty("policy_location", out _), Is.True);
        });
    }
}
