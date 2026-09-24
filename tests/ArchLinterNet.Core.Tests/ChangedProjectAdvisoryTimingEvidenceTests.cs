using System.Text.Json;
using ArchLinterNet.Core.Resolution;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
internal sealed class ChangedProjectAdvisoryTimingEvidenceTests
{
    [Test]
    public void CheckedInTimingEvidenceRetainsMeasuredFullStrictProfilesAndModeledFallbacks()
    {
        string repositoryRoot = new ArchitectureRepositoryRootResolver().Resolve();
        string path = Path.Combine(repositoryRoot, "docs", "internal", "changed-project-advisory-analysis-timing-results.json");
        Assert.That(File.Exists(path), Is.True, $"Missing checked-in #503 timing evidence at {path}.");

        ChangedProjectAdvisoryTimingEvidenceDocument evidence =
            ChangedProjectAdvisoryTimingEvidenceJson.Deserialize(File.ReadAllText(path));

        Assert.Multiple(() =>
        {
            Assert.That(evidence.Outcome, Is.EqualTo("C"));
            Assert.That(evidence.SourceIdentity, Does.Match("^[0-9a-fA-F]{40}$"),
                "Checked-in timing evidence must identify the exact clean source commit measured.");
            Assert.That(evidence.MeasurementBoundary, Does.Contain("no partial analyzer execution"));
            Assert.That(evidence.DecisionNote, Does.Contain("#991"));
            Assert.That(evidence.ScalePoints.Select(scale => scale.Label), Is.EqualTo(new[] { "8 projects", "16 projects", "32 projects" }));
        });

        foreach (ChangedProjectAdvisoryScaleEvidence scale in evidence.ScalePoints)
        {
            Assert.Multiple(() =>
            {
                Assert.That(scale.Samples, Has.Count.GreaterThanOrEqualTo(3), scale.Label);
                Assert.That(scale.Estimates.Select(estimate => estimate.ChangeClass), Is.EquivalentTo(new[]
                {
                    "leaf-project-source-change",
                    "middle-position-source-change",
                    "shared-foundation-project-source-change",
                    "policy-or-import-change",
                }), scale.Label);
                Assert.That(scale.Estimates.Single(estimate => estimate.ChangeClass == "policy-or-import-change").AffectedScopeRatio,
                    Is.EqualTo(1m), scale.Label);
                Assert.That(scale.Samples.Select(sample => sample.CanonicalResultSha256).Distinct().ToList(),
                    Has.Count.EqualTo(1), scale.Label);
            });
        }
    }

    [Test]
    public void TimingEvidenceRejectsPlaceholderSourceIdentity()
    {
        string repositoryRoot = new ArchitectureRepositoryRootResolver().Resolve();
        string path = Path.Combine(repositoryRoot, "docs", "internal", "changed-project-advisory-analysis-timing-results.json");
        string json = File.ReadAllText(path);
        string placeholderJson = json.Replace(
            "\"source_identity\": \"" + ExtractSourceIdentity(json) + "\"",
            "\"source_identity\": \"working-tree\"",
            StringComparison.Ordinal);

        Assert.That(placeholderJson, Is.Not.EqualTo(json));
        Assert.Throws<InvalidOperationException>(() => ChangedProjectAdvisoryTimingEvidenceJson.Deserialize(placeholderJson));
    }

    private static string ExtractSourceIdentity(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        return document.RootElement.GetProperty("source_identity").GetString()!;
    }
}
