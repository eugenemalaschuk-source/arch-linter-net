using System.Text.Json;
using NUnit.Framework;

namespace ArchLinterNet.Cli.Tests;

public partial class CliIntegrationTests
{
    [Test]
    public void Gate_InSyncBaseline_ProjectsHumanJsonAndSarif()
    {
        string baselinePath = Path.Combine(Path.GetTempPath(), $"architecture-debt-gate-{Guid.NewGuid():N}.yml");
        try
        {
            var (generationExit, _, generationError) = RunCli(
                "baseline", "generate", "--policy", _graphPolicy, "--output", baselinePath);
            Assert.That(generationExit, Is.EqualTo(0), $"stderr: {generationError}");

            var (humanExit, human, humanError) = RunCli("gate", "--policy", _graphPolicy, "--baseline", baselinePath);
            var (jsonExit, json, jsonError) = RunCli("gate", "--policy", _graphPolicy, "--baseline", baselinePath, "--format", "json");
            var (sarifExit, sarif, sarifError) = RunCli("gate", "--policy", _graphPolicy, "--baseline", baselinePath, "--format", "sarif");
            using JsonDocument jsonDocument = JsonDocument.Parse(json);
            using JsonDocument sarifDocument = JsonDocument.Parse(sarif);

            Assert.Multiple(() =>
            {
                Assert.That(humanExit, Is.EqualTo(0), $"stderr: {humanError}");
                Assert.That(human, Does.Contain("Architecture debt gate").And.Contain("Decision: pass"));
                Assert.That(jsonExit, Is.EqualTo(0), $"stderr: {jsonError}");
                Assert.That(jsonDocument.RootElement.GetProperty("passed").GetBoolean(), Is.True);
                Assert.That(jsonDocument.RootElement.GetProperty("persistent_debt").GetProperty("in_sync").GetBoolean(), Is.True);
                Assert.That(jsonDocument.RootElement.GetProperty("policy_weakening").GetProperty("requested").GetBoolean(), Is.False);
                Assert.That(sarifExit, Is.EqualTo(0), $"stderr: {sarifError}");
                Assert.That(sarifDocument.RootElement.GetProperty("version").GetString(), Is.EqualTo("2.1.0"));
            });
        }
        finally
        {
            if (File.Exists(baselinePath))
            {
                File.Delete(baselinePath);
            }
        }
    }

    [Test]
    public void Gate_NewDebtFailsAndUnpairedWeakeningInputIsRejected()
    {
        string baselinePath = Path.Combine(Path.GetTempPath(), $"architecture-debt-gate-{Guid.NewGuid():N}.yml");
        try
        {
            File.WriteAllText(baselinePath, "version: 2\nbaseline: {}\n");
            var (debtExit, debtJson, debtError) = RunCli(
                "gate", "--policy", _graphPolicy, "--baseline", baselinePath, "--format", "json");
            var (contextExit, _, contextError) = RunCli(
                "gate", "--policy", _graphPolicy, "--baseline", baselinePath, "--base-context", "base.json");
            using JsonDocument document = JsonDocument.Parse(debtJson);

            Assert.Multiple(() =>
            {
                Assert.That(debtExit, Is.EqualTo(1), $"stderr: {debtError}");
                Assert.That(document.RootElement.GetProperty("passed").GetBoolean(), Is.False);
                Assert.That(document.RootElement.GetProperty("persistent_debt").GetProperty("entries").GetArrayLength(), Is.GreaterThan(0));
                Assert.That(contextExit, Is.EqualTo(2));
                Assert.That(contextError, Does.Contain("Both --base-context and --current-context"));
            });
        }
        finally
        {
            if (File.Exists(baselinePath))
            {
                File.Delete(baselinePath);
            }
        }
    }
}
