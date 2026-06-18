using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Resolution;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class LayerResolverNamespaceBleedTests
{
    [Test]
    public void MatchesNamespace_ExactMatch_ReturnsTrue()
    {
        var layer = new ArchitectureLayer { Namespace = "MyApp.Core" };

        bool result = ArchitectureLayerResolver.MatchesNamespace(layer, "MyApp.Core");

        Assert.That(result, Is.True);
    }

    [Test]
    public void MatchesNamespace_ChildNamespace_ReturnsTrue()
    {
        var layer = new ArchitectureLayer { Namespace = "MyApp.Core" };

        bool result = ArchitectureLayerResolver.MatchesNamespace(layer, "MyApp.Core.Services");

        Assert.That(result, Is.True);
    }

    [Test]
    public void MatchesNamespace_SiblingNamespace_ReturnsFalse()
    {
        var layer = new ArchitectureLayer { Namespace = "MyApp.Core" };

        bool result = ArchitectureLayerResolver.MatchesNamespace(layer, "MyApp.CoreExtra");

        Assert.That(result, Is.False);
    }

    [Test]
    public void MatchesNamespace_SiblingWithNumber_ReturnsFalse()
    {
        var layer = new ArchitectureLayer { Namespace = "MyApp.Core" };

        bool result = ArchitectureLayerResolver.MatchesNamespace(layer, "MyApp.Core2");

        Assert.That(result, Is.False);
    }

    [Test]
    public void MatchesPrefix_ExactMatch_ReturnsTrue()
    {
        bool result = ArchitectureLayerResolver.MatchesPrefix("MyApp.Core", "MyApp.Core");

        Assert.That(result, Is.True);
    }

    [Test]
    public void MatchesPrefix_ChildNamespace_ReturnsTrue()
    {
        bool result = ArchitectureLayerResolver.MatchesPrefix("MyApp.Core.Services", "MyApp.Core");

        Assert.That(result, Is.True);
    }

    [Test]
    public void MatchesPrefix_SiblingNamespace_ReturnsFalse()
    {
        bool result = ArchitectureLayerResolver.MatchesPrefix("MyApp.CoreExtra", "MyApp.Core");

        Assert.That(result, Is.False);
    }

    [Test]
    public void MatchesPrefix_SuffixWithDot_ReturnsFalse()
    {
        bool result = ArchitectureLayerResolver.MatchesPrefix("MyApp.Core.Services.Helper", "MyApp.Core.Services");

        Assert.That(result, Is.True);
    }

    [Test]
    public void MatchesPrefix_SuffixWithoutDot_ReturnsFalse()
    {
        bool result = ArchitectureLayerResolver.MatchesPrefix("MyApp.CoreServices", "MyApp.Core.Services");

        Assert.That(result, Is.False);
    }
}
