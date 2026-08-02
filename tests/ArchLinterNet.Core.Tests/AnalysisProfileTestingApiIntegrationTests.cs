using ArchLinterNet.Core.Profiling;
using ArchLinterNet.Testing;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

// Issue #374: Testing API mirror of the CLI's --profile option. See
// openspec/specs/analysis-profile/spec.md, "Testing API exposes the same profile semantics as the
// CLI".
[TestFixture]
public sealed class AnalysisProfileTestingApiIntegrationTests
{
    private static string WriteHarmlessPolicy()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"arch-linter-analysis-profile-testing-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        string policyPath = Path.Combine(tempDir, "dependencies.arch.yml");
        File.WriteAllText(policyPath, """
            version: 1
            name: Test

            layers:
              execution:
                namespace: ArchLinterNet.Core.Execution

            analysis:
              target_assemblies: [ArchLinterNet.Core]
            """);
        return policyPath;
    }

    [Test]
    public void ValidateStrict_WithoutWithProfile_ProfileIsNull()
    {
        var builder = new ArchitectureValidationBuilder(WriteHarmlessPolicy());

        ArchitectureValidationResult result = builder.ValidateStrict();

        Assert.That(result.Profile, Is.Null);
    }

    [Test]
    public void ValidateStrict_WithProfile_CountersProveOneCompositionOneEvaluation()
    {
        var builder = new ArchitectureValidationBuilder(WriteHarmlessPolicy()).WithProfile();

        ArchitectureValidationResult result = builder.ValidateStrict();

        Assert.That(result.Profile, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(result.Profile!.Counters.PolicyCompositions, Is.EqualTo(1));
            Assert.That(result.Profile.Counters.ProjectGraphEvaluations, Is.EqualTo(1));
            Assert.That(result.Profile.Counters.SnapshotMaterializations, Is.EqualTo(1));
            Assert.That(result.Profile.CompletionStatus, Is.EqualTo(AnalysisProfileCompletionStatus.Success));
            Assert.That(result.Profile.CancellationObserved, Is.False);
            Assert.That(result.Profile.SchemaId, Is.EqualTo(AnalysisProfileId.V1));
            Assert.That(result.Profile.Measurements, Is.Not.Null);
        });
    }

    [Test]
    public void CreateSnapshot_WithProfile_SharesOneSnapshotCountersAcrossBothModes()
    {
        var builder = new ArchitectureValidationBuilder(WriteHarmlessPolicy()).WithProfile();

        using ArchitectureValidationSnapshotSession session = builder.CreateSnapshot();
        ArchitectureValidationResult strict = session.ValidateStrict();
        ArchitectureValidationResult audit = session.ValidateAudit();

        Assert.Multiple(() =>
        {
            Assert.That(strict.Profile, Is.Not.Null);
            Assert.That(audit.Profile, Is.Not.Null);
            Assert.That(strict.Profile!.Counters.PolicyCompositions, Is.EqualTo(1));
            Assert.That(audit.Profile!.Counters.PolicyCompositions, Is.EqualTo(1));
            Assert.That(audit.Profile.Counters.ModesEvaluated, Is.EqualTo(2));
        });
    }

    [Test]
    public void ValidateStrict_WithProfile_FactIndexMaterializesAtMostOncePerSnapshot()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"arch-linter-analysis-profile-fact-index-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        string policyPath = Path.Combine(tempDir, "dependencies.arch.yml");
        File.WriteAllText(policyPath, """
            version: 1
            name: Fact index test

            analysis:
              target_assemblies: [ArchLinterNet.Core]

            contracts:
              strict_layout_conventions:
                - name: exercise the fact index
                  files_matching:
                    namespace_segment: Execution
                  require_type_kind: class
            """);

        ArchitectureValidationResult result = new ArchitectureValidationBuilder(policyPath).WithProfile().ValidateStrict();

        Assert.That(result.Profile, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(result.Profile!.Counters.SnapshotMaterializations, Is.EqualTo(1));
            Assert.That(result.Profile.Counters.FactIndexMaterializations, Is.EqualTo(1));
            Assert.That(result.Profile.Counters.SourceScanPasses, Is.EqualTo(0));
            Assert.That(result.Profile.Counters.SourceFilesScanned, Is.EqualTo(0));
        });
    }
}
