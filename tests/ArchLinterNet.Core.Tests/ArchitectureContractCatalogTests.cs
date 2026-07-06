using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Execution;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class ArchitectureContractCatalogTests
{
    private static ArchitectureContractDocument BuildDocument()
    {
        return new ArchitectureContractDocument
        {
            Version = 1,
            Name = "Test",
            Contracts = new ArchitectureContractGroups
            {
                Strict = new List<ArchitectureDependencyContract>
                {
                    new() { Id = "dep-strict", Name = "Dependency Strict", Source = "core", Forbidden = { "runtime" } }
                },
                Audit = new List<ArchitectureDependencyContract>
                {
                    new() { Id = "dep-audit", Name = "Dependency Audit", Source = "core", Forbidden = { "runtime" } }
                },
                StrictLayers = new List<ArchitectureLayerContract>
                {
                    new() { Id = "layer-strict", Name = "Layer Strict", Layers = { "core", "runtime" } }
                },
                StrictCycles = new List<ArchitectureCycleContract>
                {
                    new() { Id = "cycle-strict", Name = "Cycle Strict", Layers = { "core", "runtime" } }
                },
                StrictMethodBody = new List<ArchitectureMethodBodyContract>
                {
                    new() { Id = "method-body-strict", Name = "Method Body Strict", Source = "core" }
                },
                StrictAsmdef = new List<ArchitectureAsmdefContract>
                {
                    new() { Id = "asmdef-strict", Name = "Asmdef Strict" }
                },
                StrictIndependence = new List<ArchitectureIndependenceContract>
                {
                    new() { Id = "independence-strict", Name = "Independence Strict", Layers = { "core", "runtime" } }
                },
                StrictProtected = new List<ArchitectureProtectedContract>
                {
                    new() { Id = "protected-strict", Name = "Protected Strict", Protected = { "core" } }
                },
                StrictExternal = new List<ArchitectureExternalDependencyContract>
                {
                    new() { Id = "external-strict", Name = "External Strict", Source = "core" }
                },
                StrictAcyclicSiblings = new List<ArchitectureAcyclicSiblingContract>
                {
                    new() { Id = "acyclic-strict", Name = "Acyclic Strict", Ancestors = { "core" } }
                },
                StrictLayerTemplates = new List<ArchitectureLayerTemplateContract>
                {
                    new()
                    {
                        Id = "tmpl",
                        Name = "Template",
                        Containers = { "ArchLinterNet.Core" },
                        Layers = { new ArchitectureTemplateLayer { Name = "Sub" } }
                    }
                }
            }
        };
    }

    [Test]
    public void Build_CreatesDescriptorsAcrossRepresentativeFamilies()
    {
        ArchitectureContractCatalog catalog = ArchitectureContractCatalog.Build(BuildDocument());

        HashSet<string> strictIds = catalog.AvailableContractIds("strict");

        Assert.That(strictIds, Does.Contain("dep-strict"));
        Assert.That(strictIds, Does.Contain("layer-strict"));
        Assert.That(strictIds, Does.Contain("cycle-strict"));
        Assert.That(strictIds, Does.Contain("method-body-strict"));
        Assert.That(strictIds, Does.Contain("asmdef-strict"));
        Assert.That(strictIds, Does.Contain("independence-strict"));
        Assert.That(strictIds, Does.Contain("protected-strict"));
        Assert.That(strictIds, Does.Contain("external-strict"));
        Assert.That(strictIds, Does.Contain("acyclic-strict"));
    }

    [Test]
    public void AvailableContractIds_FiltersByMode()
    {
        ArchitectureContractCatalog catalog = ArchitectureContractCatalog.Build(BuildDocument());

        Assert.That(catalog.AvailableContractIds("strict"), Does.Not.Contain("dep-audit"));
        Assert.That(catalog.AvailableContractIds("audit"), Does.Contain("dep-audit"));
        Assert.That(catalog.AvailableContractIds("audit"), Does.Not.Contain("dep-strict"));
    }

    [Test]
    public void AvailableContractIds_IncludesExpandedLayerTemplateIds()
    {
        ArchitectureContractCatalog catalog = ArchitectureContractCatalog.Build(BuildDocument());

        Assert.That(catalog.AvailableContractIds("strict"), Does.Contain("tmpl/archlinternet-core"));
    }

    [Test]
    public void ContractsFor_FiltersByModeAndFamily()
    {
        ArchitectureContractDocument document = BuildDocument();
        ArchitectureContractCatalog catalog = ArchitectureContractCatalog.Build(document);

        List<IArchitectureContract> strictDependency = catalog.ContractsFor("strict", "dependency").ToList();
        List<IArchitectureContract> auditDependency = catalog.ContractsFor("audit", "dependency").ToList();
        List<IArchitectureContract> strictCycle = catalog.ContractsFor("strict", "cycle").ToList();

        Assert.That(strictDependency, Is.EquivalentTo(document.Contracts.Strict));
        Assert.That(auditDependency, Is.EquivalentTo(document.Contracts.Audit));
        Assert.That(strictCycle, Is.EquivalentTo(document.Contracts.StrictCycles));
    }

    [Test]
    public void ContractsFor_IncludesExpandedLayerTemplateContracts()
    {
        ArchitectureContractCatalog catalog = ArchitectureContractCatalog.Build(BuildDocument());

        List<IArchitectureContract> templateContracts = catalog.ContractsFor("strict", "layer_template").ToList();

        Assert.That(templateContracts, Has.Count.EqualTo(1));
        Assert.That(templateContracts[0].Id, Is.EqualTo("tmpl/archlinternet-core"));
    }

    [Test]
    public void FamiliesInOrder_MatchesHistoricalExecutorDispatchOrder()
    {
        // ArchitectureContractExecutor dispatches families in this exact order, which determines
        // violation/cycle insertion order in ValidationOutcome (and JSON output) and --timings entry
        // order. This is the order the executor used before it became catalog-driven; pinned here so
        // a future reordering of the AddGroup calls in Build is caught as a behavior change.
        ArchitectureContractCatalog catalog = ArchitectureContractCatalog.Build(BuildDocument());

        Assert.That(catalog.FamiliesInOrder, Is.EqualTo(new[]
        {
            "dependency", "layer", "layer_template", "allow_only", "cycle", "method_body",
            "asmdef", "independence", "assembly_independence", "assembly_dependency", "assembly_allow_only",
            "package_dependency", "package_allow_only",
            "protected", "external", "external_allow_only", "acyclic_sibling", "coverage",
        }));
    }

    [Test]
    public void ContractsFor_UnknownFamily_ReturnsEmpty()
    {
        ArchitectureContractCatalog catalog = ArchitectureContractCatalog.Build(BuildDocument());

        Assert.That(catalog.ContractsFor("strict", "does_not_exist"), Is.Empty);
    }

    [Test]
    public void ResolveGroup_ReturnsGroupForKnownContractByReference()
    {
        ArchitectureContractDocument document = BuildDocument();
        ArchitectureContractCatalog catalog = ArchitectureContractCatalog.Build(document);

        string? group = catalog.ResolveGroup(document.Contracts.Strict[0]);

        Assert.That(group, Is.EqualTo("strict"));
    }

    [Test]
    public void ResolveGroup_ReturnsGroupForKnownContractByFallbackId()
    {
        ArchitectureContractDocument document = BuildDocument();
        ArchitectureContractCatalog catalog = ArchitectureContractCatalog.Build(document);

        var unrelatedInstance = new ArchitectureDependencyContract { Id = "dep-strict", Name = "Other instance" };

        string? group = catalog.ResolveGroup(unrelatedInstance);

        Assert.That(group, Is.EqualTo("strict"));
    }

    [Test]
    public void ResolveGroup_ExcludesAsmdefContracts()
    {
        ArchitectureContractDocument document = BuildDocument();
        ArchitectureContractCatalog catalog = ArchitectureContractCatalog.Build(document);

        string? group = catalog.ResolveGroup(document.Contracts.StrictAsmdef[0]);

        Assert.That(group, Is.Null);
    }

    [Test]
    public void ResolveGroup_ExcludesExpandedLayerTemplateContracts()
    {
        ArchitectureContractDocument document = BuildDocument();
        ArchitectureContractCatalog catalog = ArchitectureContractCatalog.Build(document);

        var unrelatedInstance = new ArchitectureLayerContract { Id = "tmpl/archlinternet-core", Name = "Other instance" };

        string? group = catalog.ResolveGroup(unrelatedInstance);

        Assert.That(group, Is.Null);
    }

    [Test]
    public void ResolveGroup_ReturnsNullForUnknownContract()
    {
        ArchitectureContractCatalog catalog = ArchitectureContractCatalog.Build(BuildDocument());

        var unknown = new ArchitectureDependencyContract { Id = "does-not-exist", Name = "Unknown" };

        Assert.That(catalog.ResolveGroup(unknown), Is.Null);
    }
}
