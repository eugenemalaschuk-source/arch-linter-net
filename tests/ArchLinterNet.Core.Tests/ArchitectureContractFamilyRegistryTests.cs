using System.Reflection;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Families;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Execution.Abstractions;
using ArchLinterNet.Core.Model;
using ContextualContractTestFixtures;
using NUnit.Framework;
using ArchitectureContractGroups = ArchLinterNet.Core.Contracts.Families.ArchitectureContractGroups;

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
        "protected", "external", "external_allow_only", "acyclic_sibling", "type_placement", "layout_conventions",
        "public_api_surface", "attribute_usage", "inheritance", "interface_implementation", "composition", "coverage",
        "context_dependency", "context_allow_only", "port_boundary",
    };

    [Test]
    public void All_ContainsExactlyTheHistoricalFamilyCount()
    {
        Assert.That(ArchitectureContractFamilyRegistry.All, Has.Count.EqualTo(29));
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

    [Test]
    public void PortBoundaryDescriptor_ChecksThroughRegisteredCheckerDelegate()
    {
        ArchitectureContractFamilyDescriptor descriptor = ArchitectureContractFamilyRegistry.All
            .Single(d => d.FamilyId == "port_boundary");

        Assembly assembly = typeof(SalesCheckout).Assembly;
        var contract = new ArchitecturePortBoundaryContract
        {
            Name = "sales-to-inventory-through-port",
            Source = new ArchitectureContextSelector { Role = "DomainLayer", Metadata = new Dictionary<string, object> { ["domain"] = "Sales" } },
            TargetContext = new ArchitectureContextMetadataSelector { Metadata = new Dictionary<string, object> { ["domain"] = "Inventory" } },
            AllowedSeams = new List<ArchitectureContextSelector> { new() { Role = "Port", Metadata = new Dictionary<string, object> { ["domain"] = "Inventory" } } },
            Forbidden = new List<ArchitectureContextSelector> { new() { Role = "DomainLayer", Metadata = new Dictionary<string, object> { ["domain"] = "Inventory" } } },
            Reason = "Use the reviewed port."
        };
        var document = new ArchitectureContractDocument
        {
            Version = 1,
            Name = "ports",
            Analysis = new ArchitectureAnalysisConfiguration { TargetAssemblies = new List<string> { assembly.GetName().Name! } },
            Classification = new ArchitectureClassificationConfiguration
            {
                Attributes =
                {
                    new ArchitectureAttributeClassificationMapping { Attribute = "ContextualContractTestFixtures.ContextDomainMarkerAttribute", Role = "DomainLayer", Metadata = new Dictionary<string, object> { ["domain"] = "constructor[0]" } },
                    new ArchitectureAttributeClassificationMapping { Attribute = "ContextualContractTestFixtures.ContextPortMarkerAttribute", Role = "Port", Metadata = new Dictionary<string, object> { ["domain"] = "constructor[0]" } },
                },
            },
            Contracts = new ArchitectureContractGroups { StrictPortBoundaries = new List<ArchitecturePortBoundaryContract> { contract } },
        };
        var runner = new ArchitectureContractRunner(new ArchitectureAnalysisContext("/tmp", new[] { assembly }, Array.Empty<string>(), Array.Empty<string>()), document);

        ArchitectureHandlerResult result = descriptor.Checker(runner.Session, contract);

        Assert.That(result.Violations.Any(v => v.SourceType == typeof(SalesCheckout).FullName), Is.True);
    }
}
