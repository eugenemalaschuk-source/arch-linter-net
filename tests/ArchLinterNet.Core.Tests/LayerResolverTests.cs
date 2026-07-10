using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Resolution;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class LayerResolverTests
{
    private static readonly string[] TestCoreTestWeb = { "Test.Core", "Test.Web" };

    private ArchitectureContractDocument _document = null!;

    [SetUp]
    public void SetUp()
    {
        _document = new ArchitectureContractDocument
        {
            Version = 1,
            Name = "Test",
            Layers = new Dictionary<string, ArchitectureLayer>
            {
                ["core"] = new() { Namespace = "Test.Core" },
                ["web"] = new() { Namespace = "Test.Web" },
                ["domain"] = new() { Namespace = "Test.Domain", NamespaceSuffix = "Models" }
            }
        };
    }

    [Test]
    public void ResolveLayer_ExistingLayer_ReturnsLayer()
    {
        ArchitectureLayer layer = ArchitectureLayerResolver.ResolveLayer(_document, "test", "core");

        Assert.That(layer.Namespace, Is.EqualTo("Test.Core"));
    }

    [Test]
    public void ResolveLayer_UnknownLayer_ThrowsInvalidOperationException()
    {
        Assert.Throws<InvalidOperationException>(() =>
            ArchitectureLayerResolver.ResolveLayer(_document, "test", "nonexistent"));
    }

    [Test]
    public void ResolveLayerNamespace_ReturnsNamespace()
    {
        string ns = ArchitectureLayerResolver.ResolveLayerNamespace(_document, "test", "web");

        Assert.That(ns, Is.EqualTo("Test.Web"));
    }

    [Test]
    public void MatchesNamespace_ExactPrefix_ReturnsTrue()
    {
        ArchitectureLayer layer = new() { Namespace = "Test.Core" };

        bool result = ArchitectureLayerResolver.MatchesNamespace(layer, "Test.Core.Services");

        Assert.That(result, Is.True);
    }

    [Test]
    public void MatchesNamespace_DifferentPrefix_ReturnsFalse()
    {
        ArchitectureLayer layer = new() { Namespace = "Test.Core" };

        bool result = ArchitectureLayerResolver.MatchesNamespace(layer, "Test.Web.Services");

        Assert.That(result, Is.False);
    }

    [Test]
    public void MatchesNamespace_WithSuffix_MatchesCorrectly()
    {
        ArchitectureLayer layer = _document.Layers["domain"];

        Assert.That(ArchitectureLayerResolver.MatchesNamespace(layer, "Test.Domain.Fishing.Models"), Is.True);
        Assert.That(ArchitectureLayerResolver.MatchesNamespace(layer, "Test.Domain.Fishing.Fish.Models"), Is.True);
        Assert.That(ArchitectureLayerResolver.MatchesNamespace(layer, "Test.Domain.Fishing.Services"), Is.False);
    }

    [Test]
    public void DescribeLayer_WithoutSuffix_ReturnsNamespace()
    {
        ArchitectureLayer layer = new() { Namespace = "Test.Core" };

        string description = ArchitectureLayerResolver.DescribeLayer(layer);

        Assert.That(description, Is.EqualTo("Test.Core"));
    }

    [Test]
    public void DescribeLayer_WithSuffix_ReturnsDescriptiveFormat()
    {
        ArchitectureLayer layer = _document.Layers["domain"];

        string description = ArchitectureLayerResolver.DescribeLayer(layer);

        Assert.That(description, Is.EqualTo("Test.Domain.*.Models"));
    }

    [Test]
    public void IsProjectType_TypeInKnownLayer_ReturnsTrue()
    {
        bool result = ArchitectureLayerResolver.IsProjectType(_document, "Test.Core.Services.Foo");

        Assert.That(result, Is.True);
    }

    [Test]
    public void IsProjectType_TypeNotInAnyLayer_ReturnsFalse()
    {
        bool result = ArchitectureLayerResolver.IsProjectType(_document, "System.String");

        Assert.That(result, Is.False);
    }

    [Test]
    public void IsInAnyNamespace_MatchingPrefix_ReturnsTrue()
    {
        bool result = ArchitectureLayerResolver.IsInAnyNamespace(
            "Test.Core.Foo",
            TestCoreTestWeb);

        Assert.That(result, Is.True);
    }

    [Test]
    public void IsInAnyNamespace_NoMatchingPrefix_ReturnsFalse()
    {
        bool result = ArchitectureLayerResolver.IsInAnyNamespace(
            "Test.Domain.Foo",
            TestCoreTestWeb);

        Assert.That(result, Is.False);
    }

    [Test]
    public void MatchesNamespace_SiblingNamespace_ReturnsFalse()
    {
        ArchitectureLayer layer = new() { Namespace = "Test.Core" };

        bool result = ArchitectureLayerResolver.MatchesNamespace(layer, "Test.CoreExtra");

        Assert.That(result, Is.False);
    }

    [Test]
    public void IsProjectType_SiblingNamespace_ReturnsFalse()
    {
        bool result = ArchitectureLayerResolver.IsProjectType(_document, "Test.CoreExtra.Foo");

        Assert.That(result, Is.False);
    }

    [Test]
    public void IsInAnyNamespace_SiblingNamespace_ReturnsFalse()
    {
        bool result = ArchitectureLayerResolver.IsInAnyNamespace(
            "Test.CoreExtra.Foo",
            new[] { "Test.Core" });

        Assert.That(result, Is.False);
    }
}
