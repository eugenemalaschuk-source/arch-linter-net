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
    public void Load_ModularMonolithSample_ContainsPortBoundaryAndLayoutConventionContracts()
    {
        ArchitectureContractDocument document = Load("samples/policies/imports/modular-monolith/architecture/arch.yml");

        Assert.Multiple(() =>
        {
            Assert.That(document.Contracts.StrictPortBoundaries.Select(c => c.Id),
                Is.EquivalentTo(new[] { "sales-to-catalog-through-port", "legacy-crm-through-acl" }));
            Assert.That(document.Contracts.StrictLayoutConventions.Select(c => c.Id),
                Is.EquivalentTo(new[] { "application-services-have-matching-interfaces" }));
        });
    }

    [Test]
    public void Load_ModularMonolithSample_IsolationContractsCoverEveryBoundedContextDomain()
    {
        // Regression: adding a bounded context (Catalog, LegacyCrm) without extending the sample's
        // own independence/SharedKernel isolation contracts would silently permit exactly the
        // coupling those contracts' `reason` text promises is prevented.
        ArchitectureContractDocument document = Load("samples/policies/imports/modular-monolith/architecture/arch.yml");

        Assert.Multiple(() =>
        {
            Assert.That(
                document.Contracts.StrictIndependence
                    .Single(c => c.Id == "bounded-context-domains-independent").Layers,
                Is.EquivalentTo(new[] { "sales_domain", "inventory_domain", "catalog_domain", "legacy_crm_domain" }));
            Assert.That(
                document.Contracts.Strict
                    .Single(c => c.Id == "shared-kernel-does-not-depend-on-modules").Forbidden,
                Is.EquivalentTo(new[] { "sales_domain", "inventory_domain", "catalog_domain", "legacy_crm_domain" }));
        });
    }

    [Test]
    public void Load_UnityClientSample_ContainsLayoutConventionContract()
    {
        ArchitectureContractDocument document = Load("samples/policies/imports/unity-client/architecture/arch.yml");

        Assert.That(document.Contracts.StrictLayoutConventions.Select(c => c.Id),
            Is.EquivalentTo(new[] { "runtime-folder-forbids-editor-types" }));
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
            document.Layers,
            document.ExternalDependencies,
            document.Packages,
            document.LegacyRuntimeLayers,
            document.Analysis,
            document.Contracts,
            document.Classification,
            ClassificationPathDeferredEntryCount = document.ClassificationPathDeferred?.DeclaredEntryCount
        };

        return JsonSerializer.Serialize(model);
    }
}
