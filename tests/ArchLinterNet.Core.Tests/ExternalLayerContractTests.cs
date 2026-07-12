using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Families;
using ArchLinterNet.Core.Execution;
using NUnit.Framework;
using ArchitectureContractGroups = ArchLinterNet.Core.Contracts.Families.ArchitectureContractGroups;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class ExternalLayerContractTests
{
    [Test]
    public void CheckContract_ExternalForbiddenLayer_ProducesViolations()
    {
        var document = new ArchitectureContractDocument
        {
            Version = 1,
            Name = "Test",
            Layers = new Dictionary<string, ArchitectureLayer>
            {
                ["core"] = new() { Namespace = "ArchLinterNet.Core" },
                ["system"] = new() { Namespace = "System", External = true }
            },
            Analysis = new ArchitectureAnalysisConfiguration
            {
                TargetAssemblies = new List<string> { "ArchLinterNet.Core" }
            },
            Contracts = new ArchitectureContractGroups
            {
                Strict = new List<ArchitectureDependencyContract>
                {
                    new()
                    {
                        Name = "no-system-refs",
                        Source = "core",
                        Forbidden = new List<string> { "system" }
                    }
                }
            }
        };

        var context = new ArchitectureAnalysisContext(
            "/tmp",
            new[] { typeof(ArchitecturePolicyDocumentLoader).Assembly },
            Array.Empty<string>(),
            Array.Empty<string>());

        var runner = new ArchitectureContractRunner(context, document);
        var violations = runner.CheckContract(document.Contracts.Strict[0]);

        Assert.That(violations, Is.Not.Empty);
    }

    [Test]
    public void CheckLayerContract_ExternalLayer_RespectsLayering()
    {
        var document = new ArchitectureContractDocument
        {
            Version = 1,
            Name = "Test",
            Layers = new Dictionary<string, ArchitectureLayer>
            {
                ["system"] = new() { Namespace = "System", External = true },
                ["core"] = new() { Namespace = "ArchLinterNet.Core" }
            },
            Analysis = new ArchitectureAnalysisConfiguration
            {
                TargetAssemblies = new List<string> { "ArchLinterNet.Core" }
            },
            Contracts = new ArchitectureContractGroups
            {
                StrictLayers = new List<ArchitectureLayerContract>
                {
                    new()
                    {
                        Name = "layering",
                        Layers = new List<string> { "system", "core" }
                    }
                }
            }
        };

        var context = new ArchitectureAnalysisContext(
            "/tmp",
            new[] { typeof(ArchitecturePolicyDocumentLoader).Assembly },
            Array.Empty<string>(),
            Array.Empty<string>());

        var runner = new ArchitectureContractRunner(context, document);
        var violations = runner.CheckLayerContract(document.Contracts.StrictLayers[0]);

        // Core (index 1) references System types (index 0) -> violations
        Assert.That(violations, Is.Not.Empty);
    }

    [Test]
    public void CheckIndependenceContract_ExternalLayer_EnforcesIsolation()
    {
        var document = new ArchitectureContractDocument
        {
            Version = 1,
            Name = "Test",
            Layers = new Dictionary<string, ArchitectureLayer>
            {
                ["core"] = new() { Namespace = "ArchLinterNet.Core" },
                ["system"] = new() { Namespace = "System", External = true }
            },
            Analysis = new ArchitectureAnalysisConfiguration
            {
                TargetAssemblies = new List<string> { "ArchLinterNet.Core" }
            },
            Contracts = new ArchitectureContractGroups
            {
                StrictIndependence = new List<ArchitectureIndependenceContract>
                {
                    new()
                    {
                        Name = "isolation",
                        Layers = new List<string> { "core", "system" }
                    }
                }
            }
        };

        var context = new ArchitectureAnalysisContext(
            "/tmp",
            new[] { typeof(ArchitecturePolicyDocumentLoader).Assembly },
            Array.Empty<string>(),
            Array.Empty<string>());

        var runner = new ArchitectureContractRunner(context, document);
        var violations = runner.CheckIndependenceContract(document.Contracts.StrictIndependence[0]);

        // Core references System types -> violations from core side
        Assert.That(violations, Is.Not.Empty);
        // External layer (system) has no types loaded -> no violations from that side
        Assert.That(violations.All(v => v.SourceType.StartsWith("ArchLinterNet.Core.")), Is.True);
    }
}
