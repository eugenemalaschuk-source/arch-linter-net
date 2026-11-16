using System.Text.Json;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests.Benchmarking;

[TestFixture]
[NonParallelizable]
public sealed class BenchmarkAuditReportWorkloadTests
{
    [TestCase(1)]
    [TestCase(8)]
    [TestCase(32)]
    public void ReportRuleAndFindingDimensions_IndependentlyShapeSarifOutput(int ruleCount)
    {
        BenchmarkWorkloadDefinition workload = BenchmarkWorkloadGenerator.Create(
            $"synthetic-report-dimensions-{ruleCount}",
            BenchmarkTopologyShape.Linear,
            new BenchmarkDimensionSet
            {
                ReportRuleCount = ruleCount,
                FindingCandidates = 12
            });
        using BenchmarkMaterializedFixture fixture = BenchmarkFixtureMaterializer.Materialize(workload);
        string first = BenchmarkSarifWorkloadFactory.FormatResult(fixture);
        string second = BenchmarkSarifWorkloadFactory.FormatResult(fixture);
        using var output = JsonDocument.Parse(first);
        JsonElement run = output.RootElement.GetProperty("runs")[0];
        JsonElement rules = run.GetProperty("tool").GetProperty("driver").GetProperty("rules");
        JsonElement results = run.GetProperty("results");
        string[] referencedRules = results.EnumerateArray()
            .Select(result => result.GetProperty("ruleId").GetString()!)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(ruleId => ruleId, StringComparer.Ordinal)
            .ToArray();
        string[] declaredRules = rules.EnumerateArray()
            .Select(rule => rule.GetProperty("id").GetString()!)
            .OrderBy(ruleId => ruleId, StringComparer.Ordinal)
            .ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(rules.GetArrayLength(), Is.EqualTo(ruleCount));
            Assert.That(results.GetArrayLength(), Is.EqualTo(32));
            Assert.That(referencedRules, Is.EqualTo(declaredRules));
            Assert.That(BenchmarkIdentity.Sha256(BenchmarkIdentity.NormalizeJson(first)),
                Is.EqualTo(BenchmarkIdentity.Sha256(BenchmarkIdentity.NormalizeJson(second))));
        });
    }
}
