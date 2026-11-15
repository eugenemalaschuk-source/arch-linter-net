using System.Text.Json;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
[NonParallelizable]
public sealed class BenchmarkAuditReportWorkloadTests
{
    [TestCase(2, 12)]
    [TestCase(5, 12)]
    [TestCase(8, 12)]
    public void ReportRuleAndFindingDimensions_IndependentlyShapeSarifOutput(int ruleCount, int findingCount)
    {
        BenchmarkWorkloadDefinition workload = BenchmarkWorkloadGenerator.Create(
            $"synthetic-report-dimensions-{ruleCount}",
            BenchmarkTopologyShape.Linear,
            new BenchmarkDimensionSet
            {
                ReportRuleCount = ruleCount,
                FindingCandidates = findingCount,
            });
        using BenchmarkMaterializedFixture fixture = BenchmarkFixtureMaterializer.Materialize(workload);
        string reportRulePath = Path.Combine(fixture.Root, ".benchmark", "report-rules.json");
        using JsonDocument reportRules = JsonDocument.Parse(File.ReadAllText(reportRulePath));
        string[] ruleIds = reportRules.RootElement.EnumerateArray()
            .Select(rule => rule.GetProperty("id").GetString()!)
            .ToArray();
        ArchitectureViolation[] findings = Enumerable.Range(0, findingCount)
            .Select(index => new ArchitectureViolation(
                $"Synthetic report contract {index % ruleCount + 1:000}",
                ruleIds[index % ruleCount],
                $"Synthetic.Project001.Type{index:000}",
                "Synthetic.Forbidden",
                [$"Synthetic.Reference{index:000}"]))
            .ToArray();

        string first = ArchitectureSarifFormatter.FormatResultAsSarif(
            "strict",
            findings,
            Array.Empty<ArchitectureCycleFinding>(),
            Array.Empty<BuildStatePreflightDiagnostic>(),
            "synthetic-benchmark");
        string second = ArchitectureSarifFormatter.FormatResultAsSarif(
            "strict",
            findings,
            Array.Empty<ArchitectureCycleFinding>(),
            Array.Empty<BuildStatePreflightDiagnostic>(),
            "synthetic-benchmark");
        using JsonDocument output = JsonDocument.Parse(first);
        JsonElement run = output.RootElement.GetProperty("runs")[0];
        JsonElement rules = run.GetProperty("tool").GetProperty("driver").GetProperty("rules");
        JsonElement results = run.GetProperty("results");

        Assert.Multiple(() =>
        {
            Assert.That(rules.GetArrayLength(), Is.EqualTo(ruleCount));
            Assert.That(results.GetArrayLength(), Is.EqualTo(findingCount));
            Assert.That(BenchmarkIdentity.Sha256(BenchmarkIdentity.NormalizeJson(first)),
                Is.EqualTo(BenchmarkIdentity.Sha256(BenchmarkIdentity.NormalizeJson(second))));
        });
    }
}
