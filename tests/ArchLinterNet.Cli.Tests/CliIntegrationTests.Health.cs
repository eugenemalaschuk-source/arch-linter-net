using System.Text.Json;
using ArchLinterNet.Core.Change;
using NUnit.Framework;

namespace ArchLinterNet.Cli.Tests;

[TestFixture]
internal sealed class CliHealthIntegrationTests : CliIntegrationTestBase
{
    [Test]
    public void Health_InSyncBaseline_ProjectsHumanAndJsonAndExitsZero()
    {
        string baselinePath = Path.Combine(Path.GetTempPath(), $"architecture-health-{Guid.NewGuid():N}.yml");
        try
        {
            var (generationExit, _, generationError) = RunCli(
                "baseline", "generate", "--policy", PassingPolicy, "--output", baselinePath);
            Assert.That(generationExit, Is.EqualTo(0), $"stderr: {generationError}");

            var (humanExit, human, humanError) = RunCli(
                "health", "--policy", PassingPolicy, "--baseline", baselinePath);
            var (jsonExit, json, jsonError) = RunCli(
                "health", "--policy", PassingPolicy, "--baseline", baselinePath, "--format", "json");
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
    public void Health_ChangeSnapshotOption_UsesCompleteCanonicalProjection()
    {
        string baselinePath = Path.Combine(Path.GetTempPath(), $"architecture-health-change-{Guid.NewGuid():N}.yml");
        string snapshotPath = Path.Combine(Path.GetTempPath(), $"architecture-health-change-{Guid.NewGuid():N}.json");
        try
        {
            var (generationExit, _, generationError) = RunCli(
                "baseline", "generate", "--policy", PassingPolicy, "--output", baselinePath);
            Assert.That(generationExit, Is.EqualTo(0), $"stderr: {generationError}");

            var (exitCode, json, error) = RunCli(
                "health", "--policy", PassingPolicy, "--baseline", baselinePath,
                "--format", "json", "--change-snapshot", snapshotPath);
            using JsonDocument health = JsonDocument.Parse(json);
            ArchitectureChangeSnapshot snapshot = ArchitectureChangeReports.DeserializeSnapshot(
                File.ReadAllText(snapshotPath));

            Assert.Multiple(() =>
            {
                Assert.That(exitCode, Is.EqualTo(0), $"stderr: {error}");
                Assert.That(health.RootElement.GetProperty("schema_id").GetString(),
                    Is.EqualTo("architecture-health/v1"));
                Assert.That(snapshot.SchemaVersion, Is.EqualTo(2));
                Assert.That(snapshot.Mode, Is.EqualTo("strict"));
                Assert.That(snapshot.Entries, Is.Not.Null);
                Assert.That(snapshot.Findings, Is.Not.Null);
            });
        }
        finally
        {
            DeleteIfPresent(baselinePath);
            DeleteIfPresent(snapshotPath);
        }
    }

    [Test]
    public void Health_Profile_ContainsMeasuredPhasesAndPreservesResult()
    {
        string baselinePath = Path.Combine(Path.GetTempPath(), $"architecture-health-profile-{Guid.NewGuid():N}.yml");
        string profilePath = Path.Combine(Path.GetTempPath(), $"architecture-health-profile-{Guid.NewGuid():N}.json");
        try
        {
            var (generationExit, _, generationError) = RunCli(
                "baseline", "generate", "--policy", PassingPolicy, "--output", baselinePath);
            Assert.That(generationExit, Is.EqualTo(0), $"stderr: {generationError}");

            var unprofiled = RunCli(
                "health", "--policy", PassingPolicy, "--baseline", baselinePath, "--format", "json");
            var profiled = RunCli(
                "health", "--policy", PassingPolicy, "--baseline", baselinePath, "--format", "json",
                "--profile", profilePath);
            using JsonDocument profile = JsonDocument.Parse(File.ReadAllText(profilePath));
            JsonElement root = profile.RootElement;
            string[] phaseNames = root.GetProperty("Phases")
                .EnumerateArray()
                .Select(phase => phase.GetProperty("Name").GetString()!)
                .ToArray();

            Assert.Multiple(() =>
            {
                Assert.That(profiled.ExitCode, Is.EqualTo(unprofiled.ExitCode), $"stderr: {profiled.StdErr}");
                Assert.That(profiled.StdOut, Is.EqualTo(unprofiled.StdOut));
                Assert.That(profiled.StdErr, Is.EqualTo(unprofiled.StdErr));
                Assert.That(root.GetProperty("SchemaId").GetString(), Is.EqualTo("analysis-profile/v1"));
                Assert.That(phaseNames, Does.Contain("total"));
                Assert.That(phaseNames, Does.Contain("health_validation_evaluation"));
                Assert.That(phaseNames, Does.Contain("health_debt_gate"));
                Assert.That(phaseNames, Does.Contain("health_projection"));
                Assert.That(root.GetProperty("Measurements").ValueKind, Is.EqualTo(JsonValueKind.Object));
                Assert.That(root.GetProperty("Measurements").GetProperty("AllocatedBytesTotal").GetInt64(),
                    Is.GreaterThanOrEqualTo(0));
                Assert.That(root.GetProperty("Counters").GetProperty("ModesEvaluated").GetInt32(), Is.EqualTo(2));
                Assert.That(root.GetProperty("Counters").GetProperty("SnapshotMaterializations").GetInt32(), Is.EqualTo(1));
            });
        }
        finally
        {
            DeleteIfPresent(baselinePath);
            DeleteIfPresent(profilePath);
        }
    }

    [Test]
    public void Health_ChangeSnapshotProfile_UsesOneMeasuredSnapshot()
    {
        string baselinePath = Path.Combine(Path.GetTempPath(), $"architecture-health-change-profile-{Guid.NewGuid():N}.yml");
        string snapshotPath = Path.Combine(Path.GetTempPath(), $"architecture-health-change-profile-{Guid.NewGuid():N}.json");
        string profilePath = Path.Combine(Path.GetTempPath(), $"architecture-health-change-profile-{Guid.NewGuid():N}.json");
        try
        {
            var (generationExit, _, generationError) = RunCli(
                "baseline", "generate", "--policy", PassingPolicy, "--output", baselinePath);
            Assert.That(generationExit, Is.EqualTo(0), $"stderr: {generationError}");

            var result = RunCli(
                "health", "--policy", PassingPolicy, "--baseline", baselinePath, "--format", "json",
                "--change-snapshot", snapshotPath, "--profile", profilePath);
            using JsonDocument profile = JsonDocument.Parse(File.ReadAllText(profilePath));
            string[] phaseNames = profile.RootElement.GetProperty("Phases")
                .EnumerateArray()
                .Select(phase => phase.GetProperty("Name").GetString()!)
                .ToArray();

            Assert.Multiple(() =>
            {
                Assert.That(result.ExitCode, Is.EqualTo(0), $"stderr: {result.StdErr}");
                Assert.That(File.Exists(snapshotPath), Is.True);
                Assert.That(profile.RootElement.GetProperty("SchemaId").GetString(), Is.EqualTo("analysis-profile/v1"));
                Assert.That(profile.RootElement.GetProperty("Counters").GetProperty("SnapshotMaterializations").GetInt32(), Is.EqualTo(1));
                Assert.That(phaseNames, Does.Contain("total"));
                Assert.That(phaseNames, Does.Contain("health_validation_evaluation"));
                Assert.That(phaseNames, Does.Contain("health_debt_gate"));
                Assert.That(phaseNames, Does.Contain("health_projection"));
            });
        }
        finally
        {
            DeleteIfPresent(baselinePath);
            DeleteIfPresent(snapshotPath);
            DeleteIfPresent(profilePath);
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
                "health", "--policy", GraphPolicy, "--baseline", baselinePath, "--format", "json");
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
                "health", "--policy", PassingPolicy, "--baseline", baselinePath,
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
            RepoRoot, "tests", "ArchLinterNet.Cli.Tests", "TestPolicies", "metrics-unassessable-policy.yml");
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

}
