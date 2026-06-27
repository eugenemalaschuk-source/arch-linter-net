using System.Text.Json;
using NUnit.Framework;

namespace ArchLinterNet.Cli.Tests;

public partial class CliIntegrationTests
{
    [Test]
    public void DuplicateContractId_DefaultsToError_ExitsOneAndReportsFinding()
    {
        string policy = Path.Combine(
            _repoRoot, "tests", "ArchLinterNet.Cli.Tests", "TestPolicies", "policy-consistency-duplicate-id.yml");
        var (exitCode, stdout, _) = RunCli("--policy", policy, "--strict");

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(1));
            Assert.That(stdout, Does.Contain("duplicate-id"));
        });
    }

    [Test]
    public void DuplicateContractId_JsonOutput_IncludesPolicyConsistencyFindings()
    {
        string policy = Path.Combine(
            _repoRoot, "tests", "ArchLinterNet.Cli.Tests", "TestPolicies", "policy-consistency-duplicate-id.yml");
        var (exitCode, stdout, _) = RunCli("--policy", policy, "--strict", "--json");

        Assert.That(exitCode, Is.EqualTo(1));

        using JsonDocument document = JsonDocument.Parse(stdout);
        JsonElement findings = document.RootElement.GetProperty("policy_consistency_findings");

        Assert.That(findings.GetArrayLength(), Is.GreaterThan(0));
        Assert.That(findings[0].GetProperty("check_kind").GetString(), Is.EqualTo("duplicate-id"));
    }

    [Test]
    public void AllowForbidConflict_WithWarnSeverity_ExitsZeroButReportsFinding()
    {
        string policy = Path.Combine(
            _repoRoot, "tests", "ArchLinterNet.Cli.Tests", "TestPolicies", "policy-consistency-allow-forbid-conflict.yml");
        var (exitCode, stdout, _) = RunCli("--policy", policy, "--strict");

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(0));
            Assert.That(stdout, Does.Contain("allow-forbid-conflict"));
        });
    }

    [Test]
    public void InvalidPolicyConsistencyValue_ExitsWithError()
    {
        string policy = Path.Combine(
            _repoRoot, "tests", "ArchLinterNet.Cli.Tests", "TestPolicies", "invalid-policy-consistency-config.yml");
        var (exitCode, _, stderr) = RunCli("--policy", policy, "--strict");

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(2));
            Assert.That(stderr, Does.Contain("policy_consistency"));
        });
    }
}
