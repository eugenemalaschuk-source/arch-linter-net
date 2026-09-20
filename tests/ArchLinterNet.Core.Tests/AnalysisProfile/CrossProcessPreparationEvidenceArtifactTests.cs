using ArchLinterNet.Core.Resolution;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
internal sealed class CrossProcessPreparationEvidenceArtifactTests
{
    [Test]
    public void CheckedInArtifact_ContainsBothBoundariesAndDecisionCapableEvidence()
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
                archetype.Decision.Outcome is PreparationDecisionOutcome.A or PreparationDecisionOutcome.B), Is.True);
            Assert.That(evidence.Archetypes.All(archetype => archetype.Decision.OneProcessAlternativeEvaluated), Is.True);
            Assert.That(evidence.Archetypes.All(archetype => archetype.PreparedEffect.OneProcessWorkEvidenceComplete), Is.True);
            Assert.That(evidence.Archetypes.All(archetype =>
                archetype.PreparedEffect.MissingOneProcessWorkEvidenceFamilies.Count == 0), Is.True);
            Assert.That(evidence.Archetypes.All(archetype => archetype.PreparedEffect.MeasuredOneProcessAlternativeWork.HasValue), Is.True);
            Assert.That(evidence.Archetypes.All(archetype => archetype.PreparedEffect.MaterialSavingsThreshold == 0.10m), Is.True);
            Assert.That(evidence.Archetypes.All(archetype => archetype.PreparedEffect.MateriallyCheaper), Is.True);
            Assert.That(evidence.Archetypes.All(archetype =>
                archetype.PreparedEffect.ScaleEvidence
                    .Select(point => point.Label)
                    .ToHashSet(StringComparer.Ordinal)
                    .SetEquals(new[] { "small", "medium", "large" })), Is.True);
            Assert.That(evidence.Archetypes.All(archetype =>
                archetype.PreparedEffect.ScaleEvidence.All(point =>
                    point.CommandCount == archetype.Workflow.MeasuredCommandFamilies.Count)), Is.True);
            foreach (CrossProcessPreparationEvidenceDocument archetype in evidence.Archetypes)
            {
                decimal measuredPreparationWork = archetype.PreparedEffect.MeasuredIndependentWorkflowWork *
                    archetype.PreparedEffect.RepeatedWorkShare;
                decimal expectedColdPrepareCost = measuredPreparationWork /
                    archetype.PreparedEffect.RepresentativeProcessCount;
                Assert.That(
                    archetype.PreparedEffect.ColdPrepareCost,
                    Is.EqualTo(expectedColdPrepareCost).Within(0.000000000000000000000000001m));
            }
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
                archetype.PreparedEffect.WorkMeasurementBasis.Contains("counters", StringComparison.Ordinal)), Is.True);
            Assert.That(evidence.Archetypes.All(archetype =>
                archetype.PreparedEffect.WorkMeasurementBasis.Contains("Summed", StringComparison.Ordinal) &&
                archetype.PreparedEffect.WorkMeasurementBasis.Contains("Cache miss/hit samples remain supplemental", StringComparison.Ordinal)), Is.True);
            Assert.That(json, Does.Not.Contain("private-adopter"));
            Assert.That(json, Does.Not.Contain("eugen"));
            Assert.That(evidence.Archetypes.SelectMany(archetype => archetype.Processes)
                .Any(process => process.Sample.Run.PreparedStateMode == "one_process_shared"), Is.True);
        });
    }
}
