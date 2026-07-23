using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Families;
using ArchLinterNet.Core.Contracts.Validators;
using NUnit.Framework;
using ArchitectureContractGroups = ArchLinterNet.Core.Contracts.Families.ArchitectureContractGroups;

namespace ArchLinterNet.Core.Tests;

// Regression coverage for the Contracts-local family binding registry introduced to decouple
// ArchitectureContractGroups from a single mega DTO file (issue #216). These tests exercise the
// two behavioral subtleties that a naive "just enumerate every family" registry would silently
// break: AllStrict/AllAudit exclude layer_template (its raw contracts are expanded into real layer
// contracts elsewhere), while DuplicateIdValidator must still check layer_template.
[TestFixture]
public sealed class ArchitectureContractFamilyBindingsTests
{
    [Test]
    public void All_HasTwentyNineUniqueFamilyIds()
    {
        var familyIds = ArchitectureContractFamilyBindings.All.Select(b => b.FamilyId).ToList();

        Assert.That(familyIds, Has.Count.EqualTo(31));
        Assert.That(familyIds.Distinct(StringComparer.Ordinal).Count(), Is.EqualTo(31));
    }

    [Test]
    public void All_ExactlyLayerTemplateIsExcludedFromContractEnumeration()
    {
        var excluded = ArchitectureContractFamilyBindings.All
            .Where(b => !b.IncludeInContractEnumeration)
            .Select(b => b.FamilyId)
            .ToList();

        Assert.That(excluded, Is.EqualTo(new[] { "layer_template" }));
    }

    private static ArchitectureContractGroups BuildGroupsWithOneStrictContractPerFamily() => new()
    {
        Strict = { new ArchitectureDependencyContract { Name = "n", Id = "dependency" } },
        StrictLayers = { new ArchitectureLayerContract { Name = "n", Id = "layer" } },
        StrictLayerTemplates = { new ArchitectureLayerTemplateContract { Name = "n", Id = "layer_template" } },
        StrictAllowOnly = { new ArchitectureAllowOnlyContract { Name = "n", Id = "allow_only" } },
        StrictCycles = { new ArchitectureCycleContract { Name = "n", Id = "cycle" } },
        StrictMethodBody = { new ArchitectureMethodBodyContract { Name = "n", Id = "method_body" } },
        StrictAsmdef = { new ArchitectureAsmdefContract { Name = "n", Id = "asmdef" } },
        StrictIndependence = { new ArchitectureIndependenceContract { Name = "n", Id = "independence" } },
        StrictAssemblyIndependence =
            { new ArchitectureAssemblyIndependenceContract { Name = "n", Id = "assembly_independence" } },
        StrictAssemblyDependency =
            { new ArchitectureAssemblyDependencyContract { Name = "n", Id = "assembly_dependency" } },
        StrictAssemblyAllowOnly =
            { new ArchitectureAssemblyAllowOnlyContract { Name = "n", Id = "assembly_allow_only" } },
        StrictPackageDependency =
            { new ArchitecturePackageDependencyContract { Name = "n", Id = "package_dependency" } },
        StrictPackageAllowOnly =
            { new ArchitecturePackageAllowOnlyContract { Name = "n", Id = "package_allow_only" } },
        StrictProjectMetadata = { new ArchitectureProjectMetadataContract { Name = "n", Id = "project_metadata" } },
        StrictProtected = { new ArchitectureProtectedContract { Name = "n", Id = "protected" } },
        StrictExternal = { new ArchitectureExternalDependencyContract { Name = "n", Id = "external" } },
        StrictExternalAllowOnly =
            { new ArchitectureExternalAllowOnlyContract { Name = "n", Id = "external_allow_only" } },
        StrictAcyclicSiblings = { new ArchitectureAcyclicSiblingContract { Name = "n", Id = "acyclic_sibling" } },
        StrictTypePlacement = { new ArchitectureTypePlacementContract { Name = "n", Id = "type_placement" } },
        StrictPublicApiSurface =
            { new ArchitecturePublicApiSurfaceContract { Name = "n", Id = "public_api_surface" } },
        StrictAttributeUsage = { new ArchitectureAttributeUsageContract { Name = "n", Id = "attribute_usage" } },
        StrictInheritance = { new ArchitectureInheritanceContract { Name = "n", Id = "inheritance" } },
        StrictInterfaceImplementation =
            { new ArchitectureInterfaceImplementationContract { Name = "n", Id = "interface_implementation" } },
        StrictComposition = { new ArchitectureCompositionContract { Name = "n", Id = "composition" } },
        StrictCoverage = { new ArchitectureCoverageContract { Name = "n", Id = "coverage" } },
        StrictPortBoundaries = { new ArchitecturePortBoundaryContract { Name = "n", Id = "port_boundary" } },
    };

    [Test]
    public void AllStrict_ContainsExactlyOnePerFamilyExcludingLayerTemplate()
    {
        var groups = BuildGroupsWithOneStrictContractPerFamily();

        var strictIds = groups.AllStrict.Select(c => c.Id).ToList();

        Assert.That(strictIds, Has.Count.EqualTo(25));
        Assert.That(strictIds, Does.Not.Contain("layer_template"));
    }

    [Test]
    public void AllStrict_ExcludesLayerTemplateContracts()
    {
        var groups = new ArchitectureContractGroups
        {
            Strict = { new ArchitectureDependencyContract { Name = "dep", Id = "dep" } },
            StrictLayerTemplates = { new ArchitectureLayerTemplateContract { Name = "tmpl", Id = "tmpl" } },
        };

        var strictIds = groups.AllStrict.Select(c => c.Id).ToList();

        Assert.That(strictIds, Does.Contain("dep"));
        Assert.That(strictIds, Does.Not.Contain("tmpl"));
        Assert.That(groups.AllStrict, Has.None.InstanceOf<ArchitectureLayerTemplateContract>());
    }

    [Test]
    public void AllAudit_ExcludesLayerTemplateContracts()
    {
        var groups = new ArchitectureContractGroups
        {
            Audit = { new ArchitectureDependencyContract { Name = "dep", Id = "dep" } },
            AuditLayerTemplates = { new ArchitectureLayerTemplateContract { Name = "tmpl", Id = "tmpl" } },
        };

        var auditIds = groups.AllAudit.Select(c => c.Id).ToList();

        Assert.That(auditIds, Does.Contain("dep"));
        Assert.That(auditIds, Does.Not.Contain("tmpl"));
        Assert.That(groups.AllAudit, Has.None.InstanceOf<ArchitectureLayerTemplateContract>());
    }

    [Test]
    public void DuplicateIdValidator_DetectsDuplicatesInLayerTemplateGroup()
    {
        var document = new ArchitectureContractDocument
        {
            Contracts = new ArchitectureContractGroups
            {
                StrictLayerTemplates =
                {
                    new ArchitectureLayerTemplateContract { Name = "Template One", Id = "dup-template" },
                    new ArchitectureLayerTemplateContract { Name = "Template Two", Id = "dup-template" },
                },
            },
        };

        Assert.Throws<InvalidOperationException>(() => new DuplicateIdValidator().Validate(document));
    }

    [Test]
    public void DuplicateIdValidator_AllowsUniqueIdsAcrossEveryFamily()
    {
        var document = new ArchitectureContractDocument { Contracts = BuildGroupsWithOneStrictContractPerFamily() };

        Assert.DoesNotThrow(() => new DuplicateIdValidator().Validate(document));
    }
}
