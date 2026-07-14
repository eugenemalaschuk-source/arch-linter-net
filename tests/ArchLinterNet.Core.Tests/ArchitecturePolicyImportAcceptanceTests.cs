using System.Text.Json;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.PolicyImports;
using ArchLinterNet.Core.Resolution;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class ArchitecturePolicyImportAcceptanceTests
{
    [TestCase("modular-monolith")]
    [TestCase("unity-client")]
    public void Load_PublicMonolithicAndImportedSamples_ProduceEquivalentModels(string sample)
    {
        ArchitectureContractDocument monolithic = Load($"samples/policies/imports/{sample}/monolithic.yml");
        ArchitectureContractDocument imported = Load($"samples/policies/imports/{sample}/architecture/arch.yml");

        Assert.That(Normalize(imported), Is.EqualTo(Normalize(monolithic)));
    }

    [Test]
    public void Load_RecommendedAndArbitraryFixtureNames_ProduceEquivalentModels()
    {
        ArchitectureContractDocument recommended = Load(
            "tests/ArchLinterNet.Core.Tests/TestPolicies/PolicyImports/Naming/recommended/architecture/arch.yml");
        ArchitectureContractDocument arbitrary = Load(
            "tests/ArchLinterNet.Core.Tests/TestPolicies/PolicyImports/Naming/alternative/config/company-policy.yaml");

        Assert.That(Normalize(arbitrary), Is.EqualTo(Normalize(recommended)));
    }

    [TestCase(
        "root-fragment",
        "architecture/arch.yml",
        "architecture/policy/domain.arch.yml")]
    [TestCase(
        "fragment-fragment",
        "architecture/policy/first.arch.yml",
        "architecture/policy/second.arch.yml")]
    public void Load_CommittedConflictFixtures_ReportBothSources(
        string fixture,
        string expectedPrimarySuffix,
        string expectedRelatedSuffix)
    {
        string relativeRoot =
            $"tests/ArchLinterNet.Core.Tests/TestPolicies/PolicyImports/Conflicts/{fixture}/architecture/arch.yml";

        ArchitecturePolicyImportException exception = Assert.Throws<ArchitecturePolicyImportException>(
            () => Load(relativeRoot))!;

        Assert.Multiple(() =>
        {
            Assert.That(exception.Category, Is.EqualTo(ArchitecturePolicyImportErrorCategory.CompositionConflict));
            Assert.That(exception.Diagnostic!.Location!.SourcePath, Does.EndWith(expectedPrimarySuffix));
            Assert.That(exception.Diagnostic.RelatedLocations.Single().SourcePath,
                Does.EndWith(expectedRelatedSuffix));
        });
    }

    private static ArchitectureContractDocument Load(string relativePath)
    {
        string repositoryRoot = new ArchitectureRepositoryRootResolver().Resolve();
        string path = Path.Combine(repositoryRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
        return new ArchitecturePolicyDocumentLoader().Load(path);
    }

    private static string Normalize(ArchitectureContractDocument document)
    {
        var model = new
        {
            document.Version,
            document.Name,
            Layers = document.Layers.Select(pair => new
            {
                pair.Key,
                pair.Value.Namespace,
                pair.Value.NamespaceSuffix,
                pair.Value.External
            }),
            ExternalDependencies = document.ExternalDependencies.Select(pair => new
            {
                pair.Key,
                pair.Value.NamespacePrefixes,
                pair.Value.TypePrefixes
            }),
            document.LegacyRuntimeLayers,
            document.Analysis.TargetAssemblies,
            document.Analysis.AssemblySearchPaths,
            document.Analysis.SourceRoots,
            document.Analysis.Configuration,
            StrictContracts = document.Contracts.AllStrict.Select(ContractIdentity),
            AuditContracts = document.Contracts.AllAudit.Select(ContractIdentity)
        };

        return JsonSerializer.Serialize(model);
    }

    private static string ContractIdentity(IArchitectureContract contract)
    {
        return $"{contract.GetType().Name}:{contract.Id}:{contract.Name}";
    }
}

