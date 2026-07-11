using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Validators;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Resolution;
using AttributeRoleExtractionTestFixtures;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class LayerResolverTests
{
    private static readonly string[] _testCoreTestWeb = { "Test.Core", "Test.Web" };

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
            _testCoreTestWeb);

        Assert.That(result, Is.True);
    }

    [Test]
    public void IsInAnyNamespace_NoMatchingPrefix_ReturnsFalse()
    {
        bool result = ArchitectureLayerResolver.IsInAnyNamespace(
            "Test.Domain.Foo",
            _testCoreTestWeb);

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

    [Test]
    public void FindTypesInLayer_SelectorOnlyMatchesRole()
    {
        var index = new ArchitectureRoleIndex(
            new ArchitectureClassificationConfiguration
            {
                Attributes =
                {
                    new ArchitectureAttributeClassificationMapping
                    {
                        Attribute = "AttributeRoleExtractionTestFixtures.DomainMarkerAttribute",
                        Role = "DomainLayer"
                    }
                }
            },
            new ArchitectureTypeIndex(new[] { typeof(TypeWithConstructorDefault).Assembly }));

        ArchitectureLayer layer = new()
        {
            Selector = new ArchitectureLayerSelector { Role = "DomainLayer" }
        };

        Type[] matches = new ArchitectureTypeIndex(new[] { typeof(TypeWithConstructorDefault).Assembly })
            .FindTypesInLayer(layer, index);

        Assert.That(matches, Does.Contain(typeof(TypeWithConstructorDefault)));
        Assert.That(matches, Does.Not.Contain(typeof(PlainType)));
    }

    [Test]
    public void FindTypesInLayer_NamespaceAndSelectorRequireBoth()
    {
        var index = new ArchitectureRoleIndex(
            new ArchitectureClassificationConfiguration
            {
                Attributes =
                {
                    new ArchitectureAttributeClassificationMapping
                    {
                        Attribute = "AttributeRoleExtractionTestFixtures.DomainMarkerAttribute",
                        Role = "DomainLayer"
                    }
                }
            },
            new ArchitectureTypeIndex(new[] { typeof(TypeWithConstructorDefault).Assembly }));

        ArchitectureLayer layer = new()
        {
            Namespace = "AttributeRoleExtractionTestFixtures",
            Selector = new ArchitectureLayerSelector { Role = "OtherLayer" }
        };

        Type[] matches = new ArchitectureTypeIndex(new[] { typeof(TypeWithConstructorDefault).Assembly })
            .FindTypesInLayer(layer, index);

        Assert.That(matches, Is.Empty);
    }

    [Test]
    public void FindTypesInLayer_SelectorMetadataUsesExactAndMatching()
    {
        var classification = new ArchitectureClassificationConfiguration
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
        };
        var typeIndex = new ArchitectureTypeIndex(new[] { typeof(TypeWithBooleanProperty).Assembly });
        var roleIndex = new ArchitectureRoleIndex(classification, typeIndex);
        ArchitectureLayer layer = new()
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
        };

        Type[] matches = typeIndex.FindTypesInLayer(layer, roleIndex);

        Assert.That(matches, Does.Contain(typeof(TypeWithBooleanProperty)));
        Assert.That(matches, Does.Not.Contain(typeof(TypeWithConstructorDefault)));
    }

    [Test]
    public void FindTypesInLayer_SelectorMetadataComparesNumericValuesByValue()
    {
        var classification = new ArchitectureClassificationConfiguration
        {
            Attributes =
            {
                new ArchitectureAttributeClassificationMapping
                {
                    Attribute = "AttributeRoleExtractionTestFixtures.DomainMarkerAttribute",
                    Role = "DomainLayer",
                    Metadata = new Dictionary<string, object> { ["priority"] = 5 }
                }
            }
        };
        var typeIndex = new ArchitectureTypeIndex(new[] { typeof(TypeWithConstructorDefault).Assembly });
        var roleIndex = new ArchitectureRoleIndex(classification, typeIndex);
        ArchitectureLayer layer = new()
        {
            Selector = new ArchitectureLayerSelector
            {
                Role = "DomainLayer",
                Metadata = new Dictionary<string, object> { ["priority"] = 5.0 }
            }
        };

        Type[] matches = typeIndex.FindTypesInLayer(layer, roleIndex);

        Assert.That(matches, Does.Contain(typeof(TypeWithConstructorDefault)));
    }

    [Test]
    public void DescribeLayer_SelectorOnlyLayer_UsesSelectorDescription()
    {
        ArchitectureLayer layer = new()
        {
            Selector = new ArchitectureLayerSelector
            {
                Role = "DomainLayer",
                Metadata = new Dictionary<string, object> { ["domain"] = "Sales" }
            }
        };

        Assert.That(ArchitectureLayerResolver.DescribeLayer(layer),
            Is.EqualTo("selector(role: DomainLayer, metadata: domain=Sales)"));
    }

    [Test]
    public void ResolveContainingLayer_SelectorOnlyLayer_IsIgnoredByNamespaceResolution()
    {
        ArchitectureContractDocument document = new()
        {
            Layers = new Dictionary<string, ArchitectureLayer>
            {
                ["semantic"] = new()
                {
                    Selector = new ArchitectureLayerSelector { Role = "DomainLayer" }
                },
                ["core"] = new() { Namespace = "Test.Core" }
            }
        };

        string? layer = ArchitectureLayerResolver.ResolveContainingLayer(
            document,
            "Test.Core.Services",
            new HashSet<string>(new[] { "semantic", "core" }, StringComparer.Ordinal));

        Assert.That(layer, Is.EqualTo("core"));
    }

    [Test]
    public void MatchesNamespace_SelectorOnlyLayer_ReturnsFalseWithoutThrowing()
    {
        Assert.That(
            ArchitectureLayerResolver.MatchesNamespace(
                new ArchitectureLayer
                {
                    Selector = new ArchitectureLayerSelector { Role = "DomainLayer" }
                },
                "Test.Domain"),
            Is.False);
    }

    [Test]
    public void LayerValidation_SelectorOnlyLayer_IsValid()
    {
        Assert.DoesNotThrow(() => new LayerNamespacesValidator().Validate(
            new ArchitectureContractDocument
            {
                Layers = new Dictionary<string, ArchitectureLayer>
                {
                    ["domain"] = new()
                    {
                        Selector = new ArchitectureLayerSelector { Role = "DomainLayer" }
                    }
                }
            }));
    }

    [Test]
    public void LayerValidation_SelectorWithoutRole_IsRejected()
    {
        Assert.Throws<InvalidOperationException>(() => new LayerNamespacesValidator().Validate(
            new ArchitectureContractDocument
            {
                Layers = new Dictionary<string, ArchitectureLayer>
                {
                    ["domain"] = new() { Selector = new ArchitectureLayerSelector() }
                }
            }));
    }

    [Test]
    public void LayerValidation_SelectorWithEmptyMetadataKey_IsRejected()
    {
        Assert.Throws<InvalidOperationException>(() => new LayerNamespacesValidator().Validate(
            new ArchitectureContractDocument
            {
                Layers = new Dictionary<string, ArchitectureLayer>
                {
                    ["domain"] = new()
                    {
                        Selector = new ArchitectureLayerSelector
                        {
                            Role = "DomainLayer",
                            Metadata = new Dictionary<string, object> { [string.Empty] = "Sales" }
                        }
                    }
                }
            }));
    }

    [Test]
    public void LayerValidation_WithoutNamespaceOrSelector_IsRejected()
    {
        Assert.Throws<InvalidOperationException>(() => new LayerNamespacesValidator().Validate(
            new ArchitectureContractDocument
            {
                Layers = new Dictionary<string, ArchitectureLayer>
                {
                    ["domain"] = new()
                }
            }));
    }
}
