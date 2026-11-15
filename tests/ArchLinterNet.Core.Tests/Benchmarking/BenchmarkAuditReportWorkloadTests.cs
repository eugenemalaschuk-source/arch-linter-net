using System.Text.Json;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
[NonParallelizable]
public sealed class BenchmarkAuditReportWorkloadTests
{
    [TestCase(2, 12)]
    [TestCase(5, 12)]
    [TestCase(8, 12)]
    [TestCase(32, 12)]
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
        string first = BenchmarkSarifWorkloadFactory.FormatResult(fixture);
        string second = BenchmarkSarifWorkloadFactory.FormatResult(fixture);
        using JsonDocument output = JsonDocument.Parse(first);
        JsonElement run = output.RootElement.GetProperty("runs")[0];
        JsonElement rules = run.GetProperty("tool").GetProperty("driver").GetProperty("rules");
        JsonElement results = run.GetProperty("results");

        Assert.Multiple(() =>
        {
            Assert.That(rules.GetArrayLength(), Is.EqualTo(ruleCount));
            Assert.That(results.GetArrayLength(), Is.EqualTo(Math.Max(ruleCount, findingCount)));
            Assert.That(BenchmarkIdentity.Sha256(BenchmarkIdentity.NormalizeJson(first)),
                Is.EqualTo(BenchmarkIdentity.Sha256(BenchmarkIdentity.NormalizeJson(second))));
        });
    }
}
