using System.Text.Json;
using System.Text.Json.Nodes;
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
            Assert.That(document.RootElement.GetProperty("schema_version").GetInt32(), Is.EqualTo(3));
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

    [Test]
    public void PolicyWeakening_ExplicitContextArtifacts_ReportsErrorSeverityDowngradeAsJson()
    {
        var (contextExit, contextJson, contextError) = RunCli("policy", "context", "--policy", _passingPolicy, "--format", "json");
        JsonObject currentContext = JsonNode.Parse(contextJson)!.AsObject();
        JsonObject strictContract = currentContext["contracts"]!.AsArray()
            .Select(node => node!.AsObject())
            .First(contract => contract["mode"]!.GetValue<string>() == "strict");
        strictContract["mode"] = "audit";

        string temporaryDirectory = Path.Combine(Path.GetTempPath(), $"arch-linter-policy-weakening-{Guid.NewGuid():N}");
        Directory.CreateDirectory(temporaryDirectory);
        string basePath = Path.Combine(temporaryDirectory, "base.json");
        string currentPath = Path.Combine(temporaryDirectory, "current.json");
        try
        {
            File.WriteAllText(basePath, contextJson);
            File.WriteAllText(currentPath, currentContext.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));

            var (exitCode, stdout, stderr) = RunCli(
                "policy", "weakening", "--base-context", basePath, "--current-context", currentPath, "--format", "json");

            using JsonDocument document = JsonDocument.Parse(stdout);
            Assert.Multiple(() =>
            {
                Assert.That(contextExit, Is.EqualTo(0), contextError);
                Assert.That(exitCode, Is.EqualTo(1));
                Assert.That(stderr, Is.Empty);
                Assert.That(document.RootElement.GetProperty("findings").EnumerateArray()
                    .Any(finding => finding.GetProperty("kind").GetString() == "strict_to_audit"), Is.True);
            });
        }
        finally
        {
            Directory.Delete(temporaryDirectory, recursive: true);
        }
    }

    [Test]
    public void PolicyWeakening_IncompleteArtifact_FailsClosed()
    {
        string temporaryPath = Path.Combine(Path.GetTempPath(), $"arch-linter-policy-weakening-{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(temporaryPath, "{}");

            var (exitCode, stdout, stderr) = RunCli(
                "policy", "weakening", "--base-context", temporaryPath, "--current-context", temporaryPath, "--format", "json");

            using JsonDocument document = JsonDocument.Parse(stdout);
            Assert.Multiple(() =>
            {
                Assert.That(exitCode, Is.EqualTo(2));
                Assert.That(stderr, Is.Empty);
                Assert.That(document.RootElement.GetProperty("kind").GetString(), Is.EqualTo("command_error"));
            });
        }
        finally
        {
            File.Delete(temporaryPath);
        }
    }
}
