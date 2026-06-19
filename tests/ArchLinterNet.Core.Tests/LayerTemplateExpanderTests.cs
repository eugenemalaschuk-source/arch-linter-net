using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Execution;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class LayerTemplateExpanderTests
{
    [Test]
    public void Expand_SingleContainer_ProducesOneContract()
    {
        var templates = new List<ArchitectureLayerTemplateContract>
        {
            new()
            {
                Name = "test-template",
                Containers = new List<string> { "MyApp.Features.Fishing" },
                Layers = new List<ArchitectureTemplateLayer>
                {
                    new() { Name = "Presentation" },
                    new() { Name = "Domain" }
                }
            }
        };

        List<ArchitectureLayerContract> result = LayerTemplateExpander.Expand(templates);

        Assert.That(result, Has.Count.EqualTo(1));
    }

    [Test]
    public void Expand_SingleContainer_ResolvesLayersCorrectly()
    {
        var templates = new List<ArchitectureLayerTemplateContract>
        {
            new()
            {
                Name = "t",
                Containers = new List<string> { "MyApp.Features.Fishing" },
                Layers = new List<ArchitectureTemplateLayer>
                {
                    new() { Name = "Presentation" },
                    new() { Name = "Domain" }
                }
            }
        };

        List<ArchitectureLayerContract> result = LayerTemplateExpander.Expand(templates);

        Assert.That(result[0].Layers,
            Is.EqualTo(new[] { "MyApp.Features.Fishing.Presentation", "MyApp.Features.Fishing.Domain" }));
    }

    [Test]
    public void Expand_MultipleContainers_ProducesOneContractPerContainer()
    {
        var templates = new List<ArchitectureLayerTemplateContract>
        {
            new()
            {
                Name = "t",
                Containers = new List<string> { "App.A", "App.B", "App.C" },
                Layers = new List<ArchitectureTemplateLayer>
                {
                    new() { Name = "Presentation" },
                    new() { Name = "Domain" }
                }
            }
        };

        List<ArchitectureLayerContract> result = LayerTemplateExpander.Expand(templates);

        Assert.That(result, Has.Count.EqualTo(3));
        Assert.That(result[0].Layers[0], Does.StartWith("App.A."));
        Assert.That(result[1].Layers[0], Does.StartWith("App.B."));
        Assert.That(result[2].Layers[0], Does.StartWith("App.C."));
    }

    [Test]
    public void Expand_IdGeneratedFromTemplateNameAndContainer()
    {
        var templates = new List<ArchitectureLayerTemplateContract>
        {
            new()
            {
                Name = "Feature Clean Architecture",
                Id = "fca",
                Containers = new List<string> { "MyApp.Features.Fishing" },
                Layers = new List<ArchitectureTemplateLayer>
                {
                    new() { Name = "Presentation" }
                }
            }
        };

        List<ArchitectureLayerContract> result = LayerTemplateExpander.Expand(templates);

        Assert.That(result[0].Id, Is.EqualTo("fca/myapp-features-fishing"));
    }

    [Test]
    public void Expand_FallbackIdFromName()
    {
        var templates = new List<ArchitectureLayerTemplateContract>
        {
            new()
            {
                Name = "Feature Clean Architecture",
                Containers = new List<string> { "MyApp.Features.Fishing" },
                Layers = new List<ArchitectureTemplateLayer>
                {
                    new() { Name = "Presentation" }
                }
            }
        };

        List<ArchitectureLayerContract> result = LayerTemplateExpander.Expand(templates);

        Assert.That(result[0].Id, Does.StartWith("feature-clean-architecture/"));
    }

    [Test]
    public void Expand_ContractNameIncludesTemplateAndContainer()
    {
        var templates = new List<ArchitectureLayerTemplateContract>
        {
            new()
            {
                Name = "clean-arch",
                Containers = new List<string> { "MyApp.Features.Fishing" },
                Layers = new List<ArchitectureTemplateLayer>
                {
                    new() { Name = "Presentation" }
                }
            }
        };

        List<ArchitectureLayerContract> result = LayerTemplateExpander.Expand(templates);

        Assert.That(result[0].Name, Is.EqualTo("clean-arch (MyApp.Features.Fishing)"));
    }

    [Test]
    public void Expand_PreservesOptionalLayers()
    {
        var templates = new List<ArchitectureLayerTemplateContract>
        {
            new()
            {
                Name = "t",
                Containers = new List<string> { "App" },
                Layers = new List<ArchitectureTemplateLayer>
                {
                    new() { Name = "Required" },
                    new() { Name = "Optional", Optional = true },
                    new() { Name = "AlsoRequired" }
                }
            }
        };

        List<ArchitectureLayerContract> result = LayerTemplateExpander.Expand(templates);

        Assert.That(result[0].OptionalLayers, Has.Count.EqualTo(1));
        Assert.That(result[0].OptionalLayers, Does.Contain("App.Optional"));
    }

    [Test]
    public void Expand_SetsTemplateNameAndContainerNamespace()
    {
        var templates = new List<ArchitectureLayerTemplateContract>
        {
            new()
            {
                Name = "my-template",
                Containers = new List<string> { "MyApp.Features.Fishing" },
                Layers = new List<ArchitectureTemplateLayer>
                {
                    new() { Name = "Presentation" }
                }
            }
        };

        List<ArchitectureLayerContract> result = LayerTemplateExpander.Expand(templates);

        Assert.That(result[0].TemplateName, Is.EqualTo("my-template"));
        Assert.That(result[0].ContainerNamespace, Is.EqualTo("MyApp.Features.Fishing"));
    }

    [Test]
    public void Expand_EmptyTemplateList_ReturnsEmpty()
    {
        List<ArchitectureLayerContract> result = LayerTemplateExpander.Expand(
            new List<ArchitectureLayerTemplateContract>());

        Assert.That(result, Is.Empty);
    }

    [Test]
    public void Expand_EmptyContainers_ReturnsEmpty()
    {
        var templates = new List<ArchitectureLayerTemplateContract>
        {
            new()
            {
                Name = "t",
                Containers = new List<string>(),
                Layers = new List<ArchitectureTemplateLayer>
                {
                    new() { Name = "Presentation" }
                }
            }
        };

        List<ArchitectureLayerContract> result = LayerTemplateExpander.Expand(templates);

        Assert.That(result, Is.Empty);
    }

    [Test]
    public void Expand_MultipleTemplates_ExpandsIndependently()
    {
        var templates = new List<ArchitectureLayerTemplateContract>
        {
            new()
            {
                Name = "t1",
                Containers = new List<string> { "App.A" },
                Layers = new List<ArchitectureTemplateLayer> { new() { Name = "Layer" } }
            },
            new()
            {
                Name = "t2",
                Containers = new List<string> { "App.B" },
                Layers = new List<ArchitectureTemplateLayer> { new() { Name = "Layer" } }
            }
        };

        List<ArchitectureLayerContract> result = LayerTemplateExpander.Expand(templates);

        Assert.That(result, Has.Count.EqualTo(2));
        Assert.That(result[0].TemplateName, Is.EqualTo("t1"));
        Assert.That(result[1].TemplateName, Is.EqualTo("t2"));
    }

    [Test]
    public void Expand_PersistsReason()
    {
        var templates = new List<ArchitectureLayerTemplateContract>
        {
            new()
            {
                Name = "t",
                Containers = new List<string> { "App" },
                Layers = new List<ArchitectureTemplateLayer> { new() { Name = "Layer" } },
                Reason = "Because architecture matters"
            }
        };

        List<ArchitectureLayerContract> result = LayerTemplateExpander.Expand(templates);

        Assert.That(result[0].Reason, Is.EqualTo("Because architecture matters"));
    }
}
