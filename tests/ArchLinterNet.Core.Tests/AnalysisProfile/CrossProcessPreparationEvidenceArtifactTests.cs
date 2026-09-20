using ArchLinterNet.Core.Resolution;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
internal sealed class CrossProcessPreparationEvidenceArtifactTests
{
    [Test]
    public void CheckedInArtifact_ContainsBothBoundariesAndValidatesAsPreImplementationEvidence()
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
            Assert.That(evidence.Archetypes.All(archetype => archetype.Decision.OneProcessAlternativeEvaluated), Is.True);
            Assert.That(evidence.Archetypes.All(archetype =>
                archetype.PreparedEffect.WorkMeasurementBasis.Contains("counters", StringComparison.Ordinal)), Is.True);
            Assert.That(json, Does.Not.Contain("private-adopter"));
            Assert.That(json, Does.Not.Contain("eugen"));
            Assert.That(evidence.Archetypes.SelectMany(archetype => archetype.Processes)
                .Any(process => process.Sample.Run.PreparedStateMode == "one_process_shared"), Is.True);
        });
    }
}
