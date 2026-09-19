using System.Security.Cryptography;
using System.Text.Json;
using NUnit.Framework;

namespace ArchLinterNet.Cli.Tests;

[TestFixture]
internal sealed class CliHealthRevalidatePublicationIntegrationTests : CliIntegrationTestBase
{
    [Test]
    public void RevalidatePublication_UsesOnlySerializedHealthEvidenceAndLeavesInputUnchanged()
    {
        string baselinePath = Path.Combine(Path.GetTempPath(), $"architecture-health-{Guid.NewGuid():N}.yml");
        string healthPath = Path.Combine(Path.GetTempPath(), $"architecture-health-{Guid.NewGuid():N}.json");
        string receiptPath = Path.Combine(Path.GetTempPath(), $"architecture-health-receipt-{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(baselinePath, "version: 3\nbaseline: {}\nmetric_baselines: []\n");
            var (healthExit, healthJson, healthError) = RunCli(
                "health", "--policy", PassingPolicy, "--baseline", baselinePath,
                "--execution-context", "run", "--format", "json");
            Assert.That(healthExit, Is.EqualTo(0), $"stderr: {healthError}");
            File.WriteAllText(healthPath, healthJson);
            byte[] originalInput = File.ReadAllBytes(healthPath);
            string sourceDigest = Convert.ToHexStringLower(SHA256.HashData(originalInput));

            var (exitCode, stdout, stderr) = RunCli(
                "health", "revalidate-publication",
                "--input", healthPath,
                "--evaluation-date", "2026-09-10",
                "--source-health-sha256", sourceDigest,
                "--badge-payload-sha256", new string('b', 64),
                "--merged-tree-sha", new string('c', 40),
                "--producer-identity-sha256", new string('d', 64),
                "--output", receiptPath);
            using JsonDocument receipt = JsonDocument.Parse(File.ReadAllText(receiptPath));

            Assert.Multiple(() =>
            {
                Assert.That(exitCode, Is.EqualTo(0), $"stderr: {stderr}");
                Assert.That(stdout, Is.Empty);
                Assert.That(stderr, Is.Empty);
                Assert.That(receipt.RootElement.GetProperty("schema_id").GetString(),
                    Is.EqualTo("architecture-health-temporal-publication-receipt/v1"));
                Assert.That(receipt.RootElement.GetProperty("state").GetString(), Is.EqualTo("ready"));
                Assert.That(receipt.RootElement.GetProperty("evaluation_date").GetString(), Is.EqualTo("2026-09-10"));
                Assert.That(receipt.RootElement.GetProperty("semantic_horizon").GetString(), Is.EqualTo("2026-09-11T00:00:00Z"));
                Assert.That(receipt.RootElement.GetProperty("source_health_sha256").GetString(), Is.EqualTo(sourceDigest));
                Assert.That(File.ReadAllBytes(healthPath), Is.EqualTo(originalInput));
            });
        }
        finally
        {
            DeleteIfPresent(baselinePath);
            DeleteIfPresent(healthPath);
            DeleteIfPresent(receiptPath);
        }
    }

    [Test]
    public void RevalidatePublication_BindingMismatch_FailsClosed()
    {
        string inputPath = Path.Combine(Path.GetTempPath(), $"architecture-health-invalid-{Guid.NewGuid():N}.json");
        try
        {
            const string InputJson = "{\"schema_id\":\"architecture-health/v1\"}";
            File.WriteAllText(inputPath, InputJson);
            string sourceDigest = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(inputPath)));

            var (exitCode, stdout, stderr) = RunCli(
                "health", "revalidate-publication",
                "--input", inputPath,
                "--evaluation-date", "2026-09-10",
                "--source-health-sha256", sourceDigest,
                "--badge-payload-sha256", new string('b', 64),
                "--merged-tree-sha", new string('c', 40),
                "--producer-identity-sha256", new string('d', 64));
            using JsonDocument receipt = JsonDocument.Parse(stdout);

            Assert.Multiple(() =>
            {
                Assert.That(exitCode, Is.EqualTo(2));
                Assert.That(stderr, Is.Empty);
                Assert.That(receipt.RootElement.GetProperty("state").GetString(), Is.EqualTo("unassessable"));
                Assert.That(receipt.RootElement.GetProperty("reasons").GetArrayLength(), Is.GreaterThan(0));
            });
        }
        finally
        {
            DeleteIfPresent(inputPath);
        }
    }
}
