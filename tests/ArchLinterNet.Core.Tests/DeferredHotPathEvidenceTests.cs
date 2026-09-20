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
}
