using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Scanning;
using AttributeRoleExtractionTestFixtures;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class ArchitectureAttributeRoleExtractorTests
{
    private const string DomainMarkerAttributeName = "AttributeRoleExtractionTestFixtures.DomainMarkerAttribute";
    private const string SecondMarkerAttributeName = "AttributeRoleExtractionTestFixtures.SecondMarkerAttribute";
    private const string BoundedContextMarkerAttributeName = "AttributeRoleExtractionTestFixtures.BoundedContextMarkerAttribute";

    private static Type[] TypeUniverse => typeof(PlainType).Assembly.GetTypes();

    private static ArchitectureAttributeRoleExtractor CreateExtractor(ArchitectureClassificationConfiguration configuration)
    {
        return new ArchitectureAttributeRoleExtractor(configuration, TypeUniverse);
    }

    private static ArchitectureAttributeClassificationMapping DomainMapping(
        string role = "DomainLayer", Dictionary<string, object>? metadata = null)
    {
        return new ArchitectureAttributeClassificationMapping
        {
            Attribute = DomainMarkerAttributeName,
            Role = role,
            Metadata = metadata ?? new Dictionary<string, object>()
        };
    }

    [Test]
    public void Extract_TypeWithNoMatchingAttribute_HasNoRole()
    {
        var configuration = new ArchitectureClassificationConfiguration
        {
            Attributes = { DomainMapping() }
        };

        ArchitectureTypeClassificationResult result = CreateExtractor(configuration).Extract(typeof(PlainType));

        Assert.That(result.Role, Is.Null);
        Assert.That(result.Source, Is.Null);
    }

    [Test]
    public void Extract_ConstructorArgument_ResolvesCompilerSuppliedDefault()
    {
        var configuration = new ArchitectureClassificationConfiguration
        {
            Attributes =
            {
                DomainMapping(metadata: new Dictionary<string, object>
                {
                    ["domain"] = "constructor[0]",
                    ["module"] = "constructor[1]"
                })
            }
        };

        ArchitectureTypeClassificationResult result = CreateExtractor(configuration).Extract(typeof(TypeWithConstructorDefault));

        Assert.That(result.Role, Is.EqualTo("DomainLayer"));
        Assert.That(result.Source, Is.EqualTo(ArchitectureClassificationSource.TypeAttribute));
        Assert.That(result.Metadata["domain"], Is.EqualTo("Sales"));
        Assert.That(result.Metadata["module"], Is.EqualTo("UnknownModule"));
    }

    [Test]
    public void Extract_ConstructorIndexOutOfRange_OmitsKeyButKeepsRole()
    {
        var configuration = new ArchitectureClassificationConfiguration
        {
            Attributes =
            {
                DomainMapping(metadata: new Dictionary<string, object> { ["missing"] = "constructor[5]" })
            }
        };

        ArchitectureTypeClassificationResult result = CreateExtractor(configuration).Extract(typeof(TypeWithConstructorDefault));

        Assert.That(result.Role, Is.EqualTo("DomainLayer"));
        Assert.That(result.Metadata.ContainsKey("missing"), Is.False);
        Assert.That(result.MetadataFailures, Has.Count.EqualTo(1));
        Assert.That(result.MetadataFailures[0].MetadataKey, Is.EqualTo("missing"));
    }

    [Test]
    public void Extract_NamedPropertySupplied_ResolvesValue()
    {
        var configuration = new ArchitectureClassificationConfiguration
        {
            Attributes =
            {
                DomainMapping(metadata: new Dictionary<string, object> { ["module"] = "property:Module" })
            }
        };

        ArchitectureTypeClassificationResult result = CreateExtractor(configuration).Extract(typeof(TypeWithSuppliedNamedProperty));

        Assert.That(result.Metadata["module"], Is.EqualTo("Checkout"));
    }

    [Test]
    public void Extract_NamedPropertyNotSupplied_OmitsKeyButKeepsRole()
    {
        var configuration = new ArchitectureClassificationConfiguration
        {
            Attributes =
            {
                DomainMapping(metadata: new Dictionary<string, object> { ["module"] = "property:Module" })
            }
        };

        ArchitectureTypeClassificationResult result = CreateExtractor(configuration).Extract(typeof(TypeWithUnsuppliedNamedProperty));

        Assert.That(result.Role, Is.EqualTo("DomainLayer"));
        Assert.That(result.Metadata.ContainsKey("module"), Is.False);
        Assert.That(result.MetadataFailures, Has.Count.EqualTo(1));
    }

    [Test]
    public void Extract_BooleanProperty_CanonicalizesToBoolean()
    {
        var configuration = new ArchitectureClassificationConfiguration
        {
            Attributes =
            {
                DomainMapping(metadata: new Dictionary<string, object> { ["enabled"] = "property:Enabled" })
            }
        };

        ArchitectureTypeClassificationResult result = CreateExtractor(configuration).Extract(typeof(TypeWithBooleanProperty));

        Assert.That(result.Metadata["enabled"], Is.EqualTo(true));
    }

    [Test]
    public void Extract_EnumProperty_CanonicalizesToDeclaredMemberName()
    {
        var configuration = new ArchitectureClassificationConfiguration
        {
            Attributes =
            {
                DomainMapping(metadata: new Dictionary<string, object> { ["tier"] = "property:Tier" })
            }
        };

        ArchitectureTypeClassificationResult result = CreateExtractor(configuration).Extract(typeof(TypeWithEnumProperty));

        Assert.That(result.Metadata["tier"], Is.EqualTo("Domain"));
    }

    [Test]
    public void Extract_AliasedEnumProperty_IsEvidenceExtractionFailure()
    {
        var configuration = new ArchitectureClassificationConfiguration
        {
            Attributes =
            {
                DomainMapping(metadata: new Dictionary<string, object> { ["aliasTier"] = "property:AliasTier" })
            }
        };

        ArchitectureTypeClassificationResult result = CreateExtractor(configuration).Extract(typeof(TypeWithAliasedEnumProperty));

        Assert.That(result.Role, Is.EqualTo("DomainLayer"));
        Assert.That(result.Metadata.ContainsKey("aliasTier"), Is.False);
        Assert.That(result.MetadataFailures, Has.Count.EqualTo(1));
    }

    [Test]
    public void Extract_ConstStringAndDecimalFields_ResolveSuccessfully()
    {
        var configuration = new ArchitectureClassificationConfiguration
        {
            Attributes =
            {
                DomainMapping(metadata: new Dictionary<string, object>
                {
                    ["owner"] = "const:AttributeRoleExtractionTestFixtures.Constants.Owner",
                    ["priority"] = "const:AttributeRoleExtractionTestFixtures.Constants.Priority",
                    ["ratio"] = "const:AttributeRoleExtractionTestFixtures.Constants.Ratio"
                })
            }
        };

        ArchitectureTypeClassificationResult result = CreateExtractor(configuration).Extract(typeof(TypeWithConstructorDefault));

        Assert.That(result.Metadata["owner"], Is.EqualTo("platform-team"));
        Assert.That(result.Metadata["priority"], Is.EqualTo(5m));
        Assert.That(result.Metadata["ratio"], Is.EqualTo(1.5m));
    }

    [Test]
    public void Extract_ConstReferenceToStaticReadonlyField_IsEvidenceExtractionFailure()
    {
        var configuration = new ArchitectureClassificationConfiguration
        {
            Attributes =
            {
                DomainMapping(metadata: new Dictionary<string, object>
                {
                    ["notConst"] = "const:AttributeRoleExtractionTestFixtures.Constants.NotConst"
                })
            }
        };

        ArchitectureTypeClassificationResult result = CreateExtractor(configuration).Extract(typeof(TypeWithConstructorDefault));

        Assert.That(result.Role, Is.EqualTo("DomainLayer"));
        Assert.That(result.Metadata.ContainsKey("notConst"), Is.False);
        Assert.That(result.MetadataFailures, Has.Count.EqualTo(1));
    }

    [Test]
    public void Extract_LiteralScalarMetadata_UsedVerbatim()
    {
        var configuration = new ArchitectureClassificationConfiguration
        {
            Attributes =
            {
                DomainMapping(metadata: new Dictionary<string, object> { ["owner"] = "platform-team" })
            }
        };

        ArchitectureTypeClassificationResult result = CreateExtractor(configuration).Extract(typeof(TypeWithConstructorDefault));

        Assert.That(result.Metadata["owner"], Is.EqualTo("platform-team"));
    }

    [Test]
    public void Extract_SameTierConflictingEntries_FirstDeclaredWinsAndConflictIsRecorded()
    {
        var configuration = new ArchitectureClassificationConfiguration
        {
            Attributes =
            {
                DomainMapping("DomainLayer"),
                new ArchitectureAttributeClassificationMapping { Attribute = SecondMarkerAttributeName, Role = "InfrastructureLayer" }
            }
        };

        ArchitectureTypeClassificationResult result = CreateExtractor(configuration).Extract(typeof(TypeWithConflictingEntries));

        Assert.That(result.Role, Is.EqualTo("DomainLayer"));
        Assert.That(result.Conflicts, Has.Count.EqualTo(1));
        Assert.That(result.Conflicts[0].WinningRole, Is.EqualTo("DomainLayer"));
        Assert.That(result.Conflicts[0].DiscardedRole, Is.EqualTo("InfrastructureLayer"));
    }

    [Test]
    public void Extract_IdenticalRepeatedInstances_NoConflictRecorded()
    {
        var configuration = new ArchitectureClassificationConfiguration
        {
            Attributes = { DomainMapping() }
        };

        ArchitectureTypeClassificationResult result = CreateExtractor(configuration).Extract(typeof(TypeWithIdenticalRepeatedInstances));

        Assert.That(result.Role, Is.EqualTo("DomainLayer"));
        Assert.That(result.Conflicts, Is.Empty);
    }

    [Test]
    public void Extract_DifferingRepeatedInstances_FirstInMetadataOrderWinsAndConflictIsRecorded()
    {
        var configuration = new ArchitectureClassificationConfiguration
        {
            Attributes =
            {
                DomainMapping(metadata: new Dictionary<string, object> { ["domain"] = "constructor[0]" })
            }
        };

        ArchitectureTypeClassificationResult result = CreateExtractor(configuration).Extract(typeof(TypeWithDifferingRepeatedInstances));

        Assert.That(result.Role, Is.EqualTo("DomainLayer"));
        Assert.That(result.Metadata["domain"], Is.EqualTo("Sales"));
        Assert.That(result.Conflicts, Has.Count.EqualTo(1));
    }

    [Test]
    public void Extract_AssemblyAttributeOnly_AssignsAssemblyRole()
    {
        var configuration = new ArchitectureClassificationConfiguration
        {
            AssemblyAttributes =
            {
                new ArchitectureAttributeClassificationMapping { Attribute = BoundedContextMarkerAttributeName, Role = "ApplicationLayer" }
            }
        };

        ArchitectureTypeClassificationResult result = CreateExtractor(configuration).Extract(typeof(TypeRelyingOnAssemblyAttribute));

        Assert.That(result.Role, Is.EqualTo("ApplicationLayer"));
        Assert.That(result.Source, Is.EqualTo(ArchitectureClassificationSource.AssemblyAttribute));
    }

    [Test]
    public void Extract_TypeAttributeAndAssemblyAttributeBothPresent_TypeAttributeWins()
    {
        var configuration = new ArchitectureClassificationConfiguration
        {
            Attributes = { DomainMapping("DomainLayer") },
            AssemblyAttributes =
            {
                new ArchitectureAttributeClassificationMapping { Attribute = BoundedContextMarkerAttributeName, Role = "ApplicationLayer" }
            }
        };

        ArchitectureTypeClassificationResult result = CreateExtractor(configuration).Extract(typeof(TypeOverridingAssemblyAttribute));

        Assert.That(result.Role, Is.EqualTo("DomainLayer"));
        Assert.That(result.Source, Is.EqualTo(ArchitectureClassificationSource.TypeAttribute));
    }

    [Test]
    public void Extract_EmptyConfiguration_NeverAssignsRole()
    {
        ArchitectureTypeClassificationResult result = CreateExtractor(new ArchitectureClassificationConfiguration()).Extract(typeof(PlainType));

        Assert.That(result.Role, Is.Null);
        Assert.That(result.Conflicts, Is.Empty);
        Assert.That(result.MetadataFailures, Is.Empty);
    }
}
