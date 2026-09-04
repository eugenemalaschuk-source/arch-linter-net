using System.Text.Json;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

public sealed partial class CheckpointBReleaseGateTests
{
    private static (CheckpointScenarioResult Scenario, string ReportPath) AssertReportPr(
        CandidatePackageFeed candidate, string root, string healthPath, string changeReportPath)
    {
        string outputPath = Path.Combine(root, "v08-architecture-pr-report.md");
        CommandResult report = candidate.RunToolWithReusedRestore(root,
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
        CommandResult badge = candidate.RunToolWithReusedRestore(root,
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

    // Real cross-projection agreement on overlapping canonical facts, comparing full canonical
    // finding identity (not just contract_id, which many findings for different occurrences can
    // share) across five independently rendered projections of the SAME strict validate run: the
    // JSON violations, the SARIF results (ArchitectureSarifFormatter embeds the identical
    // ArchitectureDiagnosticFormatter.FormatNormalizedFindingForSarif payload -- including
    // canonical_identity -- under results[].properties.arch_linter_net), the canonical Health
    // artifact's own embedded report_evidence.validation_outcomes findings for the strict mode
    // entry (ArchitectureHealthProjector reuses the same FormatNormalizedFindingForJson), the
    // packaged ArchLinterNet.Testing API (an isolated external consumer resolves it from the
    // candidate feed -- not the source-compiled test-host assembly -- and runs
    // ArchitectureValidationBuilder against the same policy, printing each violation's identity via
    // the same ArchitectureViolationIdentityJson.Serialize wire projection every formatter uses),
    // and the effective rule count / ignore debt counters both JSON and Health carry independently
    // in their own policy_inventory section. Report Markdown and the badge are prose/summary
    // projections rather than structured re-parse targets, so they are still checked by content, but
    // against the full canonical identity set rather than "at least one".
    private static CheckpointScenarioResult AssertProjectionParity(
        CandidatePackageFeed candidate, string root,
        string validateJson, string strictValidateSarifPath, string healthPath, string reportPath, string badgePath)
    {
        using JsonDocument validate = JsonDocument.Parse(validateJson);
        JsonElement jsonFindings = validate.RootElement.TryGetProperty("violations", out JsonElement violations)
            ? violations
            : validate.RootElement.GetProperty("findings");
        HashSet<string> jsonCanonicalIdentities = jsonFindings.EnumerateArray()
            .Select(finding => finding.TryGetProperty("canonical_identity", out JsonElement id) ? id.GetString() : null)
            .Where(id => !string.IsNullOrEmpty(id))
            .Select(id => id!)
            .ToHashSet(StringComparer.Ordinal);
        Assert.That(jsonCanonicalIdentities, Is.Not.Empty,
            $"v08-projection-parity expected at least one strict finding canonical_identity in the JSON projection: {validateJson}");

        JsonElement jsonPolicyInventory = validate.RootElement.GetProperty("policy_inventory");
        int jsonEffectiveRuleCount = jsonPolicyInventory.GetProperty("effective_rule_count").GetInt32();
        int jsonIgnoreDebtTotal = jsonPolicyInventory.GetProperty("ignore_debt").GetProperty("total").GetInt32();

        using JsonDocument sarif = JsonDocument.Parse(File.ReadAllText(strictValidateSarifPath));
        HashSet<string> sarifCanonicalIdentities = sarif.RootElement.GetProperty("runs")[0].GetProperty("results")
            .EnumerateArray()
            .Select(result => result.TryGetProperty("properties", out JsonElement properties)
                && properties.TryGetProperty("arch_linter_net", out JsonElement normalized)
                && normalized.TryGetProperty("canonical_identity", out JsonElement id)
                ? id.GetString()
                : null)
            .Where(id => !string.IsNullOrEmpty(id))
            .Select(id => id!)
            .ToHashSet(StringComparer.Ordinal);
        Assert.That(sarifCanonicalIdentities, Is.EqualTo(jsonCanonicalIdentities),
            "v08-projection-parity expected the SARIF projection's canonical finding identities to exactly match "
            + $"the JSON projection's: json={string.Join(",", jsonCanonicalIdentities)} sarif={string.Join(",", sarifCanonicalIdentities)}");

        // The packaged ArchLinterNet.Testing API does not bind --external-evidence (that surface is
        // CLI-only), so it cannot resolve the required-evidence applicability control this policy
        // also declares -- but that control produces an applicability finding, not an
        // ArchitectureViolation, so it never appears in result.Violations in the first place and does
        // not change the expected set. Exact equality (not a one-way subset) so a regression emitting
        // arbitrary additional identities through the Testing surface fails this scenario too.
        HashSet<string> testingCanonicalIdentities = candidate
            .RunTestingCanonicalIdentities(DependenciesPath(root))
            .ToHashSet(StringComparer.Ordinal);
        Assert.That(testingCanonicalIdentities, Is.EqualTo(jsonCanonicalIdentities),
            "v08-projection-parity expected the packaged ArchLinterNet.Testing API's canonical finding identities to "
            + $"exactly match the JSON projection's: json={string.Join(",", jsonCanonicalIdentities)} "
            + $"testing={string.Join(",", testingCanonicalIdentities)}");

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

        JsonElement strictOutcome = health.RootElement.GetProperty("report_evidence").GetProperty("validation_outcomes")
            .EnumerateArray()
            .Single(outcome => outcome.GetProperty("mode").GetString() == "strict");
        HashSet<string> healthCanonicalIdentities = strictOutcome.GetProperty("findings")
            .EnumerateArray()
            .Select(finding => finding.TryGetProperty("canonical_identity", out JsonElement id) ? id.GetString() : null)
            .Where(id => !string.IsNullOrEmpty(id))
            .Select(id => id!)
            .ToHashSet(StringComparer.Ordinal);
        // A subset, not exact equality: ArchitectureHealthProjector.ReportEvidence.Findings.BuildFindings
        // also folds outcome.PreflightDiagnostics (one build_state_preflight finding per project,
        // from --ensure-built's receipt verification) into the embedded findings list, which plain
        // `validate`'s violations[] array does not carry. Every strict violation JSON reports must
        // still appear in Health's own findings -- Health's set legitimately being broader, never
        // narrower, is exactly the fail-closed direction this scenario proves.
        Assert.That(jsonCanonicalIdentities.IsSubsetOf(healthCanonicalIdentities), Is.True,
            "v08-projection-parity expected every strict JSON finding's canonical_identity to also appear in the "
            + $"canonical Health artifact's own embedded strict findings: json={string.Join(",", jsonCanonicalIdentities)} "
            + $"health={string.Join(",", healthCanonicalIdentities)}");

        JsonElement healthPolicyInventory = strictOutcome.GetProperty("policy_inventory");
        Assert.Multiple(() =>
        {
            Assert.That(healthPolicyInventory.GetProperty("effective_rule_count").GetInt32(), Is.EqualTo(jsonEffectiveRuleCount),
                "v08-projection-parity expected the Health artifact's strict effective_rule_count to match the JSON projection's.");
            Assert.That(healthPolicyInventory.GetProperty("ignore_debt").GetProperty("total").GetInt32(), Is.EqualTo(jsonIgnoreDebtTotal),
                "v08-projection-parity expected the Health artifact's strict ignore_debt total to match the JSON projection's.");
        });

        int healthIgnoreDebtTotal = healthPolicyInventory.GetProperty("ignore_debt").GetProperty("total").GetInt32();
        int healthEffectiveRuleCount = healthPolicyInventory.GetProperty("effective_rule_count").GetInt32();

        // "FAILING · {ignores} ignores · {rules} rules" (ArchitectureHealthBadgeProjector.Project) --
        // the badge's embedded counters must match the canonical Health artifact's own
        // policy_inventory, not just the FAILING/gate prefix.
        using JsonDocument badge = JsonDocument.Parse(File.ReadAllText(badgePath));
        string badgeMessage = badge.RootElement.GetProperty("message").GetString() ?? string.Empty;
        Assert.That(badgeMessage,
            Is.EqualTo($"FAILING · {healthIgnoreDebtTotal} ignores · {healthEffectiveRuleCount} rules"),
            "v08-projection-parity expected the badge message's category and counters to match the canonical Health "
            + $"artifact exactly: {badgeMessage}");

        // PrReportMarkdownRenderer's exact rendered lines (AppendHeadline/AppendCompleteness) --
        // every overlapping fact the Markdown represents must match the canonical Health artifact, not
        // just "each contract id appears somewhere" (which stays green even if the category/gate/
        // counters shown are stale or fabricated).
        string reportContent = File.ReadAllText(reportPath);
        Assert.Multiple(() =>
        {
            Assert.That(reportContent, Does.Contain($"Architecture acceptance: **{gate}** (`gate={gate}`)"),
                $"v08-projection-parity expected the PR report headline to match the canonical gate: {reportContent}");
            Assert.That(reportContent, Does.Contain($"Architecture health: `{healthCategory}`"),
                $"v08-projection-parity expected the PR report headline to match the canonical Health category: {reportContent}");
            Assert.That(reportContent, Does.Contain($"Effective policy controls: `{healthEffectiveRuleCount}`"),
                $"v08-projection-parity expected the PR report to match the canonical effective_rule_count: {reportContent}");
            Assert.That(reportContent, Does.Contain($"Explicit waiver debt: `{healthIgnoreDebtTotal}` total"),
                $"v08-projection-parity expected the PR report to match the canonical ignore_debt total: {reportContent}");
        });

        string[] missingFromReport = jsonFindings.EnumerateArray()
            .Select(finding => finding.TryGetProperty("contract_id", out JsonElement id) ? id.GetString() : null)
            .Where(id => !string.IsNullOrEmpty(id))
            .Select(id => id!)
            .Distinct(StringComparer.Ordinal)
            .Where(contractId => !reportContent.Contains(contractId, StringComparison.Ordinal))
            .ToArray();
        Assert.That(missingFromReport, Is.Empty,
            "v08-projection-parity expected the PR Markdown report to name every distinct strict finding contract_id "
            + $"from JSON/SARIF, not just Health's gate/category summary: missing={string.Join(",", missingFromReport)}");

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
