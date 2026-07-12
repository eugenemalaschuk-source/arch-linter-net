using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Families;
using ArchLinterNet.Core.Execution;
using NUnit.Framework;
using ArchitectureContractGroups = ArchLinterNet.Core.Contracts.Families.ArchitectureContractGroups;

namespace ArchLinterNet.Core.Tests;

public sealed partial class AcyclicSiblingContractTests
{
    [Test]
    public void CheckCycleContract_SelectorOnlyTargetLayer_FormsCycle()
    {
        var document = new ArchitectureContractDocument
        {
            Version = 1,
            Name = "Test",
            Classification = new ArchitectureClassificationConfiguration
            {
                Attributes =
                {
                    new ArchitectureAttributeClassificationMapping
                    {
                        Attribute = "AttributeRoleExtractionTestFixtures.DomainMarkerAttribute",
                        Role = "DomainLayer",
                        Metadata = new Dictionary<string, object>
                        {
                            ["domain"] = "constructor[0]",
                            ["enabled"] = "property:Enabled"
                        }
                    }
                }
            },
            Layers = new Dictionary<string, ArchitectureLayer>
            {
                ["semantic_domain"] = new()
                {
                    Selector = new ArchitectureLayerSelector
                    {
                        Role = "DomainLayer",
                        Metadata = new Dictionary<string, object>
                        {
                            ["domain"] = "Sales",
                            ["enabled"] = true
                        }
                    }
                },
                ["application"] = new()
                {
                    Namespace = "SelectorCycleFixtures.ApplicationSelector"
                }
            },
            Analysis = new ArchitectureAnalysisConfiguration
            {
                TargetAssemblies = new List<string> { "ArchLinterNet.Core.Tests" }
            },
            Contracts = new ArchitectureContractGroups
            {
                StrictCycles = new List<ArchitectureCycleContract>
                {
                    new()
                    {
                        Name = "selector-cycle",
                        Layers = new List<string> { "semantic_domain", "application" }
                    }
                }
            }
        };

        var context = new ArchitectureAnalysisContext(
            _tempDir,
            new[] { typeof(SelectorCycleFixtures.Domain.SelectedDomainNode).Assembly },
            Array.Empty<string>(),
            Array.Empty<string>());

        var runner = new ArchitectureContractRunner(context, document);
        var cycles = runner.CheckCycleContract(document.Contracts.StrictCycles[0]);

        Assert.That(cycles, Does.Contain("application -> semantic_domain -> application"));
    }

    [Test]
    public void CheckCycleContract_NamespaceAndSelectorTargetLayer_DoesNotCreateFalseEdge()
    {
        var document = new ArchitectureContractDocument
        {
            Version = 1,
            Name = "Test",
            Classification = new ArchitectureClassificationConfiguration
            {
                Attributes =
                {
                    new ArchitectureAttributeClassificationMapping
                    {
                        Attribute = "AttributeRoleExtractionTestFixtures.DomainMarkerAttribute",
                        Role = "DomainLayer",
                        Metadata = new Dictionary<string, object>
                        {
                            ["domain"] = "constructor[0]",
                            ["enabled"] = "property:Enabled"
                        }
                    }
                }
            },
            Layers = new Dictionary<string, ArchitectureLayer>
            {
                ["semantic_domain"] = new()
                {
                    Namespace = "SelectorCycleFixtures.Domain",
                    Selector = new ArchitectureLayerSelector
                    {
                        Role = "DomainLayer",
                        Metadata = new Dictionary<string, object>
                        {
                            ["domain"] = "Sales",
                            ["enabled"] = true
                        }
                    }
                },
                ["application_false_edge"] = new()
                {
                    Namespace = "SelectorCycleFixtures.ApplicationFalseEdge"
                }
            },
            Analysis = new ArchitectureAnalysisConfiguration
            {
                TargetAssemblies = new List<string> { "ArchLinterNet.Core.Tests" }
            },
            Contracts = new ArchitectureContractGroups
            {
                StrictCycles = new List<ArchitectureCycleContract>
                {
                    new()
                    {
                        Name = "combined-selector-no-false-cycle",
                        Layers = new List<string> { "semantic_domain", "application_false_edge" }
                    }
                }
            }
        };

        var context = new ArchitectureAnalysisContext(
            _tempDir,
            new[] { typeof(SelectorCycleFixtures.Domain.SelectedDomainNode).Assembly },
            Array.Empty<string>(),
            Array.Empty<string>());

        var runner = new ArchitectureContractRunner(context, document);
        var cycles = runner.CheckCycleContract(document.Contracts.StrictCycles[0]);

        Assert.That(cycles, Is.Empty);
    }
}
