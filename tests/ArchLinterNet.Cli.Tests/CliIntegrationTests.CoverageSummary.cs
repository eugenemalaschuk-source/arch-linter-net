using System.Text.Json;
using NUnit.Framework;

namespace ArchLinterNet.Cli.Tests;

public partial class CliIntegrationTests
{
    private string CoveragePolicy => Path.Combine(
        _repoRoot, "tests", "ArchLinterNet.Cli.Tests", "TestPolicies", "coverage-policy.yml");

    private string RuleInputCoveragePolicy => Path.Combine(
        _repoRoot, "tests", "ArchLinterNet.Cli.Tests", "TestPolicies", "rule-input-coverage-policy.yml");

    private static JsonElement FindSummaryEntry(JsonElement summaries, string contractId)
    {
        foreach (JsonElement entry in summaries.EnumerateArray())
        {
            if (entry.GetProperty("contract_id").GetString() == contractId)
            {
                return entry;
            }
        }

        Assert.Fail($"No coverage_summary entry found for contract_id '{contractId}'.");
        throw new InvalidOperationException("Unreachable.");
    }

    [Test]
    public void CoverageSummary_JsonOutput_IncludesSummarySiblingToCoverageFindings()
    {
        var (exitCode, stdout, _) = RunCli("--policy", CoveragePolicy, "--format", "json");

        Assert.That(exitCode, Is.EqualTo(0));

        using var doc = JsonDocument.Parse(stdout);
        JsonElement root = doc.RootElement;

        Assert.That(root.TryGetProperty("coverage_findings", out _), Is.True);
        Assert.That(root.TryGetProperty("coverage_summary", out JsonElement summaries), Is.True);
        Assert.That(summaries.GetArrayLength(), Is.EqualTo(1));

        JsonElement entry = FindSummaryEntry(summaries, "validation-namespace-coverage");
        Assert.Multiple(() =>
        {
            Assert.That(entry.GetProperty("contract").GetString(), Is.EqualTo("validation-namespace-coverage"));
            Assert.That(entry.GetProperty("scope").GetString(), Is.EqualTo("namespace"));

            JsonElement counts = entry.GetProperty("counts");
            Assert.That(counts.GetProperty("covered").GetInt32(), Is.EqualTo(0));
            Assert.That(counts.GetProperty("excluded").GetInt32(), Is.EqualTo(0));
            Assert.That(counts.GetProperty("uncovered").GetInt32(), Is.EqualTo(2));
            Assert.That(counts.GetProperty("stale").GetInt32(), Is.EqualTo(0));
            Assert.That(counts.GetProperty("unknown").GetInt32(), Is.EqualTo(0));

            // ArchLinterNet.Core.Validation.Abstractions is a distinct namespace under the
            // ArchLinterNet.Core.Validation root, so it surfaces as its own uncovered item.
            JsonElement uncoveredItems = entry.GetProperty("uncovered_items");
            Assert.That(uncoveredItems.GetArrayLength(), Is.EqualTo(2));
            Assert.That(uncoveredItems[0].GetProperty("item").GetString(), Is.EqualTo("ArchLinterNet.Core.Validation"));
            Assert.That(uncoveredItems[1].GetProperty("item").GetString(), Is.EqualTo("ArchLinterNet.Core.Validation.Abstractions"));
            Assert.That(entry.GetProperty("excluded_items").GetArrayLength(), Is.EqualTo(0));
            Assert.That(entry.GetProperty("stale_items").GetArrayLength(), Is.EqualTo(0));
            Assert.That(entry.GetProperty("unknown_items").GetArrayLength(), Is.EqualTo(0));
        });
    }

    [Test]
    public void CoverageSummary_JsonOutput_BucketsRuleInputStaleAndUnknownSeparately()
    {
        // ghost-rule's forbidden layer ("ghost") matches no code -> stale.
        // typo-rule's source layer ("does_not_exist_layer") isn't declared at all -> unknown.
        // Both must be locatable by which bucket they're in, not lumped into a single
        // ambiguous "uncovered" list.
        var (exitCode, stdout, _) = RunCli("--policy", RuleInputCoveragePolicy, "--format", "json");

        Assert.That(exitCode, Is.EqualTo(0));

        using var doc = JsonDocument.Parse(stdout);
        JsonElement entry = FindSummaryEntry(doc.RootElement.GetProperty("coverage_summary"), "rule-input-coverage");

        Assert.Multiple(() =>
        {
            Assert.That(entry.GetProperty("scope").GetString(), Is.EqualTo("rule_input"));

            JsonElement counts = entry.GetProperty("counts");
            Assert.That(counts.GetProperty("stale").GetInt32(), Is.EqualTo(1));
            Assert.That(counts.GetProperty("unknown").GetInt32(), Is.EqualTo(1));

            Assert.That(entry.GetProperty("uncovered_items").GetArrayLength(), Is.EqualTo(0));

            JsonElement staleItems = entry.GetProperty("stale_items");
            Assert.That(staleItems.GetArrayLength(), Is.EqualTo(1));
            Assert.That(staleItems[0].GetProperty("item").GetString(), Is.EqualTo("ghost-rule"));
            Assert.That(staleItems[0].GetProperty("evidence").GetString(), Is.EqualTo("ghost"));

            JsonElement unknownItems = entry.GetProperty("unknown_items");
            Assert.That(unknownItems.GetArrayLength(), Is.EqualTo(1));
            Assert.That(unknownItems[0].GetProperty("item").GetString(), Is.EqualTo("typo-rule"));
            Assert.That(unknownItems[0].GetProperty("evidence").GetString(), Is.EqualTo("does_not_exist_layer"));
        });
    }

    [Test]
    public void CoverageSummary_HumanOutput_LabelsRuleInputStaleAndUnknownDistinctly()
    {
        var (exitCode, stdout, _) = RunCli("--policy", RuleInputCoveragePolicy, "--format", "human");

        Assert.That(exitCode, Is.EqualTo(0));
        Assert.That(stdout, Does.Contain("stale: ghost-rule (ghost)"));
        Assert.That(stdout, Does.Contain("unknown: typo-rule (does_not_exist_layer)"));
        Assert.That(stdout, Does.Not.Contain("uncovered: ghost-rule"));
        Assert.That(stdout, Does.Not.Contain("uncovered: typo-rule"));
    }

    [Test]
    public void CoverageSummary_HumanOutput_PrintsSummarySectionAfterFindings()
    {
        var (exitCode, stdout, _) = RunCli("--policy", CoveragePolicy, "--format", "human");

        Assert.That(exitCode, Is.EqualTo(0));
        Assert.That(stdout, Does.Contain("Coverage findings:"));
        Assert.That(stdout, Does.Contain("Coverage summary:"));
        Assert.That(stdout, Does.Contain("covered=0 excluded=0 uncovered=2 stale=0 unknown=0"));

        int findingsIndex = stdout.IndexOf("Coverage findings:", StringComparison.Ordinal);
        int summaryIndex = stdout.IndexOf("Coverage summary:", StringComparison.Ordinal);
        Assert.That(summaryIndex, Is.GreaterThan(findingsIndex));
    }

    [Test]
    public void CoverageSummary_HumanOutput_IsDeterministicAcrossRepeatedRuns()
    {
        var (_, firstStdout, _) = RunCli("--policy", CoveragePolicy, "--format", "human");
        var (_, secondStdout, _) = RunCli("--policy", CoveragePolicy, "--format", "human");

        Assert.That(firstStdout, Is.EqualTo(secondStdout));
    }

    [Test]
    public void CoverageSummary_AuditOnlyMode_StillReportsSummaryAndPassesRun()
    {
        var (exitCode, stdout, _) = RunCli("--policy", CoveragePolicy, "--format", "human");

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(0), "analysis.coverage: warn must not fail the run.");
            Assert.That(stdout, Does.Contain("Architecture validation passed."));
            Assert.That(stdout, Does.Contain("Coverage summary:"));
        });
    }

    [Test]
    public static void CoverageSummary_NoCoverageContracts_OmitsSummarySection()
    {
        var (exitCode, stdout, _) = RunCli("--policy", _passingPolicy, "--format", "human");

        Assert.That(exitCode, Is.EqualTo(0));
        Assert.That(stdout, Does.Not.Contain("Coverage summary:"));
    }

    [Test]
    public static void CoverageSummary_NoCoverageContracts_JsonStillReportsEmptyArray()
    {
        var (exitCode, stdout, _) = RunCli("--policy", _passingPolicy, "--format", "json");

        Assert.That(exitCode, Is.EqualTo(0));

        using var doc = JsonDocument.Parse(stdout);
        Assert.That(doc.RootElement.GetProperty("coverage_summary").GetArrayLength(), Is.EqualTo(0));
    }

    [Test]
    public void CoverageSummary_ContractFilterExcludesUnselectedCoverageContract_JsonOmitsSummaryEntry()
    {
        // coverage-policy.yml declares both a non-coverage contract (core-no-forbidden) and a
        // coverage contract (validation-namespace-coverage). Selecting only the non-coverage
        // contract must not produce a phantom zero-count summary row for the unselected
        // coverage contract — it should be entirely absent, not present with zeroed counts.
        var (exitCode, stdout, _) = RunCli(
            "--policy", CoveragePolicy, "--contract", "core-no-forbidden", "--format", "json");

        Assert.That(exitCode, Is.EqualTo(0));

        using var doc = JsonDocument.Parse(stdout);
        Assert.That(doc.RootElement.GetProperty("coverage_summary").GetArrayLength(), Is.EqualTo(0));
        Assert.That(doc.RootElement.GetProperty("coverage_findings").GetArrayLength(), Is.EqualTo(0));
    }

    [Test]
    public void CoverageSummary_ContractFilterExcludesUnselectedCoverageContract_HumanOmitsSummarySection()
    {
        var (exitCode, stdout, _) = RunCli(
            "--policy", CoveragePolicy, "--contract", "core-no-forbidden", "--format", "human");

        Assert.That(exitCode, Is.EqualTo(0));
        Assert.That(stdout, Does.Not.Contain("Coverage summary:"));
        Assert.That(stdout, Does.Not.Contain("Coverage findings:"));
    }

    [Test]
    public void CoverageSummary_JsonOutput_ExcludedItemIncludesTypedLayerExclusionProvenance()
    {
        // Regression for PR #384 review: a namespace excluded via a layer's `exclude` entry must
        // carry typed provenance for the exact layers.<name>.exclude[<index>] element (not just a
        // text reason), so JSON/Testing API consumers can locate the exact policy element -
        // mirroring PolicyConsistencyDiagnostic.PolicyLocation for the unmatched-layer-exclusion
        // finding.
        string policy = Path.Combine(
            _repoRoot, "tests", "ArchLinterNet.Cli.Tests", "TestPolicies", "coverage-layer-exclusion-provenance.yml");
        var (exitCode, stdout, _) = RunCli("--policy", policy, "--format", "json");

        Assert.That(exitCode, Is.EqualTo(0));

        using var doc = JsonDocument.Parse(stdout);
        JsonElement entry = FindSummaryEntry(doc.RootElement.GetProperty("coverage_summary"), "contracts-namespace-coverage");

        JsonElement families = entry.GetProperty("excluded_items").EnumerateArray()
            .First(item => item.GetProperty("item").GetString() == "ArchLinterNet.Core.Contracts.Families");

        JsonElement policyLocation = families.GetProperty("policy_location");
        string yamlPath = policyLocation.GetProperty("yaml_path").GetString()!;

        Assert.Multiple(() =>
        {
            Assert.That(families.GetProperty("reason").GetString(), Does.Contain("contracts"));
            Assert.That(yamlPath, Does.Contain("exclude"));
            Assert.That(yamlPath, Does.Contain("layers"));
            Assert.That(policyLocation.GetProperty("line").GetInt32(), Is.GreaterThan(0));
        });
    }

    [Test]
    public void CoverageSummary_JsonOutput_OverlappingLayerExclusionsAcrossImportedFragments_ReportsAllProvenance()
    {
        // Regression for final PR #384 review: when two independent layers - one declared in the
        // root policy, one in an imported fragment - both exclude the same namespace, the excluded
        // item must carry provenance for EVERY contributing exclude element, not just the first
        // one found. Dropping all but the first silently loses provenance for the rest of the
        // union-subtraction, especially across imported fragments where the first-found element
        // may not even belong to the same file as the second.
        string policy = Path.Combine(
            _repoRoot, "tests", "ArchLinterNet.Cli.Tests", "TestPolicies",
            "coverage-overlapping-layer-exclusion-root.yml");
        var (exitCode, stdout, _) = RunCli("--policy", policy, "--format", "json");

        Assert.That(exitCode, Is.EqualTo(0));

        using var doc = JsonDocument.Parse(stdout);
        JsonElement entry = FindSummaryEntry(doc.RootElement.GetProperty("coverage_summary"), "contracts-namespace-coverage");

        JsonElement families = entry.GetProperty("excluded_items").EnumerateArray()
            .First(item => item.GetProperty("item").GetString() == "ArchLinterNet.Core.Contracts.Families");

        JsonElement policyLocation = families.GetProperty("policy_location");
        JsonElement relatedLocations = families.GetProperty("related_policy_locations");
        string primaryPath = policyLocation.GetProperty("source_path").GetString()!;
        string relatedPath = relatedLocations[0].GetProperty("source_path").GetString()!;

        Assert.Multiple(() =>
        {
            Assert.That(families.GetProperty("reason").GetString(), Does.Contain("contracts_broad"));
            Assert.That(families.GetProperty("reason").GetString(), Does.Contain("contracts_families_only"));
            Assert.That(relatedLocations.GetArrayLength(), Is.EqualTo(1));
            Assert.That(new[] { primaryPath, relatedPath },
                Is.EquivalentTo(new[]
                {
                    "coverage-overlapping-layer-exclusion-root.yml",
                    "coverage-overlapping-layer-exclusion-fragment.yml"
                }),
                "Provenance must name both the root and the imported fragment that each contributed an exclude entry.");
        });
    }

    [Test]
    public void CoverageSummary_ContractFilterIncludesSelectedCoverageContract_StillReportsSummary()
    {
        var (exitCode, stdout, _) = RunCli(
            "--policy", CoveragePolicy, "--contract", "validation-namespace-coverage", "--format", "json");

        Assert.That(exitCode, Is.EqualTo(0));

        using var doc = JsonDocument.Parse(stdout);
        Assert.That(doc.RootElement.GetProperty("coverage_summary").GetArrayLength(), Is.EqualTo(1));
    }
}
