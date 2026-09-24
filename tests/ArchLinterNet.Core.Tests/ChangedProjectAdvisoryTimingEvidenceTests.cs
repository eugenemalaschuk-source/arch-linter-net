using System.Text.Json;
using ArchLinterNet.Core.Resolution;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
internal sealed class ChangedProjectAdvisoryTimingEvidenceTests
{
    private static readonly string[] _unscaledRepositoryWidePhaseNames =
    [
        "load_and_setup", "build_state_preflight", "post_processing", "repository_metrics",
    ];

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
                decimal contractChecksMedian = Median(scale.Samples.Select(sample =>
                    sample.TopLevelPhaseMilliseconds[ChangedProjectAdvisoryScaleEvidence.ProjectScaledUpperBoundPhaseName]));
                Assert.That(scale.ProjectScaledUpperBoundPhaseMedianMilliseconds, Is.EqualTo(contractChecksMedian), scale.Label);
                Assert.That(scale.UnscaledPhaseResidualMilliseconds,
                    Is.EqualTo(scale.FullValidationMedianMilliseconds - contractChecksMedian), scale.Label);
                Assert.That(scale.Samples.All(sample => _unscaledRepositoryWidePhaseNames.All(phaseName =>
                    sample.TopLevelPhaseMilliseconds.ContainsKey(phaseName))), Is.True,
                    $"Setup, preflight, post-processing, and repository-metrics phases must remain in the unscaled residual for {scale.Label}.");
                Assert.That(scale.Estimates.All(estimate => estimate.ModeledUpperBoundReductionPercent <=
                    (contractChecksMedian / scale.FullValidationMedianMilliseconds * 100m) + 0.01m), Is.True, scale.Label);
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

    private static decimal Median(IEnumerable<decimal> values)
    {
        decimal[] sorted = values.Order().ToArray();
        int middle = sorted.Length / 2;
        return sorted.Length % 2 == 0
            ? (sorted[middle - 1] + sorted[middle]) / 2
            : sorted[middle];
    }
}
