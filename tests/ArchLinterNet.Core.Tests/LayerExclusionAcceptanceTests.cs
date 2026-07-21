using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Families;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Resolution;
using NUnit.Framework;
using ArchitectureContractGroups = ArchLinterNet.Core.Contracts.Families.ArchitectureContractGroups;

namespace ArchLinterNet.Core.Tests;

// Covers the two acceptance scenarios from issue #356 (subtractive/exclusion selectors):
// a minimal single-project policy and a multi-module policy proving an external-dependency
// rule's scope narrows via layer exclusion instead of per-module enumeration.
[TestFixture]
public sealed class LayerExclusionAcceptanceTests
{
    private static ArchitectureAnalysisContext CreateContext()
    {
        return new ArchitectureAnalysisContext(
            "/tmp",
            new[] { typeof(LayerExclusionAcceptanceTests).Assembly },
            Array.Empty<string>(),
            Array.Empty<string>());
    }

    [Test]
    public void MinimalPolicy_ProductWildcardExcludingGenerated_GeneratedTypeFallsOutOfLayer()
    {
        ArchitectureLayer layer = new()
        {
            Namespace = "LayerExclusionAcceptanceFixtures.Product.*",
            Exclude = new List<ArchitectureLayerExclusion>
            {
                new() { Namespace = "LayerExclusionAcceptanceFixtures.Product.Generated" }
            }
        };

        Assert.That(
            ArchitectureLayerResolver.MatchesNamespace(layer, "LayerExclusionAcceptanceFixtures.Product.Core"),
            Is.True);
        Assert.That(
            ArchitectureLayerResolver.MatchesNamespace(
                layer, "LayerExclusionAcceptanceFixtures.Product.Generated"),
            Is.False);
    }

    [Test]
    public void MultiModulePolicy_VendorSdkBlockedInDomainAllowedInOwnedInfrastructureAndPersistence()
    {
        var document = new ArchitectureContractDocument
        {
            Version = 1,
            Name = "Multi-module exclusion test",
            Layers = new Dictionary<string, ArchitectureLayer>
            {
                ["ModulesCore"] = new()
                {
                    Namespace = "LayerExclusionAcceptanceFixtures.Modules.*",
                    Exclude = new List<ArchitectureLayerExclusion>
                    {
                        new() { Namespace = "LayerExclusionAcceptanceFixtures.Modules.*.Infrastructure" },
                        new() { Namespace = "LayerExclusionAcceptanceFixtures.Modules.*.Persistence" }
                    }
                }
            },
            ExternalDependencies = new Dictionary<string, ArchitectureExternalDependencyGroup>
            {
                ["vendor_persistence_sdk"] = new()
                {
                    NamespacePrefixes = new List<string> { "LayerExclusionAcceptanceFixtures.VendorPersistenceSdk" }
                }
            },
            Analysis = new ArchitectureAnalysisConfiguration
            {
                TargetAssemblies = new List<string> { typeof(LayerExclusionAcceptanceTests).Assembly.GetName().Name! }
            },
            Contracts = new ArchitectureContractGroups
            {
                StrictExternal = new List<ArchitectureExternalDependencyContract>
                {
                    new()
                    {
                        Name = "modules-no-vendor-persistence-sdk",
                        Source = "ModulesCore",
                        Forbidden = new List<string> { "vendor_persistence_sdk" }
                    }
                }
            }
        };

        var runner = new ArchitectureContractRunner(CreateContext(), document);
        var violations = runner.CheckExternalContract(document.Contracts.StrictExternal[0]);

        Assert.That(violations.Any(v => v.SourceType.Contains("WeatherAggregateUsingVendorSdk")), Is.True,
            "Domain type referencing the vendor SDK is still within ModulesCore's scope and must be blocked.");
        Assert.That(violations.Any(v => v.SourceType.Contains("WeatherPersistenceAdapter")), Is.False,
            "Infrastructure type is excluded from ModulesCore's scope, so the rule no longer applies to it.");
        Assert.That(violations.Any(v => v.SourceType.Contains("WeatherRepository")), Is.False,
            "Persistence type is excluded from ModulesCore's scope, so the rule no longer applies to it.");
    }
}
