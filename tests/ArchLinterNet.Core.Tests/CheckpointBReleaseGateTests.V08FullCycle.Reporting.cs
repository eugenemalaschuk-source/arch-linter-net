using System.Text.Json;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

public sealed partial class CheckpointBReleaseGateTests
{
    private static (CheckpointScenarioResult Scenario, string ReportPath) AssertReportPr(
        CandidatePackageFeed candidate, string root, string healthPath, string changeReportPath)
    {
        string outputPath = Path.Combine(root, "v08-architecture-pr-report.md");
        CommandResult report = candidate.RunTool(root,
            "report", "pr",
            "--health", healthPath,
            "--change", changeReportPath,
            "--max-details", "20",
            "--output", outputPath);
        Assert.That(report.ExitCode, Is.EqualTo(0), $"v08-report-pr: {report.CombinedOutput}");
        Assert.That(File.Exists(outputPath), Is.True, "v08-report-pr");
        Assert.That(File.ReadAllText(outputPath), Is.Not.Empty, "v08-report-pr");
        return (Passed("v08-report-pr"), outputPath);
    }

    private static (CheckpointScenarioResult Scenario, string BadgePath) AssertBadge(
        CandidatePackageFeed candidate, string root, string healthPath)
    {
        string outputPath = Path.Combine(root, "v08-architecture-health-badge.json");
        CommandResult badge = candidate.RunTool(root,
            "badge", "architecture-health",
            "--input", healthPath,
            "--output", outputPath);
        // healthPath is the FAILING scenario's output (gate: "fail"); badge's exit code mirrors the
        // gate (ArchitectureHealthBadgeProjector.ExitCode: pass->0, fail->1), not a flat 0/success.
        Assert.That(badge.ExitCode, Is.EqualTo(1), $"v08-badge: {badge.CombinedOutput}");
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(outputPath));
        Assert.That(document.RootElement.TryGetProperty("message", out JsonElement badgeMessage), Is.True, "v08-badge");
        Assert.That(badgeMessage.GetString(), Does.StartWith("FAILING"), "v08-badge");
        return (Passed("v08-badge"), outputPath);
    }

    // Real cross-projection agreement on overlapping canonical facts, not a second read of Health's
    // own two fields: JSON violations vs the SARIF projection of the same strict validate run (same
    // contract identities as ruleId, per ArchitectureSarifFormatter), and the canonical Health
    // artifact's gate/health category vs the same category appearing in both the PR report Markdown
    // and the badge JSON it and report pr were independently rendered from.
    private static CheckpointScenarioResult AssertProjectionParity(
        string validateJson, string strictValidateSarifPath, string healthPath, string reportPath, string badgePath)
    {
        using JsonDocument validate = JsonDocument.Parse(validateJson);
        JsonElement jsonFindings = validate.RootElement.TryGetProperty("violations", out JsonElement violations)
            ? violations
            : validate.RootElement.GetProperty("findings");
        HashSet<string> jsonContractIds = jsonFindings.EnumerateArray()
            .Select(finding => finding.TryGetProperty("contract_id", out JsonElement id) ? id.GetString() : null)
            .Where(id => !string.IsNullOrEmpty(id))
            .Select(id => id!)
            .ToHashSet(StringComparer.Ordinal);
        Assert.That(jsonContractIds, Is.Not.Empty,
            $"v08-projection-parity expected at least one strict finding contract_id in the JSON projection: {validateJson}");

        using JsonDocument sarif = JsonDocument.Parse(File.ReadAllText(strictValidateSarifPath));
        HashSet<string> sarifRuleIds = sarif.RootElement.GetProperty("runs")[0].GetProperty("results")
            .EnumerateArray()
            .Select(result => result.TryGetProperty("ruleId", out JsonElement ruleId) ? ruleId.GetString() : null)
            .Where(id => !string.IsNullOrEmpty(id))
            .Select(id => id!)
            .ToHashSet(StringComparer.Ordinal);
        Assert.That(jsonContractIds.IsSubsetOf(sarifRuleIds), Is.True,
            $"v08-projection-parity expected every strict JSON finding's contract_id to appear as a SARIF ruleId: "
            + $"json={string.Join(",", jsonContractIds)} sarif={string.Join(",", sarifRuleIds)}");

        using JsonDocument health = JsonDocument.Parse(File.ReadAllText(healthPath));
        string? healthCategory = health.RootElement.GetProperty("health").GetString();
        string? gate = health.RootElement.GetProperty("gate").GetString();
        Assert.Multiple(() =>
        {
            Assert.That(healthCategory, Is.EqualTo("failing"),
                "v08-projection-parity expected the canonical Health artifact to carry the failing category consumed by report/badge.");
            Assert.That(gate, Is.EqualTo("fail"),
                "v08-projection-parity expected the canonical Health artifact to carry the fail gate consumed by report/badge.");
        });

        using JsonDocument badge = JsonDocument.Parse(File.ReadAllText(badgePath));
        string badgeMessage = badge.RootElement.GetProperty("message").GetString() ?? string.Empty;
        Assert.That(badgeMessage, Does.StartWith("FAILING"),
            $"v08-projection-parity expected the badge message to carry the same failing Health category: {badgeMessage}");

        string reportContent = File.ReadAllText(reportPath);
        bool reportNamesAStrictFinding = jsonContractIds.Any(id => reportContent.Contains(id, StringComparison.Ordinal));
        Assert.That(reportNamesAStrictFinding, Is.True,
            $"v08-projection-parity expected the PR Markdown report to name at least one of the same strict finding "
            + $"contract_ids as JSON/SARIF ({string.Join(",", jsonContractIds)}), not just Health's gate/category summary.");

        return Passed("v08-projection-parity");
    }

    private static void WriteSarif(string path, bool executionSuccessful, IReadOnlyList<string> resultMessages)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var sarif = new
        {
            version = "2.1.0",
            runs = new object[]
            {
                new
                {
                    tool = new { driver = new { name = "V08 Synthetic Analyzer", version = "1.0.0" } },
                    automationDetails = new { id = "v08-full-cycle" },
                    invocations = new object[] { new { executionSuccessful } },
                    results = resultMessages
                        .Select(static message => new
                        {
                            ruleId = "synthetic",
                            level = "warning",
                            message = new { text = message },
                        })
                        .ToArray(),
                },
            },
        };
        File.WriteAllText(path, JsonSerializer.Serialize(sarif));
    }
}
