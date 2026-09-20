using System.Text.Json;
using ArchLinterNet.Core.Resolution;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class DeferredHotPathEvidenceTests
{
    private static readonly string[] _expectedFindingIds =
    [
        "type-layer-membership-amplification",
        "repeated-selector-classification",
        "graph-reachability-witness",
        "cross-process-preparation",
        "exact-request-cache-eligibility",
        "public-api-cross-process-reuse",
        "consumer-forced-sequential-vs-bounded-parallelism",
    ];

    [Test]
    public void CheckedInEvidence_RecordsExactlyOneOutcomeForEveryFinding()
    {
        DeferredHotPathEvidenceDocument document = Load();
        IReadOnlyList<DeferredHotPathFindingEvidence> findings = document.Findings;

        Assert.Multiple(() =>
        {
            Assert.That(document.EvidenceSchemaId, Is.EqualTo(DeferredHotPathEvidenceDocument.SchemaId));
            Assert.That(findings.Select(finding => finding.Id), Is.EqualTo(_expectedFindingIds));
            Assert.That(findings.Select(finding => finding.Id).Distinct().Count(), Is.EqualTo(_expectedFindingIds.Length));
            Assert.That(findings.Select(finding => finding.Outcome), Has.All.Matches<string>(outcome => outcome is "A" or "B" or "C" or "D"));
            Assert.That(document.SourceIdentity, Does.Contain("synthetic"));
            Assert.That(document.ToolIdentity, Does.Contain("analysis-profile/v1"));
        });

        foreach (DeferredHotPathFindingEvidence finding in findings)
        {
            Assert.That(finding.Measurements, Is.Not.Null, finding.Id);
            foreach (DeferredHotPathMeasurement measurement in finding.Measurements)
            {
                Assert.Multiple(() =>
                {
                    Assert.That(measurement.WorkloadId, Does.StartWith("synthetic-"));
                    Assert.That(measurement.CanonicalResultSha256, Does.Match("^[0-9a-f]{64}$"));
                    Assert.That(measurement.CompletionStatus, Is.EqualTo("Success"));
                    Assert.That(measurement.ExitCode, Is.EqualTo(0));
                    Assert.That(measurement.RawAnalysisProfile.GetProperty("SchemaId").GetString(), Is.EqualTo("analysis-profile/v1"));
                    Assert.That(measurement.RawAnalysisProfile.ToString(), Does.Not.Contain("/Users/"));
                    Assert.That(measurement.RawAnalysisProfile.ToString(), Does.Not.Contain("private-adopter"));
                });
            }
        }

        Assert.Multiple(() =>
        {
            Assert.That(findings.Take(5).All(finding => finding.Measurements.Count >= 3), Is.True);
            Assert.That(findings.Single(finding => finding.Id == "type-layer-membership-amplification").Measurements,
                Has.All.Matches<DeferredHotPathMeasurement>(measurement =>
                    measurement.ObservedCounter == "phase.selector_predicate_evaluation.count"
                    && measurement.ObservedCounterValue is > 0));
            Assert.That(SelectorCounts(findings, "P=projects"), Is.EqualTo([112, 224, 448]));
            Assert.That(SelectorCounts(findings, "T=types_per_project"), Is.EqualTo([112, 224, 448]));
            Assert.That(SelectorCounts(findings, "L=layers"), Is.EqualTo([128, 224, 416]));
            Assert.That(SelectorCounts(findings, "S=selector_terms_per_layer"), Is.EqualTo([224, 224, 224]));
            Assert.That(findings.Single(finding => finding.Id == "graph-reachability-witness").TopologyEvidence
                .Select(topology => topology.Shape)
                .Distinct(),
                Is.EquivalentTo(["Linear", "WideFanOutFanIn", "Diamond", "Dense", "CyclicScc"]));
            Assert.That(findings.Single(finding => finding.Id == "graph-reachability-witness").Measurements
                .Select(measurement => measurement.WorkloadId)
                .Distinct()
                .Count(), Is.EqualTo(12));
            Assert.That(findings.Single(finding => finding.Id == "consumer-forced-sequential-vs-bounded-parallelism").Measurements
                .Count, Is.EqualTo(24));
            Assert.That(findings.Single(finding => finding.Id == "consumer-forced-sequential-vs-bounded-parallelism").Measurements
                .GroupBy(measurement => measurement.WorkloadId)
                .All(pair => pair.Count() == 2
                    && pair.Select(measurement => measurement.CanonicalResultSha256).Distinct().Count() == 1), Is.True);
            Assert.That(findings.Single(finding => finding.Id == "consumer-forced-sequential-vs-bounded-parallelism").Measurements
                .Where(measurement => measurement.ExecutionVariant == "bounded-default")
                .All(measurement => measurement.RawAnalysisProfile.GetProperty("Counters").GetProperty("Concurrency")
                    .GetProperty("Status").GetString() == "Active"), Is.True);
            Assert.That(findings.Single(finding => finding.Id == "cross-process-preparation").Routing, Does.Contain("#492"));
            Assert.That(findings.Single(finding => finding.Id == "exact-request-cache-eligibility").Routing, Does.Contain("#675"));
            Assert.That(findings.Single(finding => finding.Id == "public-api-cross-process-reuse").Routing, Does.Contain("#498"));
        });
    }

    private static DeferredHotPathEvidenceDocument Load()
    {
        string root = new ArchitectureRepositoryRootResolver().Resolve();
        string path = Path.Combine(root, "docs", "internal", "deferred-hot-path-analysis-results.json");
        Assert.That(File.Exists(path), Is.True, $"Missing checked-in evidence at {path}.");
        return DeferredHotPathBenchmarkEvidenceJson.Deserialize(File.ReadAllText(path));
    }

    private static int[] SelectorCounts(
        IReadOnlyList<DeferredHotPathFindingEvidence> findings,
        string scaleDimension) => findings
        .Single(finding => finding.Id == "type-layer-membership-amplification")
        .Measurements
        .Where(measurement => measurement.ScaleDimension == scaleDimension)
        .OrderBy(measurement => measurement.ScaleValue)
        .Select(measurement => measurement.ObservedCounterValue!.Value)
        .ToArray();
}
