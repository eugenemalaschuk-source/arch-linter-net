using System.Text.Json;
using NUnit.Framework;

namespace ArchLinterNet.Cli.Tests;

public partial class CliIntegrationTests
{
    [TestCase("revision", 0, "complete", "current")]
    [TestCase("other-revision", 2, "unassessable", "stale")]
    public void ReportPr_HealthExternalEvidenceProducerChain_PreservesCanonicalTrustState(
        string sarifRevision,
        int expectedHealthExit,
        string expectedAvailability,
        string expectedTrustState)
    {
        string directory = Path.Combine(Path.GetTempPath(), $"architecture-pr-external-{Guid.NewGuid():N}");
        string policyPath = Path.Combine(directory, "policy.yml");
        string sarifPath = Path.Combine(directory, "external.sarif");
        string baselinePath = Path.Combine(directory, "baseline.yml");
        string baseSnapshotPath = Path.Combine(directory, "base.json");
        string currentSnapshotPath = Path.Combine(directory, "current.json");
        string changePath = Path.Combine(directory, "change.json");
        string healthPath = Path.Combine(directory, "health.json");
        try
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(policyPath, ExternalEvidencePolicy);
            File.WriteAllText(sarifPath, ZeroResultSarif(sarifRevision));

            var (baselineExit, _, baselineError) = RunCli(
                "baseline", "generate", "--policy", policyPath, "--output", baselinePath);
            Assert.That(baselineExit, Is.EqualTo(0), $"stderr: {baselineError}");

            var (baseSnapshotExit, _, baseSnapshotError) = RunCli(
                "change", "snapshot", "--policy", policyPath, "--baseline", baselinePath,
                "--mode", "strict", "--output", baseSnapshotPath);
            Assert.That(baseSnapshotExit, Is.EqualTo(0), $"stderr: {baseSnapshotError}");

            var (currentSnapshotExit, _, currentSnapshotError) = RunCli(
                "change", "snapshot", "--policy", policyPath, "--baseline", baselinePath,
                "--mode", "strict", "--output", currentSnapshotPath);
            Assert.That(currentSnapshotExit, Is.EqualTo(0), $"stderr: {currentSnapshotError}");

            var (changeExit, _, changeError) = RunCli(
                "change", "report", "--base", baseSnapshotPath, "--current", currentSnapshotPath,
                "--execution-context", "run", "--format", "json", "--output", changePath);
            Assert.That(changeExit, Is.EqualTo(0), $"stderr: {changeError}");

            var (healthExit, healthJson, healthError) = RunCli(
                "health", "--policy", policyPath, "--baseline", baselinePath, "--format", "json",
                "--execution-context", "run",
                "--external-evidence", "id=external.scan,path=external.sarif",
                "--evidence-repository", "repo",
                "--evidence-revision", "revision");
            Assert.That(healthExit, Is.EqualTo(expectedHealthExit), $"stderr: {healthError}");
            File.WriteAllText(healthPath, healthJson);

            using JsonDocument health = JsonDocument.Parse(healthJson);
            JsonElement receipt = health.RootElement.GetProperty("report_evidence")
                .GetProperty("validation_outcomes")
                .EnumerateArray()
                .Single(item => item.GetProperty("mode").GetString() == "strict")
                .GetProperty("external_evidence")
                .GetProperty("trust_receipts")
                .EnumerateArray()
                .Single();

            var (reportExit, markdown, reportError) = RunCli(
                "report", "pr", "--health", healthPath, "--change", changePath, "--max-details", "3");

            Assert.Multiple(() =>
            {
                Assert.That(receipt.GetProperty("logical_id").GetString(), Is.EqualTo("external.scan"));
                Assert.That(receipt.GetProperty("state").GetString(), Is.EqualTo(expectedTrustState));
                Assert.That(reportExit, Is.EqualTo(0), $"stderr: {reportError}");
                Assert.That(markdown, Does.Contain($"Report availability: `{expectedAvailability}`"));
                Assert.That(markdown, Does.Contain($"logical_evidence=`external.scan` state=`{expectedTrustState}`"));
                Assert.That(reportError, Is.Empty);
            });
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }

        }
    }

    private const string ExternalEvidencePolicy = """
        version: 1
        name: PR report external-evidence policy
        layers:
          core:
            namespace: ArchLinterNet.Core
        analysis:
          target_assemblies:
            - ArchLinterNet.Core
        contracts:
          strict:
            - id: core-no-forbidden
              name: core-has-no-forbidden-dependencies
              source: core
              forbidden: []
              reason: Core has no forbidden dependencies in this test.
        external_evidence:
          - id: external.scan
            format: sarif
            required: true
            tool: Synthetic.Scanner
            tool_version: "1.0"
            run: assessment-42
            require_repository: true
            require_revision: true
            require_scope: false
        """;

    private static string ZeroResultSarif(string revision) => $$"""
        {
          "version": "2.1.0",
          "runs": [
            {
              "tool": {
                "driver": {
                  "name": "Synthetic.Scanner",
                  "version": "1.0",
                  "rules": []
                }
              },
              "automationDetails": { "id": "assessment-42" },
              "invocations": [ { "executionSuccessful": true } ],
              "versionControlProvenance": [
                { "repositoryUri": "repo", "revisionId": "{{revision}}" }
              ],
              "results": []
            }
          ]
        }
        """;
}
