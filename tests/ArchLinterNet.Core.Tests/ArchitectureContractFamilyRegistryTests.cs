using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Execution;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class ArchitectureContractFamilyRegistryTests
{
    // Pinned to the same order ArchitectureContractCatalogTests.FamiliesInOrder_MatchesHistoricalExecutorDispatchOrder
    // asserts for ArchitectureContractCatalog.FamiliesInOrder, since the catalog derives that order
    // from this registry.
    private static readonly string[] _historicalOrder =
    {
        "dependency", "layer", "layer_template", "allow_only", "cycle", "method_body",
        "asmdef", "independence", "assembly_independence", "assembly_dependency", "assembly_allow_only",
        "package_dependency", "package_allow_only", "project_metadata",
        "protected", "external", "external_allow_only", "acyclic_sibling", "type_placement",
        "public_api_surface", "attribute_usage", "inheritance", "interface_implementation", "composition", "coverage",
    };

    [Test]
    public void All_ContainsExactlyTheHistoricalFamilyCount()
    {
        Assert.That(ArchitectureContractFamilyRegistry.All, Has.Count.EqualTo(25));
    }

    [Test]
    public void All_HasNoDuplicateFamilyIds()
    {
        List<string> familyIds = ArchitectureContractFamilyRegistry.All.Select(d => d.FamilyId).ToList();

        Assert.That(familyIds.Distinct().ToList(), Has.Count.EqualTo(familyIds.Count));
    }

    [Test]
    public void All_FamilyIdOrderMatchesHistoricalDispatchOrder()
    {
        List<string> familyIds = ArchitectureContractFamilyRegistry.All.Select(d => d.FamilyId).ToList();

        Assert.That(familyIds, Is.EqualTo(_historicalOrder));
    }

    [Test]
    public void All_OnlyAsmdefAndLayerTemplateAreNotBaselineCapable()
    {
        List<string> nonBaselineCapable = ArchitectureContractFamilyRegistry.All
            .Where(d => !d.IsBaselineCapable)
            .Select(d => d.FamilyId)
            .ToList();

        Assert.That(nonBaselineCapable, Is.EquivalentTo(new[] { "layer_template", "asmdef" }));
    }

    [Test]
    public void All_NoDescriptorInvokesAdditionalValidationInThisChange()
    {
        foreach (ArchitectureContractFamilyDescriptor descriptor in ArchitectureContractFamilyRegistry.All)
        {
            Assert.That(descriptor.AdditionalValidation, Is.Null,
                $"Family '{descriptor.FamilyId}' declares an AdditionalValidation hook, but no descriptor " +
                "should wire one in this change - it is a placeholder for future decomposition.");
        }
    }

    [Test]
    public void All_EveryDescriptorHasANonNullChecker()
    {
        foreach (ArchitectureContractFamilyDescriptor descriptor in ArchitectureContractFamilyRegistry.All)
        {
            Assert.That(descriptor.Checker, Is.Not.Null,
                $"Family '{descriptor.FamilyId}' must expose a live Checker delegate.");
        }
    }

    [Test]
    public void All_OnlySixteenFamiliesHaveANonNullConfigurationContributor()
    {
        // Families that CheckConfiguration hand-validated configuration references for before #212;
        // every other family (including composition, whose AllowedOnlyInLayers is already handled
        // by GetReferencedLayerNames elsewhere but was never fed into CheckConfiguration) must stay
        // null so the refactor does not silently start producing new violations for previously-silent
        // policies.
        string[] expectedContributingFamilies =
        {
            "dependency", "layer", "allow_only", "cycle", "method_body", "independence", "protected",
            "external", "external_allow_only", "package_dependency", "package_allow_only", "project_metadata",
            "type_placement", "attribute_usage", "inheritance", "interface_implementation",
        };

        List<string> actualContributingFamilies = ArchitectureContractFamilyRegistry.All
            .Where(d => d.ConfigurationContributor is not null)
            .Select(d => d.FamilyId)
            .ToList();

        Assert.That(actualContributingFamilies, Is.EquivalentTo(expectedContributingFamilies));

        ArchitectureContractFamilyDescriptor compositionDescriptor = ArchitectureContractFamilyRegistry.All
            .Single(d => d.FamilyId == "composition");
        Assert.That(compositionDescriptor.ConfigurationContributor, Is.Null,
            "composition intentionally has no ConfigurationContributor yet - see design.md for the documented gap.");
    }

    [Test]
    public void LayerTemplateDescriptor_AccessorsExpandTemplatesLikeCatalogDoes()
    {
        ArchitectureContractFamilyDescriptor descriptor = ArchitectureContractFamilyRegistry.All
            .Single(d => d.FamilyId == "layer_template");

        var groups = new ArchitectureContractGroups
        {
            StrictLayerTemplates = new List<ArchitectureLayerTemplateContract>
            {
                new()
                {
                    Id = "tmpl",
                    Name = "Template",
                    Containers = { "ArchLinterNet.Core" },
                    Layers = { new ArchitectureTemplateLayer { Name = "Sub" } },
                },
            },
            AuditLayerTemplates = new List<ArchitectureLayerTemplateContract>
            {
                new()
                {
                    Id = "audit-tmpl",
                    Name = "Audit Template",
                    Containers = { "ArchLinterNet.Core" },
                    Layers = { new ArchitectureTemplateLayer { Name = "Sub" } },
                },
            },
        };

        List<IArchitectureContract> expectedStrict = LayerTemplateExpander.Expand(groups.StrictLayerTemplates)
            .Cast<IArchitectureContract>()
            .ToList();
        List<IArchitectureContract> expectedAudit = LayerTemplateExpander.Expand(groups.AuditLayerTemplates)
            .Cast<IArchitectureContract>()
            .ToList();

        List<IArchitectureContract> actualStrict = descriptor.StrictContracts(groups).ToList();
        List<IArchitectureContract> actualAudit = descriptor.AuditContracts(groups).ToList();

        Assert.That(actualStrict.Select(c => c.Id), Is.EqualTo(expectedStrict.Select(c => c.Id)));
        Assert.That(actualAudit.Select(c => c.Id), Is.EqualTo(expectedAudit.Select(c => c.Id)));
    }
}
