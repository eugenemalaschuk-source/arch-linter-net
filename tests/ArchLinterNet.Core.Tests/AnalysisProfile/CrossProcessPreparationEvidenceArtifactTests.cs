using ArchLinterNet.Core.Resolution;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
internal sealed class CrossProcessPreparationEvidenceArtifactTests
{
    [Test]
    public void CheckedInArtifact_ContainsBothBoundariesAndFailsClosedWithoutProcessBoundTiming()
    {
        string repositoryRoot = new ArchitectureRepositoryRootResolver().Resolve();
        string path = Path.Combine(repositoryRoot, "docs", "internal", "prepared-analysis-reuse-evidence.json");
        Assert.That(File.Exists(path), Is.True, $"Missing checked-in issue #493 artifact: {path}");

        string json = File.ReadAllText(path);
        CrossProcessPreparationEvidenceBundle evidence = CrossProcessPreparationEvidenceBundleJson.Deserialize(json);

        Assert.Multiple(() =>
        {
            Assert.That(evidence.Archetypes, Has.Count.EqualTo(2));
            Assert.That(evidence.Archetypes.Select(archetype => archetype.Workflow.PreparationBoundary),
                Is.EquivalentTo(new[] { PreparationBoundaryKind.MsBuildReceipt, PreparationBoundaryKind.StagedAssemblies }));
            Assert.That(evidence.Archetypes.All(archetype =>
                archetype.Decision.Outcome == PreparationDecisionOutcome.C), Is.True);
            Assert.That(evidence.Archetypes.All(archetype => !archetype.Decision.OneProcessAlternativeEvaluated), Is.True);
            Assert.That(evidence.Archetypes.All(archetype => archetype.PreparedEffect.OneProcessWorkEvidenceComplete), Is.True);
            Assert.That(evidence.Archetypes.All(archetype =>
                archetype.PreparedEffect.MissingOneProcessWorkEvidenceFamilies.Count == 0), Is.True);
            Assert.That(evidence.Archetypes.All(archetype => !archetype.PreparedEffect.TimingEvidenceComplete), Is.True);
            Assert.That(evidence.Archetypes.All(archetype =>
                archetype.PreparedEffect.MissingTimingEvidenceFamilies.Contains("architecture_health", StringComparer.Ordinal) &&
                archetype.PreparedEffect.MissingTimingEvidenceFamilies.Contains("change_snapshot", StringComparer.Ordinal) &&
                archetype.PreparedEffect.MissingTimingEvidenceFamilies.Contains("measure", StringComparer.Ordinal) &&
                archetype.PreparedEffect.MissingTimingEvidenceFamilies.Contains("no_new_debt", StringComparer.Ordinal) &&
                archetype.PreparedEffect.MissingTimingEvidenceFamilies.Contains("topology", StringComparer.Ordinal)), Is.True);
            Assert.That(evidence.Archetypes.All(archetype =>
                archetype.PreparedEffect.CostModelUnit == PreparedEffectContract.CostModelUnitMilliseconds), Is.True);
            Assert.That(evidence.Archetypes.All(archetype => !archetype.PreparedEffect.MeasuredOneProcessAlternativeWork.HasValue), Is.True);
            Assert.That(evidence.Archetypes.All(archetype => archetype.PreparedEffect.MaterialSavingsThreshold == 0.10m), Is.True);
            Assert.That(evidence.Archetypes.All(archetype =>
                archetype.Decision.Outcome != PreparationDecisionOutcome.A || archetype.PreparedEffect.MateriallyCheaper), Is.True);
            Assert.That(evidence.Archetypes.All(archetype =>
                archetype.PreparedEffect.ScaleEvidence
                    .Select(point => point.Label)
                    .ToHashSet(StringComparer.Ordinal)
                    .SetEquals(new[] { "small", "medium", "large" })), Is.True);
            Assert.That(evidence.Archetypes.All(archetype =>
                archetype.PreparedEffect.ScaleEvidence.All(point =>
                    point.CommandCount == archetype.Workflow.MeasuredCommandFamilies.Count)), Is.True);
            Assert.That(evidence.Archetypes.All(archetype =>
                archetype.PreparedEffect.ScaleEvidenceBasis.Contains("Measured analysis-profile/v1 counters", StringComparison.Ordinal)), Is.True);
            Assert.That(evidence.Archetypes.All(archetype =>
                archetype.PreparedEffect.ScaleEvidenceBasis.Contains("attribution", StringComparison.Ordinal)), Is.True);
            Assert.That(evidence.Archetypes.SelectMany(archetype => archetype.PreparedEffect.ScaleEvidence)
                .All(point => point.MeasurementBasis.Contains("Stopwatch", StringComparison.Ordinal)), Is.True);
            Assert.That(evidence.Archetypes.SelectMany(archetype => archetype.PreparedEffect.ScaleEvidence)
                .All(point => point.MeasurementBasis.Contains("milliseconds", StringComparison.Ordinal)), Is.True);
            Assert.That(evidence.Archetypes.SelectMany(archetype => archetype.PreparedEffect.ScaleEvidence)
                .All(point => point.PerConsumerLoadAuthorizationCost > 0), Is.True);
            Assert.That(evidence.Archetypes.SelectMany(archetype => archetype.PreparedEffect.ScaleEvidence)
                .All(point => point.MeasurementBasis.Contains("scale-specific", StringComparison.Ordinal)), Is.True);
            Assert.That(evidence.Archetypes.SelectMany(archetype => archetype.PreparedEffect.ScaleEvidence)
                .All(point => !point.TimingEvidenceComplete &&
                    point.MissingTimingEvidenceFamilies.Contains("architecture_health", StringComparer.Ordinal) &&
                    point.MissingTimingEvidenceFamilies.Contains("change_snapshot", StringComparer.Ordinal) &&
                    point.MissingTimingEvidenceFamilies.Contains("measure", StringComparer.Ordinal) &&
                    point.MissingTimingEvidenceFamilies.Contains("no_new_debt", StringComparer.Ordinal) &&
                    point.MissingTimingEvidenceFamilies.Contains("topology", StringComparer.Ordinal) &&
                    point.ExpectedLocalSpeedup == 1m), Is.True);
            Assert.That(evidence.Archetypes.All(archetype => archetype.PreparedEffect.ColdPrepareCost == 0), Is.True);
            Assert.That(evidence.Archetypes.All(archetype =>
                archetype.PreparedEffect.RepresentativeProcessCount == archetype.Workflow.MeasuredCommandFamilies.Count), Is.True);
            Assert.That(evidence.Archetypes.All(archetype =>
                archetype.CandidateIndependentProcesses.Count > archetype.PreparedEffect.RepresentativeProcessCount), Is.True);
            Assert.That(evidence.Archetypes.All(archetype =>
                archetype.RepresentativeCandidateIndependentProcesses
                    .Select(process => process.Identity.Projection.CommandFamily)
                    .ToHashSet(StringComparer.Ordinal)
                    .SetEquals(archetype.Workflow.MeasuredCommandFamilies)), Is.True);
            Assert.That(evidence.Archetypes.All(archetype =>
                archetype.PreparedEffect.WorkMeasurementBasis.Contains("Stopwatch", StringComparison.Ordinal)), Is.True);
            Assert.That(evidence.Archetypes.All(archetype =>
                archetype.PreparedEffect.WorkMeasurementBasis.Contains("deterministic counters", StringComparison.Ordinal) &&
                archetype.PreparedEffect.WorkMeasurementBasis.Contains("Cache miss/hit samples remain supplemental", StringComparison.Ordinal)), Is.True);
            Assert.That(json, Does.Not.Contain("private-adopter"));
            Assert.That(json, Does.Not.Contain("eugen"));
            Assert.That(evidence.Archetypes.SelectMany(archetype => archetype.Processes)
                .Any(process => process.Sample.Run.PreparedStateMode == "one_process_shared"), Is.True);
            Assert.That(evidence.Archetypes.SelectMany(archetype => archetype.Processes)
                .Where(process => process.Sample.Run.PreparedStateMode == "one_process_process_bound")
                .All(process => process.Sample.WallClock.Status == BenchmarkMeasurementStatus.Unavailable), Is.True);
        });
    }
}
