using System.Text.Json;
using NUnit.Framework;

namespace ArchLinterNet.Cli.Tests;

public partial class CliIntegrationTests
{
    private string CoveragePolicy => Path.Combine(
        _repoRoot, "tests", "ArchLinterNet.Cli.Tests", "TestPolicies", "coverage-policy.yml");

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

        JsonElement entry = summaries[0];
        Assert.Multiple(() =>
        {
            Assert.That(entry.GetProperty("contract").GetString(), Is.EqualTo("validation-namespace-coverage"));
            Assert.That(entry.GetProperty("contract_id").GetString(), Is.EqualTo("validation-namespace-coverage"));
            Assert.That(entry.GetProperty("scope").GetString(), Is.EqualTo("namespace"));

            JsonElement counts = entry.GetProperty("counts");
            Assert.That(counts.GetProperty("covered").GetInt32(), Is.EqualTo(0));
            Assert.That(counts.GetProperty("excluded").GetInt32(), Is.EqualTo(0));
            Assert.That(counts.GetProperty("uncovered").GetInt32(), Is.EqualTo(1));
            Assert.That(counts.GetProperty("stale").GetInt32(), Is.EqualTo(0));
            Assert.That(counts.GetProperty("unknown").GetInt32(), Is.EqualTo(0));

            JsonElement uncoveredItems = entry.GetProperty("uncovered_items");
            Assert.That(uncoveredItems.GetArrayLength(), Is.EqualTo(1));
            Assert.That(uncoveredItems[0].GetProperty("item").GetString(), Is.EqualTo("ArchLinterNet.Core.Validation"));
            Assert.That(entry.GetProperty("excluded_items").GetArrayLength(), Is.EqualTo(0));
        });
    }

    [Test]
    public void CoverageSummary_HumanOutput_PrintsSummarySectionAfterFindings()
    {
        var (exitCode, stdout, _) = RunCli("--policy", CoveragePolicy, "--format", "human");

        Assert.That(exitCode, Is.EqualTo(0));
        Assert.That(stdout, Does.Contain("Coverage findings:"));
        Assert.That(stdout, Does.Contain("Coverage summary:"));
        Assert.That(stdout, Does.Contain("covered=0 excluded=0 uncovered=1 stale=0 unknown=0"));

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
    public void CoverageSummary_NoCoverageContracts_OmitsSummarySection()
    {
        var (exitCode, stdout, _) = RunCli("--policy", _passingPolicy, "--format", "human");

        Assert.That(exitCode, Is.EqualTo(0));
        Assert.That(stdout, Does.Not.Contain("Coverage summary:"));
    }

    [Test]
    public void CoverageSummary_NoCoverageContracts_JsonStillReportsEmptyArray()
    {
        var (exitCode, stdout, _) = RunCli("--policy", _passingPolicy, "--format", "json");

        Assert.That(exitCode, Is.EqualTo(0));

        using var doc = JsonDocument.Parse(stdout);
        Assert.That(doc.RootElement.GetProperty("coverage_summary").GetArrayLength(), Is.EqualTo(0));
    }
}
