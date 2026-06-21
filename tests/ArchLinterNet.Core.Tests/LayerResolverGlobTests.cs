using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Resolution;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class LayerResolverGlobTests
{
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
                ["domain"] = new() { Namespace = "Test.Domain", NamespaceSuffix = "Models" },
                ["features"] = new() { Namespace = "Test.Features.*" },
                ["feature_models"] = new() { Namespace = "Test.Features.*", NamespaceSuffix = "Models" },
                ["feature_api_contracts"] = new() { Namespace = "Test.Features.*", NamespaceSuffix = "Api.Contracts" },
                ["audio"] = new() { Namespace = "Test.Features.Audio" }
            }
        };
    }

    [Test]
    public void MatchesNamespace_ExactLiteral_StillReturnsTrue()
    {
        var layer = new ArchitectureLayer { Namespace = "Test.Core" };

        bool result = ArchitectureLayerResolver.MatchesNamespace(layer, "Test.Core.Services");

        Assert.That(result, Is.True);
    }

    [Test]
    public void MatchesNamespace_LiteralSiblingBleed_StillReturnsFalse()
    {
        var layer = new ArchitectureLayer { Namespace = "Test.Core" };

        bool result = ArchitectureLayerResolver.MatchesNamespace(layer, "Test.CoreExtra");

        Assert.That(result, Is.False);
    }

    [Test]
    public void MatchesNamespace_GlobExactSegment_ReturnsTrue()
    {
        var layer = new ArchitectureLayer { Namespace = "Test.Features.*" };

        bool result = ArchitectureLayerResolver.MatchesNamespace(layer, "Test.Features.Audio");

        Assert.That(result, Is.True);
    }

    [Test]
    public void MatchesNamespace_GlobDescendantPrefix_ReturnsTrue()
    {
        var layer = new ArchitectureLayer { Namespace = "Test.Features.*" };

        bool result = ArchitectureLayerResolver.MatchesNamespace(layer, "Test.Features.Audio.Player");

        Assert.That(result, Is.True);
    }

    [Test]
    public void MatchesNamespace_GlobFewerSegments_ReturnsFalse()
    {
        var layer = new ArchitectureLayer { Namespace = "Test.Features.*" };

        bool result = ArchitectureLayerResolver.MatchesNamespace(layer, "Test.Features");

        Assert.That(result, Is.False);
    }

    [Test]
    public void MatchesNamespace_GlobDifferentLiteral_ReturnsFalse()
    {
        var layer = new ArchitectureLayer { Namespace = "Test.Features.*" };

        bool result = ArchitectureLayerResolver.MatchesNamespace(layer, "Test.Other.Services");

        Assert.That(result, Is.False);
    }

    [Test]
    public void MatchesNamespace_GlobWithSuffixFixedPosition_ReturnsTrue()
    {
        var layer = new ArchitectureLayer { Namespace = "Test.Features.*", NamespaceSuffix = "Models" };

        bool result = ArchitectureLayerResolver.MatchesNamespace(layer, "Test.Features.Audio.Models");

        Assert.That(result, Is.True);
    }

    [Test]
    public void MatchesNamespace_GlobWithSuffixDescendant_ReturnsTrue()
    {
        var layer = new ArchitectureLayer { Namespace = "Test.Features.*", NamespaceSuffix = "Models" };

        bool result = ArchitectureLayerResolver.MatchesNamespace(layer, "Test.Features.Audio.Models.Dto");

        Assert.That(result, Is.True);
    }

    [Test]
    public void MatchesNamespace_GlobWithMultiSegmentSuffix_ReturnsTrue()
    {
        var layer = new ArchitectureLayer { Namespace = "Test.Features.*", NamespaceSuffix = "Api.Contracts" };

        bool result = ArchitectureLayerResolver.MatchesNamespace(layer, "Test.Features.Audio.Api.Contracts");

        Assert.That(result, Is.True);
    }

    [Test]
    public void MatchesNamespace_GlobWithMultiSegmentSuffixDescendant_ReturnsTrue()
    {
        var layer = new ArchitectureLayer { Namespace = "Test.Features.*", NamespaceSuffix = "Api.Contracts" };

        bool result = ArchitectureLayerResolver.MatchesNamespace(layer, "Test.Features.Audio.Api.Contracts.Dto");

        Assert.That(result, Is.True);
    }

    [Test]
    public void MatchesNamespace_GlobWithSuffixWrongPosition_ReturnsFalse()
    {
        var layer = new ArchitectureLayer { Namespace = "Test.Features.*", NamespaceSuffix = "Models" };

        bool result = ArchitectureLayerResolver.MatchesNamespace(layer, "Test.Features.Audio.Internal.Models");

        Assert.That(result, Is.False);
    }

    [Test]
    public void MatchesNamespace_GlobWithMultiSegmentSuffixWrongPosition_ReturnsFalse()
    {
        var layer = new ArchitectureLayer { Namespace = "Test.Features.*", NamespaceSuffix = "Api.Contracts" };

        bool result = ArchitectureLayerResolver.MatchesNamespace(layer, "Test.Features.Audio.Internal.Api.Contracts");

        Assert.That(result, Is.False);
    }

    [Test]
    public void MatchesNamespace_LiteralWithSuffixOldBehavior_StillTrue()
    {
        var layer = new ArchitectureLayer { Namespace = "Test.Domain", NamespaceSuffix = "Models" };

        Assert.That(ArchitectureLayerResolver.MatchesNamespace(layer, "Test.Domain.Fishing.Models"), Is.True);
        Assert.That(ArchitectureLayerResolver.MatchesNamespace(layer, "Test.Domain.Fishing.Fish.Models"), Is.True);
        Assert.That(ArchitectureLayerResolver.MatchesNamespace(layer, "Test.Domain.Fishing.Services"), Is.False);
    }

    [Test]
    public void MatchNamespace_Glob_ReturnsMatchedNamespacePrefix()
    {
        var layer = new ArchitectureLayer { Namespace = "Test.Features.*" };

        ArchitectureNamespaceMatch result = ArchitectureLayerResolver.MatchNamespace(layer, "Test.Features.Audio.Player");

        Assert.That(result.Matched, Is.True);
        Assert.That(result.MatchedNamespacePrefix, Is.EqualTo("Test.Features.Audio"));
    }

    [Test]
    public void MatchNamespace_GlobWithSuffix_ReturnsMatchedNamespacePrefixIncludingSuffix()
    {
        var layer = new ArchitectureLayer { Namespace = "Test.Features.*", NamespaceSuffix = "Models" };

        ArchitectureNamespaceMatch result = ArchitectureLayerResolver.MatchNamespace(layer, "Test.Features.Audio.Models.Dto");

        Assert.That(result.Matched, Is.True);
        Assert.That(result.MatchedNamespacePrefix, Is.EqualTo("Test.Features.Audio.Models"));
    }

    [Test]
    public void MatchNamespace_GlobWithMultiSegmentSuffix_ReturnsMatchedNamespacePrefixIncludingSuffix()
    {
        var layer = new ArchitectureLayer { Namespace = "Test.Features.*", NamespaceSuffix = "Api.Contracts" };

        ArchitectureNamespaceMatch result = ArchitectureLayerResolver.MatchNamespace(layer, "Test.Features.Audio.Api.Contracts.Dto");

        Assert.That(result.Matched, Is.True);
        Assert.That(result.MatchedNamespacePrefix, Is.EqualTo("Test.Features.Audio.Api.Contracts"));
    }

    [Test]
    public void ArchitectureLayer_NamespaceMutation_InvalidatesGlobCache()
    {
        var layer = new ArchitectureLayer { Namespace = "Test.Features.*" };

        Assert.That(ArchitectureLayerResolver.MatchesNamespace(layer, "Test.Features.Audio"), Is.True);

        layer.Namespace = "Test.Modules.*";

        Assert.That(ArchitectureLayerResolver.MatchesNamespace(layer, "Test.Features.Audio"), Is.False);
        Assert.That(ArchitectureLayerResolver.MatchesNamespace(layer, "Test.Modules.Audio"), Is.True);
    }

    [Test]
    public void MatchNamespace_Literal_ReturnsNullPrefix()
    {
        var layer = new ArchitectureLayer { Namespace = "Test.Core" };

        ArchitectureNamespaceMatch result = ArchitectureLayerResolver.MatchNamespace(layer, "Test.Core.Services");

        Assert.That(result.Matched, Is.True);
        Assert.That(result.MatchedNamespacePrefix, Is.Null);
    }

    [Test]
    public void DescribeLayer_NoSuffixNoGlob_ReturnsNamespace()
    {
        var layer = new ArchitectureLayer { Namespace = "Test.Core" };

        string desc = ArchitectureLayerResolver.DescribeLayer(layer);

        Assert.That(desc, Is.EqualTo("Test.Core"));
    }

    [Test]
    public void DescribeLayer_WithSuffixNoGlob_ReturnsPreviewFormat()
    {
        var layer = new ArchitectureLayer { Namespace = "Test.Domain", NamespaceSuffix = "Models" };

        string desc = ArchitectureLayerResolver.DescribeLayer(layer);

        Assert.That(desc, Is.EqualTo("Test.Domain.*.Models"));
    }

    [Test]
    public void DescribeLayer_GlobNoSuffix_ReturnsPattern()
    {
        var layer = new ArchitectureLayer { Namespace = "Test.Features.*" };

        string desc = ArchitectureLayerResolver.DescribeLayer(layer);

        Assert.That(desc, Is.EqualTo("Test.Features.*"));
    }

    [Test]
    public void DescribeLayer_GlobWithSuffix_ReturnsCombinedPattern()
    {
        var layer = new ArchitectureLayer { Namespace = "Test.Features.*", NamespaceSuffix = "Models" };

        string desc = ArchitectureLayerResolver.DescribeLayer(layer);

        Assert.That(desc, Is.EqualTo("Test.Features.*.Models"));
    }

    [Test]
    public void ResolveContainingLayer_LiteralBeatsGlob()
    {
        string? result = ArchitectureLayerResolver.ResolveContainingLayer(
            _document,
            "Test.Features.Audio.Player",
            new HashSet<string> { "features", "audio" });

        Assert.That(result, Is.EqualTo("audio"));
    }

    [Test]
    public void ResolveContainingLayer_GlobWhenNoLiteralMatch()
    {
        string? result = ArchitectureLayerResolver.ResolveContainingLayer(
            _document,
            "Test.Features.Fishing.Services",
            new HashSet<string> { "features", "audio" });

        Assert.That(result, Is.EqualTo("features"));
    }

    [Test]
    public void ResolveContainingLayer_MoreLiteralSegmentsBeatsFewer()
    {
        _document.Layers["features_broad"] = new() { Namespace = "Test.*" };

        string? result = ArchitectureLayerResolver.ResolveContainingLayer(
            _document,
            "Test.Features.Audio.Player",
            new HashSet<string> { "features_broad", "features" });

        Assert.That(result, Is.EqualTo("features"));
    }

    [Test]
    public void ResolveContainingLayer_BroadLiteralBeatsSpecificGlob()
    {
        _document.Layers["broad_literal"] = new() { Namespace = "Test" };
        _document.Layers["specific_glob"] = new() { Namespace = "Test.Features.*" };

        string? result = ArchitectureLayerResolver.ResolveContainingLayer(
            _document,
            "Test.Features.Audio.Player",
            new HashSet<string> { "broad_literal", "specific_glob" });

        Assert.That(result, Is.EqualTo("broad_literal"));
    }

    [Test]
    public void ResolveContainingLayer_StableTiebreakerByName()
    {
        _document.Layers["alpha"] = new() { Namespace = "Beta.*" };
        _document.Layers["beta"] = new() { Namespace = "Beta.*" };

        string? result = ArchitectureLayerResolver.ResolveContainingLayer(
            _document,
            "Beta.Gamma.Delta",
            new HashSet<string> { "beta", "alpha" });

        Assert.That(result, Is.EqualTo("alpha"));
    }

    [Test]
    public void IsProjectType_GlobLayer_ReturnsTrue()
    {
        bool result = ArchitectureLayerResolver.IsProjectType(_document, "Test.Features.Audio.Player");

        Assert.That(result, Is.True);
    }

    [Test]
    public void IsProjectType_TypeOutsideAllLayers_ReturnsFalse()
    {
        bool result = ArchitectureLayerResolver.IsProjectType(_document, "System.String");

        Assert.That(result, Is.False);
    }
}
