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
