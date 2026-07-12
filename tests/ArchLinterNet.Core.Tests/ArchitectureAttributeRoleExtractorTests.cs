using System.Reflection;
using System.Reflection.Emit;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Model;
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
    public void PublicConstructor_ExactTwoParameterOverload_ExistsForBinaryCompatibility()
    {
        // Regression (#307 review): a consumer already compiled against the original public
        // .ctor(ArchitectureClassificationConfiguration, IEnumerable<Type>) resolves that exact
        // constructor by parameter count/types at load time — an optional third parameter on a
        // single constructor only helps source compatibility, not binary compatibility, so the
        // original two-parameter public overload must keep existing verbatim, not just be callable
        // by omitting a trailing optional argument.
        ConstructorInfo? ctor = typeof(ArchitectureAttributeRoleExtractor).GetConstructor(
            BindingFlags.Public | BindingFlags.Instance,
            new[] { typeof(ArchitectureClassificationConfiguration), typeof(IEnumerable<Type>) });

        Assert.That(ctor, Is.Not.Null);
    }

    [Test]
    public void PublicConstructor_ClassificationNamespaceDeclared_ThrowsInsteadOfSilentlyNeverMatching()
    {
        // Regression (#307 review): the public two-argument constructor has no namespace-glob
        // matcher available (only ArchitectureRoleIndex/Execution can supply one). Declaring
        // classification.namespace entries through this constructor must fail loudly at
        // construction time rather than silently never matching any of them.
        var configuration = new ArchitectureClassificationConfiguration
        {
            Namespace =
            {
                new ArchitectureNamespaceClassificationMapping
                {
                    Namespace = "AttributeRoleExtractionTestFixtures",
                    Role = "DomainLayer"
                }
            }
        };

        Assert.That(
            () => new ArchitectureAttributeRoleExtractor(configuration, TypeUniverse),
            Throws.InvalidOperationException);
    }

    [Test]
    public void PublicConstructor_ClassificationNamespaceDisabledByPrecedence_DoesNotThrow()
    {
        var configuration = new ArchitectureClassificationConfiguration
        {
            Precedence = new List<string> { "type_attribute" },
            Namespace =
            {
                new ArchitectureNamespaceClassificationMapping
                {
                    Namespace = "AttributeRoleExtractionTestFixtures",
                    Role = "DomainLayer"
                }
            }
        };

        Assert.That(
            () => new ArchitectureAttributeRoleExtractor(configuration, TypeUniverse),
            Throws.Nothing);
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
    public void Extract_UnsignedSixtyFourBitEnumProperty_DoesNotOverflowAndCanonicalizesToDeclaredMemberName()
    {
        var configuration = new ArchitectureClassificationConfiguration
        {
            Attributes =
            {
                DomainMapping(metadata: new Dictionary<string, object> { ["ulongValue"] = "property:UlongValue" })
            }
        };

        ArchitectureTypeClassificationResult result = CreateExtractor(configuration).Extract(typeof(TypeWithUlongMaxEnumProperty));

        Assert.That(result.Role, Is.EqualTo("DomainLayer"));
        Assert.That(result.Metadata["ulongValue"], Is.EqualTo("Max"));
        Assert.That(result.MetadataFailures, Is.Empty);
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
    public void Extract_InvalidConstructorIndexExpression_IsEvidenceExtractionFailure()
    {
        var configuration = new ArchitectureClassificationConfiguration
        {
            Attributes =
            {
                DomainMapping(metadata: new Dictionary<string, object> { ["domain"] = "constructor[x]" })
            }
        };

        ArchitectureTypeClassificationResult result = CreateExtractor(configuration).Extract(typeof(TypeWithConstructorDefault));

        Assert.That(result.Role, Is.EqualTo("DomainLayer"));
        Assert.That(result.Metadata.ContainsKey("domain"), Is.False);
        Assert.That(result.MetadataFailures, Has.Count.EqualTo(1));
    }

    [Test]
    public void Extract_ConstructorExpressionMissingClosingBracket_IsEvidenceExtractionFailure()
    {
        var configuration = new ArchitectureClassificationConfiguration
        {
            Attributes =
            {
                DomainMapping(metadata: new Dictionary<string, object> { ["domain"] = "constructor[0" })
            }
        };

        ArchitectureTypeClassificationResult result = CreateExtractor(configuration).Extract(typeof(TypeWithConstructorDefault));

        Assert.That(result.Role, Is.EqualTo("DomainLayer"));
        Assert.That(result.Metadata.ContainsKey("domain"), Is.False);
        Assert.That(result.Metadata.Values, Does.Not.Contain("constructor[0"));
        Assert.That(result.MetadataFailures, Has.Count.EqualTo(1));
    }

    [Test]
    public void Extract_ConstReferenceWithoutFieldSeparator_IsEvidenceExtractionFailure()
    {
        var configuration = new ArchitectureClassificationConfiguration
        {
            Attributes =
            {
                DomainMapping(metadata: new Dictionary<string, object> { ["owner"] = "const:NoDotHere" })
            }
        };

        ArchitectureTypeClassificationResult result = CreateExtractor(configuration).Extract(typeof(TypeWithConstructorDefault));

        Assert.That(result.Metadata.ContainsKey("owner"), Is.False);
        Assert.That(result.MetadataFailures, Has.Count.EqualTo(1));
    }

    [Test]
    public void Extract_ConstReferenceToNonexistentField_IsEvidenceExtractionFailure()
    {
        var configuration = new ArchitectureClassificationConfiguration
        {
            Attributes =
            {
                DomainMapping(metadata: new Dictionary<string, object>
                {
                    ["owner"] = "const:AttributeRoleExtractionTestFixtures.Constants.DoesNotExist"
                })
            }
        };

        ArchitectureTypeClassificationResult result = CreateExtractor(configuration).Extract(typeof(TypeWithConstructorDefault));

        Assert.That(result.Metadata.ContainsKey("owner"), Is.False);
        Assert.That(result.MetadataFailures, Has.Count.EqualTo(1));
    }

    [Test]
    public void Extract_ConstReferenceToNullValue_IsEvidenceExtractionFailure()
    {
        var configuration = new ArchitectureClassificationConfiguration
        {
            Attributes =
            {
                DomainMapping(metadata: new Dictionary<string, object>
                {
                    ["owner"] = "const:AttributeRoleExtractionTestFixtures.Constants.NullOwner"
                })
            }
        };

        ArchitectureTypeClassificationResult result = CreateExtractor(configuration).Extract(typeof(TypeWithConstructorDefault));

        Assert.That(result.Role, Is.EqualTo("DomainLayer"));
        Assert.That(result.Metadata.ContainsKey("owner"), Is.False);
        Assert.That(result.MetadataFailures, Has.Count.EqualTo(1));
    }

    [Test]
    public void Extract_TypeConstructorArgument_CanonicalizesToFullTypeName()
    {
        var configuration = new ArchitectureClassificationConfiguration
        {
            Attributes =
            {
                DomainMapping(metadata: new Dictionary<string, object> { ["moduleType"] = "constructor[0]" })
            }
        };

        ArchitectureTypeClassificationResult result = CreateExtractor(configuration).Extract(typeof(TypeWithTypeConstructorArgument));

        Assert.That(result.Metadata["moduleType"], Is.EqualTo("AttributeRoleExtractionTestFixtures.PlainType"));
    }

    [Test]
    public void Extract_NonFiniteFloatProperty_IsEvidenceExtractionFailure()
    {
        var configuration = new ArchitectureClassificationConfiguration
        {
            Attributes =
            {
                DomainMapping(metadata: new Dictionary<string, object> { ["value"] = "property:FloatValue" })
            }
        };

        ArchitectureTypeClassificationResult result = CreateExtractor(configuration).Extract(typeof(TypeWithNonFiniteFloatProperty));

        Assert.That(result.Role, Is.EqualTo("DomainLayer"));
        Assert.That(result.Metadata.ContainsKey("value"), Is.False);
        Assert.That(result.MetadataFailures, Has.Count.EqualTo(1));
    }

    [Test]
    public void Extract_FloatPropertyExceedingDecimalRange_IsEvidenceExtractionFailure()
    {
        var configuration = new ArchitectureClassificationConfiguration
        {
            Attributes =
            {
                DomainMapping(metadata: new Dictionary<string, object> { ["value"] = "property:FloatValue" })
            }
        };

        ArchitectureTypeClassificationResult result = CreateExtractor(configuration).Extract(typeof(TypeWithOverflowingFloatProperty));

        Assert.That(result.Role, Is.EqualTo("DomainLayer"));
        Assert.That(result.Metadata.ContainsKey("value"), Is.False);
        Assert.That(result.MetadataFailures, Has.Count.EqualTo(1));
    }

    [Test]
    public void Extract_NonFiniteDoubleProperty_IsEvidenceExtractionFailure()
    {
        var configuration = new ArchitectureClassificationConfiguration
        {
            Attributes =
            {
                DomainMapping(metadata: new Dictionary<string, object> { ["value"] = "property:DoubleValue" })
            }
        };

        ArchitectureTypeClassificationResult result = CreateExtractor(configuration).Extract(typeof(TypeWithNonFiniteDoubleProperty));

        Assert.That(result.Role, Is.EqualTo("DomainLayer"));
        Assert.That(result.Metadata.ContainsKey("value"), Is.False);
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
        Assert.That(result.Conflicts[0].WinningRole, Is.EqualTo("DomainLayer"));
        Assert.That(result.Conflicts[0].DiscardedRole, Is.EqualTo("DomainLayer"));
        Assert.That(result.Conflicts[0].MetadataDetail, Does.Contain("domain: 'Sales' vs 'Marketing'"));
    }

    [Test]
    public void Extract_MultipleDistinctMetadataOnlyConflictsOnOneSubject_AllRecordedWithDistinctDetail()
    {
        var configuration = new ArchitectureClassificationConfiguration
        {
            Attributes =
            {
                DomainMapping(metadata: new Dictionary<string, object> { ["domain"] = "constructor[0]" })
            }
        };

        ArchitectureTypeClassificationResult result = CreateExtractor(configuration).Extract(typeof(TypeWithThreeDifferingRepeatedInstances));

        Assert.That(result.Conflicts, Has.Count.EqualTo(2));
        List<string?> details = result.Conflicts.Select(c => c.MetadataDetail).ToList();
        Assert.That(details, Does.Contain("domain: 'Sales' vs 'Marketing'"));
        Assert.That(details, Does.Contain("domain: 'Sales' vs 'Engineering'"));

        // Distinct MetadataDetail keeps otherwise-identical conflict facts (same subject/source/role)
        // from collapsing when a downstream consumer deduplicates via a HashSet, e.g.
        // ArchitectureAnalysisSession.CheckClassificationFacts.
        var deduplicated = new HashSet<ArchitectureClassificationConflict>(result.Conflicts);
        Assert.That(deduplicated, Has.Count.EqualTo(2));
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

    [Test]
    public void Extract_PrecedenceExcludingTypeAttribute_AssemblyAttributeWins()
    {
        var configuration = new ArchitectureClassificationConfiguration
        {
            Precedence = new List<string> { "assembly_attribute" },
            Attributes = { DomainMapping("DomainLayer") },
            AssemblyAttributes =
            {
                new ArchitectureAttributeClassificationMapping { Attribute = BoundedContextMarkerAttributeName, Role = "ApplicationLayer" }
            }
        };

        ArchitectureTypeClassificationResult result = CreateExtractor(configuration).Extract(typeof(TypeOverridingAssemblyAttribute));

        Assert.That(result.Role, Is.EqualTo("ApplicationLayer"));
        Assert.That(result.Source, Is.EqualTo(ArchitectureClassificationSource.AssemblyAttribute));
    }

    [Test]
    public void Extract_PrecedenceExcludingBothSources_NeverAssignsRole()
    {
        var configuration = new ArchitectureClassificationConfiguration
        {
            Precedence = new List<string> { "namespace" },
            Attributes = { DomainMapping("DomainLayer") },
            AssemblyAttributes =
            {
                new ArchitectureAttributeClassificationMapping { Attribute = BoundedContextMarkerAttributeName, Role = "ApplicationLayer" }
            }
        };

        ArchitectureTypeClassificationResult result = CreateExtractor(configuration).Extract(typeof(TypeOverridingAssemblyAttribute));

        Assert.That(result.Role, Is.Null);
    }

    [Test]
    public void Extract_AmbiguousConstTypeAcrossAssemblies_IsEvidenceExtractionFailure()
    {
        Type duplicateType = DefineDuplicateFixtureConstantsType();
        var typeUniverse = TypeUniverse.Append(duplicateType).ToArray();

        var configuration = new ArchitectureClassificationConfiguration
        {
            Attributes =
            {
                DomainMapping(metadata: new Dictionary<string, object>
                {
                    ["owner"] = "const:AttributeRoleExtractionTestFixtures.Constants.Owner"
                })
            }
        };

        var extractor = new ArchitectureAttributeRoleExtractor(configuration, typeUniverse);
        ArchitectureTypeClassificationResult result = extractor.Extract(typeof(TypeWithConstructorDefault));

        Assert.That(result.Role, Is.EqualTo("DomainLayer"));
        Assert.That(result.Metadata.ContainsKey("owner"), Is.False);
        Assert.That(result.MetadataFailures, Has.Count.EqualTo(1));
    }

    // Builds a second, distinct Type whose FullName collides with the real
    // AttributeRoleExtractionTestFixtures.Constants fixture, simulating the same full type name
    // reachable from two different assemblies in the extractor's type universe.
    private static Type DefineDuplicateFixtureConstantsType()
    {
        AssemblyBuilder assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(
            new AssemblyName("ArchitectureAttributeRoleExtractorTests.DuplicateFixtures"), AssemblyBuilderAccess.Run);
        ModuleBuilder moduleBuilder = assemblyBuilder.DefineDynamicModule("DuplicateFixturesModule");
        TypeBuilder typeBuilder = moduleBuilder.DefineType(
            "AttributeRoleExtractionTestFixtures.Constants", TypeAttributes.Public | TypeAttributes.Class);
        FieldBuilder fieldBuilder = typeBuilder.DefineField(
            "Owner", typeof(string), FieldAttributes.Public | FieldAttributes.Static | FieldAttributes.Literal);
        fieldBuilder.SetConstant("duplicate-team");
        return typeBuilder.CreateType();
    }
}
