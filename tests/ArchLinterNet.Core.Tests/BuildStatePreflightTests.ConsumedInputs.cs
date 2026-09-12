using ArchLinterNet.Core.BuildState;
using ArchLinterNet.Core.Model;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[Category("E2E")]
public sealed class BuildStatePreflightConsumedInputsTests : BuildStatePreflightTestSupport
{
    [Test]
    public void Evaluate_CurrentArtifact_ReportsExactConsumedProjectInputs()
    {
        string projectPath = CreateProjectFixture("ConsumedInputs", "class C {}");
        string assemblyPath = CreateFakeAssemblyFile("ConsumedInputs");
        string fingerprint = BuildStateCanonicalHasher.ComputeBuildInputFingerprint(projectPath, RepositoryRoot);
        EvaluatedBuildInputManifestV1 manifest = EvaluatedBuildInputManifestCollector.Collect(projectPath, RepositoryRoot);
        BuildReceiptStore.Write(assemblyPath, new BuildReceiptV1(
            projectPath, "ConsumedInputs", "Debug", "net10.0", fingerprint,
            BuildStateCanonicalHasher.ComputeContentDigest(assemblyPath), manifest.Digest, manifest.Eligibility,
            manifest.IneligibilityReasons));

        BuildStatePreflightResult result = BuildStatePreflightEvaluator.Evaluate(new BuildStatePreflightRequest(
            RepositoryRoot, SingleProjectDiscovery(projectPath, "ConsumedInputs"), SingleAssemblyResolution(assemblyPath),
            BuildPreparationMode.Ordinary));

        Assert.That(result.ConsumedInputPaths, Is.EquivalentTo(new[]
        {
            Path.GetFullPath(projectPath),
            Path.Combine(Path.GetDirectoryName(projectPath)!, "Class1.cs"),
        }));
    }

    [Test]
    public void Evaluate_MalformedReceipt_IsUnverifiableAndNeverCurrent()
    {
        string projectPath = CreateProjectFixture("Fixture", "class C {}");
        string assemblyPath = CreateFakeAssemblyFile("Fixture");
        File.WriteAllText(BuildReceiptStore.ReceiptPathFor(assemblyPath), "{ malformed receipt");

        BuildStatePreflightResult result = BuildStatePreflightEvaluator.Evaluate(new BuildStatePreflightRequest(
            RepositoryRoot, SingleProjectDiscovery(projectPath, "Fixture"), SingleAssemblyResolution(assemblyPath),
            BuildPreparationMode.Ordinary));

        BuildStatePreflightDiagnostic diagnostic = result.Diagnostics.Single();
        Assert.Multiple(() =>
        {
            Assert.That(diagnostic.State, Is.EqualTo(BuildStatePreflightState.UnverifiableArtifact));
            Assert.That(diagnostic.State, Is.Not.EqualTo(BuildStatePreflightState.Current));
            Assert.That(diagnostic.Evidence.Detail, Does.Contain("No ArchLinterNet build receipt"));
        });
    }
}
