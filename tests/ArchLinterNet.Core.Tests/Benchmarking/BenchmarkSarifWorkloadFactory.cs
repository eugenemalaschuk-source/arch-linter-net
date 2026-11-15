using System.Text.Json;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;

namespace ArchLinterNet.Core.Tests;

internal static class BenchmarkSarifWorkloadFactory
{
    private const int FixedFindingCount = 32;

    public static string FormatResult(BenchmarkMaterializedFixture fixture)
    {
        BenchmarkDimensionSet dimensions = fixture.Definition.Dimensions;
        if (dimensions.ReportRuleCount == 0)
        {
            throw new ArgumentException("A SARIF workload requires at least one report rule.", nameof(fixture));
        }

        string reportRulesPath = Path.Combine(fixture.Root, ".benchmark", "report-rules.json");
        using JsonDocument reportRules = JsonDocument.Parse(File.ReadAllText(reportRulesPath));
        string[] ruleIds = reportRules.RootElement.EnumerateArray()
            .Select(rule => rule.GetProperty("id").GetString()!)
            .ToArray();
        if (ruleIds.Length != dimensions.ReportRuleCount)
        {
            throw new InvalidOperationException(
                $"Expected {dimensions.ReportRuleCount} synthetic SARIF rules but found {ruleIds.Length}.");
        }

        // SARIF only includes rules referenced by findings. Keep result volume fixed while cycling
        // through every declared rule so rule-table scale remains an independent dimension.
        ArchitectureViolation[] findings = Enumerable.Range(0, FixedFindingCount)
            .Select(index => new ArchitectureViolation(
                $"Synthetic report contract {index % ruleIds.Length + 1:000}",
                ruleIds[index % ruleIds.Length],
                $"Synthetic.Project001.Type{index:000}",
                "Synthetic.Forbidden",
                [$"Synthetic.Reference{index:000}"]))
            .ToArray();

        return ArchitectureSarifFormatter.FormatResultAsSarif(
            "strict",
            findings,
            Array.Empty<ArchitectureCycleFinding>(),
            Array.Empty<BuildStatePreflightDiagnostic>(),
            "synthetic-benchmark");
    }
}
