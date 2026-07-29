using System.Text.Json;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Resolution;
using ArchLinterNet.Testing;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class CheckpointAAdoptionAcceptanceTests
{
    [Test]
    public void ScenarioManifest_ContainsRequiredSyntheticShapesAndNonReleaseBoundary()
    {
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(ManifestPath()));
        JsonElement root = document.RootElement;
        string[] shapes = root.GetProperty("fixtures").EnumerateArray()
            .Select(fixture => fixture.GetProperty("shape").GetString()!)
            .OrderBy(shape => shape, StringComparer.Ordinal)
            .ToArray();
        string[] reusers = root.GetProperty("reusers").EnumerateArray()
            .Select(reuser => reuser.GetString()!)
            .ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(root.GetProperty("schema").GetString(), Is.EqualTo("adoption-acceptance-corpus/v1"));
            Assert.That(root.GetProperty("checkpoint").GetString(), Is.EqualTo("A"));
            Assert.That(root.GetProperty("release_gate").GetBoolean(), Is.False);
            Assert.That(root.GetProperty("synthetic_identities_only").GetBoolean(), Is.True);
            Assert.That(shapes, Is.EqualTo(new[] { "clean-checkout", "migration", "multi-host", "multi-project", "small" }));
            Assert.That(reusers, Is.EqualTo(new[] { "#374", "#411", "#366" }));
            Assert.That(root.GetProperty("scenarios").GetArrayLength(), Is.GreaterThanOrEqualTo(8));
        });
    }

    [Test]
    public void ImportedMigrationFixture_LoadsThroughThePublicPolicyLoader()
    {
        string root = new ArchitectureRepositoryRootResolver().Resolve();
        string policy = Path.Combine(root, "tests", "ArchLinterNet.Cli.Tests", "TestPolicies", "imported-provenance-root.yml");

        ArchitectureContractDocument document = new ArchitecturePolicyDocumentLoader().Load(policy);

        Assert.That(document.Analysis.TargetAssemblies, Is.Not.Empty);
    }

    [Test]
    public void TestingSnapshot_UsesOneAnalysisSessionForStrictAndAudit()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"arch-linter-checkpoint-a-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            string policyPath = Path.Combine(directory, "dependencies.arch.yml");
            File.WriteAllText(policyPath, """
                version: 1
                name: Synthetic checkpoint A adopter

                layers:
                  execution:
                    namespace: ArchLinterNet.Core.Execution

                analysis:
                  target_assemblies: [ArchLinterNet.Core]
                """);

            var builder = new ArchitectureValidationBuilder(policyPath);
            using ArchitectureValidationSnapshotSession session = builder.CreateSnapshot();

            Assert.Multiple(() =>
            {
                Assert.That(session.ValidateStrict().Passed, Is.True);
                Assert.That(session.ValidateAudit().Passed, Is.True);
                Assert.That(session.Counters.PolicyCompositions, Is.EqualTo(1));
                Assert.That(session.Counters.ProjectGraphEvaluations, Is.EqualTo(1));
            });
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static string ManifestPath()
    {
        string root = new ArchitectureRepositoryRootResolver().Resolve();
        return Path.Combine(root, "tests", "ArchLinterNet.Core.Tests", "AdoptionAcceptance", "CheckpointAScenarioManifest.json");
    }
}
