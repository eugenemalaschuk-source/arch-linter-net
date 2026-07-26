using System.Text.Json;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

public sealed partial class ArchitectureDiagnosticFormatterTests
{
    [Test]
    public void FormatCoverageAndPolicyResults_IncludesSortedDetails()
    {
        var summary = new ArchitectureCoverageSummary(
            "coverage", "coverage-id", "namespace",
            new ArchitectureCoverageSummaryCounts(1, 1, 1, 1, 1),
            _excludedCoverageItems,
            _uncoveredCoverageItems,
            _staleCoverageItems,
            _unknownCoverageItems,
            _coveredCoverageItems)
        {
            OptionalEmptyItems =
            [
                new ArchitectureCoverageSummaryOptionalEmptyItem(
                    "rule:forbidden:future", "Future module is planned.", "future")
                {
                    ContractId = "rule",
                    Input = "forbidden",
                    Layer = "future"
                }
            ]
        };
        var policy = new PolicyConsistencyDiagnostic(
            "policy", "policy-id", "duplicate", "conflicting rules",
            _firstPolicyId, _policyContractNames, _policyLayers)
        { RepresentativeType = "Core.Representative" };

        Assert.That(_formatter.FormatCoverageForHumans(_coverageFinding), Does.StartWith("Coverage findings:"));
        string humanSummary = _formatter.FormatCoverageSummaryForHumans(new List<ArchitectureCoverageSummary> { summary });
        Assert.That(humanSummary, Does.Contain("covered=1 excluded=1 uncovered=1 stale=1 unknown=1"));
        Assert.That(humanSummary, Does.Contain("uncovered: a-uncovered (a-evidence)"));
        Assert.That(_formatter.FormatPolicyConsistencyForHumans(new List<PolicyConsistencyDiagnostic> { policy }),
            Does.Contain("Core.Representative").Or.Contain("conflicting rules"));

        using var json = JsonDocument.Parse(_formatter.FormatResultForCiArtifacts(
            "strict", false, Array.Empty<ArchitectureViolation>(), Array.Empty<string>(),
            policyConsistencyFindings: new List<PolicyConsistencyDiagnostic> { policy },
            coverageSummaries: new List<ArchitectureCoverageSummary> { summary }));
        Assert.That(json.RootElement.GetProperty("policy_consistency_findings")[0]
            .GetProperty("representative_type").GetString(), Is.EqualTo("Core.Representative"));
        Assert.That(json.RootElement.GetProperty("coverage_summary")[0].GetProperty("covered_items")[0]
            .GetProperty("item").GetString(), Is.EqualTo("d-covered"));
        Assert.That(json.RootElement.GetProperty("coverage_summary")[0].GetProperty("excluded_items")[0]
            .TryGetProperty("evidence", out _), Is.False);
        JsonElement optionalItem = json.RootElement.GetProperty("coverage_summary")[0]
            .GetProperty("optional_empty_items")[0];
        Assert.Multiple(() =>
        {
            Assert.That(optionalItem.GetProperty("contract_id").GetString(), Is.EqualTo("rule"));
            Assert.That(optionalItem.GetProperty("input").GetString(), Is.EqualTo("forbidden"));
            Assert.That(optionalItem.GetProperty("layer").GetString(), Is.EqualTo("future"));
        });
    }
}
