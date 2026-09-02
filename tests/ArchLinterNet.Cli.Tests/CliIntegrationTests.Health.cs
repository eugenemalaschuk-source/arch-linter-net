using System.Text.Json;
using NUnit.Framework;

namespace ArchLinterNet.Cli.Tests;

public partial class CliIntegrationTests
{
    [Test]
    public void Health_InSyncBaseline_ProjectsHumanAndJsonAndExitsZero()
    {
        string baselinePath = Path.Combine(Path.GetTempPath(), $"architecture-health-{Guid.NewGuid():N}.yml");
        try
        {
            var (generationExit, _, generationError) = RunCli(
                "baseline", "generate", "--policy", _passingPolicy, "--output", baselinePath);
            Assert.That(generationExit, Is.EqualTo(0), $"stderr: {generationError}");

            var (humanExit, human, humanError) = RunCli(
                "health", "--policy", _passingPolicy, "--baseline", baselinePath);
            var (jsonExit, json, jsonError) = RunCli(
                "health", "--policy", _passingPolicy, "--baseline", baselinePath, "--format", "json");
            using JsonDocument document = JsonDocument.Parse(json);

            Assert.Multiple(() =>
            {
                Assert.That(humanExit, Is.EqualTo(0), $"stderr: {humanError}");
                Assert.That(human, Does.Contain("Architecture Health").And.Contain("Gate: pass").And.Contain("Health: healthy"));
                Assert.That(jsonExit, Is.EqualTo(0), $"stderr: {jsonError}");
                Assert.That(document.RootElement.GetProperty("schema_id").GetString(),
                    Is.EqualTo("architecture-health/v1"));
                Assert.That(document.RootElement.GetProperty("gate").GetString(), Is.EqualTo("pass"));
                Assert.That(document.RootElement.GetProperty("health").GetString(), Is.EqualTo("healthy"));
                Assert.That(document.RootElement.GetProperty("dimensions").GetArrayLength(), Is.GreaterThan(0));
            });
        }
        finally
        {
            DeleteIfPresent(baselinePath);
        }
    }

    [Test]
    public void Health_CanonicalEmptyBaseline_ProjectsNonPassState()
    {
        string baselinePath = Path.Combine(Path.GetTempPath(), $"architecture-health-{Guid.NewGuid():N}.yml");
        try
        {
            File.WriteAllText(baselinePath, "version: 3\nbaseline: {}\nmetric_baselines: []\n");

            var (exitCode, json, error) = RunCli(
                "health", "--policy", _graphPolicy, "--baseline", baselinePath, "--format", "json");
            using JsonDocument document = JsonDocument.Parse(json);

            Assert.Multiple(() =>
            {
                Assert.That(exitCode, Is.EqualTo(1), $"stderr: {error}");
                Assert.That(document.RootElement.GetProperty("gate").GetString(), Is.Not.EqualTo("pass"));
                Assert.That(document.RootElement.GetProperty("health").GetString(), Is.Not.EqualTo("healthy"));
            });
        }
        finally
        {
            DeleteIfPresent(baselinePath);
        }
    }

    [Test]
    public void Health_UnpairedPolicyContext_UsesCommandErrorPath()
    {
        string baselinePath = Path.Combine(Path.GetTempPath(), $"architecture-health-{Guid.NewGuid():N}.yml");
        try
        {
            File.WriteAllText(baselinePath, "version: 2\nbaseline: {}\n");

            var (exitCode, output, error) = RunCli(
                "health", "--policy", _passingPolicy, "--baseline", baselinePath,
                "--base-context", "base.json", "--format", "json");
            using JsonDocument document = JsonDocument.Parse(output);

            Assert.Multiple(() =>
            {
                Assert.That(exitCode, Is.EqualTo(2));
                Assert.That(error, Is.Empty);
                Assert.That(document.RootElement.GetProperty("kind").GetString(), Is.EqualTo("command_error"));
                Assert.That(document.RootElement.GetProperty("error").GetProperty("category").GetString(),
                    Is.EqualTo("missing-policy-context"));
            });
        }
        finally
        {
            DeleteIfPresent(baselinePath);
        }
    }

    [Test]
    public void Health_RequiredApplicabilityInputMissing_EmitsUnassessableHealthJson()
    {
        string policyPath = Path.Combine(
            _repoRoot, "tests", "ArchLinterNet.Cli.Tests", "TestPolicies", "metrics-unassessable-policy.yml");
        string baselinePath = Path.Combine(Path.GetTempPath(), $"architecture-health-{Guid.NewGuid():N}.yml");
        try
        {
            File.WriteAllText(baselinePath, "version: 2\nbaseline: {}\n");

            var (exitCode, json, error) = RunCli(
                "health", "--policy", policyPath, "--baseline", baselinePath, "--format", "json");
            using JsonDocument document = JsonDocument.Parse(json);

            Assert.Multiple(() =>
            {
                Assert.That(exitCode, Is.EqualTo(2), $"stderr: {error}");
                Assert.That(error, Is.Empty);
                Assert.That(document.RootElement.GetProperty("schema_id").GetString(),
                    Is.EqualTo("architecture-health/v1"));
                Assert.That(document.RootElement.GetProperty("gate").GetString(), Is.EqualTo("unassessable"));
                Assert.That(document.RootElement.GetProperty("health").GetString(), Is.EqualTo("unassessable"));
                Assert.That(document.RootElement.TryGetProperty("kind", out _), Is.False);
            });
        }
        finally
        {
            DeleteIfPresent(baselinePath);
        }
    }

    private static void DeleteIfPresent(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}
