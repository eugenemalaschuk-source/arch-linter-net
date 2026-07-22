using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Resolution;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class LayerResolverExclusionTests
{
    [Test]
    public void MatchesNamespace_ExcludedNamespace_ReturnsFalse()
    {
        ArchitectureLayer layer = new()
        {
            Namespace = "Product.Modules.*",
            Exclude = new List<ArchitectureLayerExclusion>
            {
                new() { Namespace = "Product.Modules.*.Infrastructure" }
            }
        };

        bool result = ArchitectureLayerResolver.MatchesNamespace(layer, "Product.Modules.Weather.Infrastructure");

        Assert.That(result, Is.False);
    }

    [Test]
    public void MatchesNamespace_NamespaceOutsideExcludeEntry_StillMatches()
    {
        ArchitectureLayer layer = new()
        {
            Namespace = "Product.Modules.*",
            Exclude = new List<ArchitectureLayerExclusion>
            {
                new() { Namespace = "Product.Modules.*.Infrastructure" }
            }
        };

        bool result = ArchitectureLayerResolver.MatchesNamespace(layer, "Product.Modules.Weather.Domain");

        Assert.That(result, Is.True);
    }

    [Test]
    public void MatchesNamespace_NoExcludeList_IsUnchangedFromBaselineBehavior()
    {
        ArchitectureLayer layer = new() { Namespace = "Product.Modules.*" };

        Assert.That(ArchitectureLayerResolver.MatchesNamespace(layer, "Product.Modules.Weather.Infrastructure"), Is.True);
        Assert.That(ArchitectureLayerResolver.MatchesNamespace(layer, "Product.Other"), Is.False);
    }

    [Test]
    public void MatchesNamespace_MultipleExcludeEntries_AnyMatchExcludes()
    {
        ArchitectureLayer layer = new()
        {
            Namespace = "Product.Modules.*",
            Exclude = new List<ArchitectureLayerExclusion>
            {
                new() { Namespace = "Product.Modules.*.Infrastructure" },
                new() { Namespace = "Product.Modules.*.Persistence" }
            }
        };

        Assert.That(ArchitectureLayerResolver.MatchesNamespace(layer, "Product.Modules.Weather.Infrastructure"), Is.False);
        Assert.That(ArchitectureLayerResolver.MatchesNamespace(layer, "Product.Modules.Weather.Persistence"), Is.False);
        Assert.That(ArchitectureLayerResolver.MatchesNamespace(layer, "Product.Modules.Weather.Application"), Is.True);
    }

    [Test]
    public void MatchesNamespace_ExcludeWithSuffix_MatchesCorrectly()
    {
        ArchitectureLayer layer = new()
        {
            Namespace = "Product.Modules.*",
            Exclude = new List<ArchitectureLayerExclusion>
            {
                new() { Namespace = "Product.Modules", NamespaceSuffix = "Generated" }
            }
        };

        Assert.That(ArchitectureLayerResolver.MatchesNamespace(layer, "Product.Modules.Weather.Generated"), Is.False);
        Assert.That(ArchitectureLayerResolver.MatchesNamespace(layer, "Product.Modules.Weather.Domain"), Is.True);
    }

    [Test]
    public void ResolveContainingLayer_ExcludedNamespace_FallsBackToSeparateDeclaredLayer()
    {
        ArchitectureContractDocument document = new()
        {
            Layers = new Dictionary<string, ArchitectureLayer>
            {
                ["ModulesCore"] = new()
                {
                    Namespace = "Product.Modules.*",
                    Exclude = new List<ArchitectureLayerExclusion>
                    {
                        new() { Namespace = "Product.Modules.*.Infrastructure" }
                    }
                },
                ["Infrastructure"] = new() { Namespace = "Product.Modules.*.Infrastructure" }
            }
        };

        string? resolved = ArchitectureLayerResolver.ResolveContainingLayer(
            document,
            "Product.Modules.Weather.Infrastructure",
            new HashSet<string>(document.Layers.Keys, StringComparer.Ordinal));

        Assert.That(resolved, Is.EqualTo("Infrastructure"));
    }

    [Test]
    public void DescribeLayer_WithExclude_IncludesExcludingClause()
    {
        ArchitectureLayer layer = new()
        {
            Namespace = "Product.Modules.*",
            Exclude = new List<ArchitectureLayerExclusion>
            {
                new() { Namespace = "Product.Modules.*.Infrastructure" },
                new() { Namespace = "Product.Modules.*.Persistence" }
            }
        };

        string description = ArchitectureLayerResolver.DescribeLayer(layer);

        Assert.That(description, Is.EqualTo(
            "Product.Modules.* (excluding Product.Modules.*.Infrastructure, Product.Modules.*.Persistence)"));
    }

    [Test]
    public void DescribeLayer_WithoutExclude_IsUnchanged()
    {
        ArchitectureLayer layer = new() { Namespace = "Product.Modules.*" };

        Assert.That(ArchitectureLayerResolver.DescribeLayer(layer), Is.EqualTo("Product.Modules.*"));
    }

    [Test]
    public void FindMatchingExclusion_ReturnsMatchedEntry()
    {
        ArchitectureLayerExclusion infrastructure = new() { Namespace = "Product.Modules.*.Infrastructure" };
        ArchitectureLayer layer = new()
        {
            Namespace = "Product.Modules.*",
            Exclude = new List<ArchitectureLayerExclusion> { infrastructure }
        };

        ArchitectureLayerExclusion? matched = ArchitectureLayerResolver.FindMatchingExclusion(
            layer, "Product.Modules.Weather.Infrastructure");

        Assert.That(matched, Is.SameAs(infrastructure));
    }

    [Test]
    public void FindMatchingExclusion_NoMatch_ReturnsNull()
    {
        ArchitectureLayer layer = new()
        {
            Namespace = "Product.Modules.*",
            Exclude = new List<ArchitectureLayerExclusion>
            {
                new() { Namespace = "Product.Modules.*.Infrastructure" }
            }
        };

        ArchitectureLayerExclusion? matched = ArchitectureLayerResolver.FindMatchingExclusion(
            layer, "Product.Modules.Weather.Domain");

        Assert.That(matched, Is.Null);
    }
}
