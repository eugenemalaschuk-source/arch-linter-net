using System.Reflection;
using System.Reflection.Emit;
using ArchLinterNet.Annotations;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Scanning;
using AttributeRoleExtractionTestFixtures;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class CanonicalAnnotationRoleResolutionTests
{
    private const string EntityFqn = "ArchLinterNet.Annotations.EntityAttribute";
    private const string AggregateRootFqn = "ArchLinterNet.Annotations.AggregateRootAttribute";
    private const string UnityRuntimeFqn = "ArchLinterNet.Annotations.UnityRuntimeAttribute";
    private const string FutureRoleFqn = "ArchLinterNet.Annotations.FutureRoleAttribute";

    [Test]
    public void Extract_TypeAnnotationWithoutYamlMapping_UsesCanonicalCatalogAndManifestMetadata()
    {
        Type type = CreateType("CanonicalDomain", typeRole: true, domain: "Sales");
        var extractor = new ArchitectureAttributeRoleExtractor(new ArchitectureClassificationConfiguration(), [type]);

        ArchitectureTypeClassificationResult result = extractor.Extract(type);

        Assert.Multiple(() =>
        {
            Assert.That(result.Role, Is.EqualTo("Entity"));
            Assert.That(result.Source, Is.EqualTo(ArchitectureClassificationSource.TypeAttribute));
            Assert.That(result.Evidence, Is.EqualTo(EntityFqn));
            Assert.That(result.EvidenceSources, Is.EqualTo(new[] { EntityFqn }));
            Assert.That(result.Metadata["domain"], Is.EqualTo("Sales"));
            Assert.That(result.CanonicalAnnotationDiagnostics, Is.Empty);
        });
    }

    [Test]
    public void Extract_AssemblyAnnotationWithoutYamlMapping_UsesCanonicalCatalog()
    {
        Type type = CreateType("CanonicalUnityAssembly", assemblyRole: true);
        var extractor = new ArchitectureAttributeRoleExtractor(new ArchitectureClassificationConfiguration(), [type]);

        ArchitectureTypeClassificationResult result = extractor.Extract(type);

        Assert.Multiple(() =>
        {
            Assert.That(result.Role, Is.EqualTo("UnityRuntime"));
            Assert.That(result.Source, Is.EqualTo(ArchitectureClassificationSource.AssemblyAttribute));
            Assert.That(result.Evidence, Is.EqualTo(UnityRuntimeFqn));
            Assert.That(result.EvidenceSources, Is.EqualTo(new[] { UnityRuntimeFqn }));
        });
    }

    [Test]
    public void RoleIndex_UsesCanonicalCatalogWithoutYamlMappings()
    {
        Type type = CreateType("CanonicalRoleIndex", typeRole: true, domain: "Sales");
        var index = new ArchitectureRoleIndex(
            new ArchitectureClassificationConfiguration(), new ArchitectureTypeIndex([type.Assembly]));

        bool found = index.TryGetRole(type, out ArchitectureTypeClassificationResult result);

        Assert.Multiple(() =>
        {
            Assert.That(found, Is.True);
            Assert.That(result.Role, Is.EqualTo("Entity"));
            Assert.That(result.EvidenceSources, Is.EqualTo(new[] { EntityFqn }));
            Assert.That(index.CanonicalAnnotationDiagnostics, Is.Empty);
        });
    }

    [Test]
    public void RoleIndex_CollectsAndDeduplicatesCanonicalIdentityDiagnostics()
    {
        Type type = CreateType("CanonicalRoleIndexMissingMarker", typeRole: true, includeMarker: false);
        var index = new ArchitectureRoleIndex(
            new ArchitectureClassificationConfiguration(), new ArchitectureTypeIndex([type.Assembly]));

        bool found = index.TryGetRole(type, out _);

        Assert.Multiple(() =>
        {
            Assert.That(found, Is.False);
            ArchitectureCanonicalAnnotationDiagnostic diagnostic = index.CanonicalAnnotationDiagnostics.Single();
            Assert.That(diagnostic.Code, Is.EqualTo("MissingCatalogIdentity"));
            Assert.That(diagnostic.Subject, Does.Contain(type.Name));
            Assert.That(diagnostic.Message, Does.Contain(type.Name).And.Contain(EntityFqn));
            Assert.That(diagnostic.EvidenceSources, Is.EqualTo(new[]
            {
                "ArchLinterNet.Annotations.AnnotationCatalogIdentityAttribute",
                EntityFqn
            }));
        });
    }

    [Test]
    public void Extract_TypeAnnotationOverridesAssemblyAnnotationAtExistingSpecificity()
    {
        Type type = CreateType("CanonicalTypeWins", typeRole: true, assemblyRole: true);
        var extractor = new ArchitectureAttributeRoleExtractor(new ArchitectureClassificationConfiguration(), [type]);

        ArchitectureTypeClassificationResult result = extractor.Extract(type);

        Assert.That(result.Role, Is.EqualTo("Entity"));
        Assert.That(result.Source, Is.EqualTo(ArchitectureClassificationSource.TypeAttribute));
    }

    [Test]
    public void Extract_ContradictoryCanonicalEntityAndAggregateRootAnnotations_FailsClosedWithOrderedEvidence()
    {
        Type type = CreateType("CanonicalEntityAndAggregateRoot", typeRole: true, aggregateRootRole: true);
        ArchitectureTypeClassificationResult result = new ArchitectureAttributeRoleExtractor(
            new ArchitectureClassificationConfiguration(), [type]).Extract(type);

        Assert.Multiple(() =>
        {
            Assert.That(result.Role, Is.Null);
            ArchitectureCanonicalAnnotationDiagnostic conflict = result.CanonicalAnnotationDiagnostics
                .Single(diagnostic => diagnostic.Code == "CanonicalRoleConflict");
            Assert.That(conflict.EvidenceSources, Is.EqualTo(new[] { AggregateRootFqn, EntityFqn }));
            Assert.That(conflict.Message, Does.Contain(AggregateRootFqn).And.Contain(EntityFqn));
        });
    }

    [Test]
    public void Extract_EmptyCanonicalMetadataValue_PreservesRoleAndReportsFailureThroughBothChannels()
    {
        Type type = CreateType("CanonicalEntityEmptyDomain", typeRole: true, domain: string.Empty);
        ArchitectureClassificationConfiguration configuration = new();
        ArchitectureTypeClassificationResult result = new ArchitectureAttributeRoleExtractor(
            configuration, [type]).Extract(type);
        ArchitectureRoleIndex roleIndex = new(configuration, new ArchitectureTypeIndex([type.Assembly]));

        Assert.Multiple(() =>
        {
            Assert.That(result.Role, Is.EqualTo("Entity"));
            Assert.That(result.Metadata, Does.Not.ContainKey("domain"));
            Assert.That(result.CanonicalAnnotationDiagnostics.Select(diagnostic => diagnostic.Code),
                Does.Contain("InvalidCanonicalMetadata"));
            Assert.That(result.CanonicalAnnotationDiagnostics.Single().Message, Does.Contain("non-empty"));
            ArchitectureClassificationMetadataFailure metadataFailure = result.MetadataFailures.Single();
            Assert.That(metadataFailure.Subject, Is.EqualTo(type.FullName));
            Assert.That(metadataFailure.Source, Is.EqualTo(ArchitectureClassificationSource.TypeAttribute));
            Assert.That(metadataFailure.MetadataKey, Is.EqualTo("domain"));
            Assert.That(metadataFailure.Reason, Does.Contain("non-empty"));
            Assert.That(roleIndex.MetadataFailures, Does.Contain(metadataFailure));
        });
    }

    [Test]
    public void Extract_KnownContextMetadataIdentity_DoesNotCreateRoleOrUnknownReservedDiagnostic()
    {
        Type type = CreateType("ContextMetadataOnly", contextMetadataOnly: true);
        ArchitectureTypeClassificationResult result = new ArchitectureAttributeRoleExtractor(
            new ArchitectureClassificationConfiguration(), [type]).Extract(type);

        Assert.Multiple(() =>
        {
            Assert.That(result.Role, Is.Null);
            Assert.That(result.CanonicalAnnotationDiagnostics, Is.Empty);
        });
    }

    [Test]
    public void Extract_EquivalentCustomAndCanonicalEvidence_CoalescesAndRetainsBothExactFqns()
    {
        Type type = CreateType("CanonicalAndCustom", typeRole: true, customRole: true, domain: "Sales");
        var configuration = new ArchitectureClassificationConfiguration
        {
            Attributes =
            {
                new ArchitectureAttributeClassificationMapping
                {
                    Attribute = "AttributeRoleExtractionTestFixtures.DomainMarkerAttribute",
                    Role = "Entity",
                    Metadata = new Dictionary<string, object> { ["domain"] = "constructor[0]" }
                }
            }
        };
        var extractor = new ArchitectureAttributeRoleExtractor(configuration, [type]);

        ArchitectureTypeClassificationResult result = extractor.Extract(type);

        Assert.Multiple(() =>
        {
            Assert.That(result.Role, Is.EqualTo("Entity"));
            Assert.That(result.Metadata["domain"], Is.EqualTo("Sales"));
            Assert.That(result.EvidenceSources, Is.EquivalentTo(new[]
            {
                EntityFqn,
                "AttributeRoleExtractionTestFixtures.DomainMarkerAttribute"
            }));
        });
    }

    [Test]
    public void Extract_EquivalentCustomEvidence_RetainsEveryMappedAttributeFqn()
    {
        Type type = CreateType("TwoCustomEntityAttributes", customRole: true, secondCustomRole: true);
        var configuration = new ArchitectureClassificationConfiguration
        {
            Attributes =
            {
                new ArchitectureAttributeClassificationMapping
                {
                    Attribute = "AttributeRoleExtractionTestFixtures.DomainMarkerAttribute",
                    Role = "Entity"
                },
                new ArchitectureAttributeClassificationMapping
                {
                    Attribute = "AttributeRoleExtractionTestFixtures.SecondMarkerAttribute",
                    Role = "Entity"
                }
            }
        };
        var extractor = new ArchitectureAttributeRoleExtractor(configuration, [type]);

        ArchitectureTypeClassificationResult result = extractor.Extract(type);

        Assert.Multiple(() =>
        {
            Assert.That(result.Role, Is.EqualTo("Entity"));
            Assert.That(result.EvidenceSources, Is.EqualTo(new[]
            {
                "AttributeRoleExtractionTestFixtures.DomainMarkerAttribute",
                "AttributeRoleExtractionTestFixtures.SecondMarkerAttribute"
            }));
        });
    }

    [Test]
    public void Extract_ContradictoryCustomAndCanonicalEvidence_FailsClosedAtThatSpecificity()
    {
        Type type = CreateType("CanonicalAndConflictingCustom", typeRole: true, customRole: true);
        var configuration = new ArchitectureClassificationConfiguration
        {
            Attributes =
            {
                new ArchitectureAttributeClassificationMapping
                {
                    Attribute = "AttributeRoleExtractionTestFixtures.DomainMarkerAttribute",
                    Role = "InfrastructureLayer"
                }
            }
        };
        var extractor = new ArchitectureAttributeRoleExtractor(configuration, [type]);

        ArchitectureTypeClassificationResult result = extractor.Extract(type);

        Assert.Multiple(() =>
        {
            Assert.That(result.Role, Is.Null);
            Assert.That(result.CanonicalAnnotationDiagnostics.Select(diagnostic => diagnostic.Code),
                Does.Contain("CanonicalRoleConflict"));
            Assert.That(result.CanonicalAnnotationDiagnostics.Single(diagnostic => diagnostic.Code == "CanonicalRoleConflict")
                .EvidenceSources, Is.EquivalentTo(new[]
                {
                    EntityFqn,
                    "AttributeRoleExtractionTestFixtures.DomainMarkerAttribute"
                }));
        });
    }

    [Test]
    public void Extract_CanonicalConflictDiagnostic_IsIndependentOfYamlMappingOrder()
    {
        Type type = CreateType("CanonicalAndTwoCustomRoles", typeRole: true, customRole: true, secondCustomRole: true);
        ArchitectureAttributeClassificationMapping first = new()
        {
            Attribute = "AttributeRoleExtractionTestFixtures.DomainMarkerAttribute",
            Role = "InfrastructureLayer"
        };
        ArchitectureAttributeClassificationMapping second = new()
        {
            Attribute = "AttributeRoleExtractionTestFixtures.SecondMarkerAttribute",
            Role = "ApiLayer"
        };
        ArchitectureCanonicalAnnotationDiagnostic forward = ExtractConflict([first, second], type);
        ArchitectureCanonicalAnnotationDiagnostic reverse = ExtractConflict([second, first], type);

        Assert.Multiple(() =>
        {
            Assert.That(forward.Code, Is.EqualTo("CanonicalRoleConflict"));
            Assert.That(reverse.Code, Is.EqualTo(forward.Code));
            Assert.That(reverse.Message, Is.EqualTo(forward.Message));
            Assert.That(reverse.EvidenceSources, Is.EqualTo(forward.EvidenceSources));
        });
    }

    [TestCase(false, 1, "0.10.0", "MissingCatalogIdentity")]
    [TestCase(true, 2, "0.10.0", "UnsupportedCatalogGeneration")]
    [TestCase(true, 1, "0.9.0", "UnsupportedAnnotationPackageVersion")]
    public void Extract_InvalidCatalogIdentitySuppressesCanonicalRole(
        bool includeMarker, int generation, string version, string expectedCode)
    {
        Type type = CreateType("InvalidCanonicalIdentity", typeRole: true, includeMarker: includeMarker,
            generation: generation, version: version);
        var extractor = new ArchitectureAttributeRoleExtractor(new ArchitectureClassificationConfiguration(), [type]);

        ArchitectureTypeClassificationResult result = extractor.Extract(type);

        Assert.That(result.Role, Is.Null);
        Assert.That(result.CanonicalAnnotationDiagnostics.Select(diagnostic => diagnostic.Code), Does.Contain(expectedCode));
    }

    [Test]
    public void Extract_UnknownReservedNamespaceAttribute_IsDiagnosed()
    {
        Type type = CreateType("UnknownReservedAttribute", unknownRole: true);
        var extractor = new ArchitectureAttributeRoleExtractor(new ArchitectureClassificationConfiguration(), [type]);

        ArchitectureTypeClassificationResult result = extractor.Extract(type);

        Assert.That(result.CanonicalAnnotationDiagnostics.Select(diagnostic => diagnostic.Code),
            Does.Contain("UnknownReservedAnnotation"));
    }

    [Test]
    public void Extract_SameSimpleNameOutsideReservedNamespace_UsesOnlyTheExplicitCustomMapping()
    {
        Type type = CreateType("SameSimpleNameCustomAttribute", sameSimpleNameRole: true);
        var configuration = new ArchitectureClassificationConfiguration
        {
            Attributes =
            {
                new ArchitectureAttributeClassificationMapping
                {
                    Attribute = "Contoso.EntityAttribute",
                    Role = "InfrastructureLayer"
                }
            }
        };
        var extractor = new ArchitectureAttributeRoleExtractor(configuration, [type]);

        ArchitectureTypeClassificationResult result = extractor.Extract(type);

        Assert.Multiple(() =>
        {
            Assert.That(result.Role, Is.EqualTo("InfrastructureLayer"));
            Assert.That(result.Evidence, Is.EqualTo("Contoso.EntityAttribute"));
            Assert.That(result.CanonicalAnnotationDiagnostics, Is.Empty);
        });
    }

    [Test]
    public void Extract_DuplicateCatalogMarkers_AreDiagnosedAndSuppressCanonicalRole()
    {
        Type type = CreateType("DuplicateCatalogMarkers", typeRole: true, duplicateMarker: true);
        ArchitectureTypeClassificationResult result = new ArchitectureAttributeRoleExtractor(
            new ArchitectureClassificationConfiguration(), [type]).Extract(type);

        Assert.That(result.Role, Is.Null);
        Assert.That(result.CanonicalAnnotationDiagnostics.Select(diagnostic => diagnostic.Code),
            Does.Contain("MultipleCatalogIdentityMarkers"));
    }

    [Test]
    public void Extract_MalformedCatalogMarker_IsDiagnosedAndSuppressesCanonicalRole()
    {
        Type type = CreateType("MalformedCatalogMarker", typeRole: true, malformedMarker: true);
        ArchitectureTypeClassificationResult result = new ArchitectureAttributeRoleExtractor(
            new ArchitectureClassificationConfiguration(), [type]).Extract(type);

        Assert.That(result.Role, Is.Null);
        Assert.That(result.CanonicalAnnotationDiagnostics.Select(diagnostic => diagnostic.Code),
            Does.Contain("InvalidCatalogIdentity"));
    }

    [Test]
    public void Extract_IdentityMarkerWithoutCanonicalRole_DoesNotCreateCompatibilityDiagnostic()
    {
        Type type = CreateType("IdentityOnly", includeMarker: true);
        var extractor = new ArchitectureAttributeRoleExtractor(new ArchitectureClassificationConfiguration(), [type]);

        ArchitectureTypeClassificationResult result = extractor.Extract(type);

        Assert.That(result.Role, Is.Null);
        Assert.That(result.CanonicalAnnotationDiagnostics, Is.Empty);
    }

    private static Type CreateType(
        string name,
        bool typeRole = false,
        bool assemblyRole = false,
        bool aggregateRootRole = false,
        bool customRole = false,
        bool secondCustomRole = false,
        bool contextMetadataOnly = false,
        bool sameSimpleNameRole = false,
        bool unknownRole = false,
        string? domain = null,
        bool includeMarker = true,
        bool duplicateMarker = false,
        bool malformedMarker = false,
        int generation = 1,
        string version = "0.10.0")
    {
        AssemblyBuilder assembly = AssemblyBuilder.DefineDynamicAssembly(
            new AssemblyName($"CanonicalAnnotationFixture.{name}.{Guid.NewGuid():N}"), AssemblyBuilderAccess.Run);
        ModuleBuilder module = assembly.DefineDynamicModule("fixture");

        if (includeMarker)
        {
            ConstructorInfo constructor = malformedMarker
                ? typeof(AnnotationCatalogIdentityAttribute).GetConstructor([typeof(string)])!
                : typeof(AnnotationCatalogIdentityAttribute).GetConstructor([typeof(int), typeof(string)])!;
            object[] constructorArguments = malformedMarker ? [version] : [generation, version];
            assembly.SetCustomAttribute(new CustomAttributeBuilder(constructor, constructorArguments));
            if (duplicateMarker)
            {
                assembly.SetCustomAttribute(new CustomAttributeBuilder(
                    typeof(AnnotationCatalogIdentityAttribute).GetConstructor([typeof(int), typeof(string)])!,
                    [generation, version]));
            }
        }

        if (assemblyRole)
        {
            ConstructorInfo constructor = typeof(UnityRuntimeAttribute).GetConstructor(Type.EmptyTypes)!;
            assembly.SetCustomAttribute(new CustomAttributeBuilder(constructor, Array.Empty<object>()));
        }

        TypeBuilder typeBuilder = module.DefineType(name, TypeAttributes.Public | TypeAttributes.Class);
        if (typeRole)
        {
            ConstructorInfo constructor = typeof(EntityAttribute).GetConstructor(Type.EmptyTypes)!;
            if (domain is null)
            {
                typeBuilder.SetCustomAttribute(new CustomAttributeBuilder(constructor, Array.Empty<object>()));
            }
            else
            {
                PropertyInfo domainProperty = typeof(EntityAttribute).GetProperty(nameof(EntityAttribute.Domain))!;
                typeBuilder.SetCustomAttribute(new CustomAttributeBuilder(
                    constructor,
                    Array.Empty<object>(),
                    [domainProperty],
                    [domain]));
            }
        }

        if (aggregateRootRole)
        {
            ConstructorInfo constructor = typeof(AggregateRootAttribute).GetConstructor(Type.EmptyTypes)!;
            typeBuilder.SetCustomAttribute(new CustomAttributeBuilder(constructor, Array.Empty<object>()));
        }

        if (contextMetadataOnly)
        {
            ConstructorInfo constructor = typeof(DomainAttribute).GetConstructor(Type.EmptyTypes)!;
            typeBuilder.SetCustomAttribute(new CustomAttributeBuilder(constructor, Array.Empty<object>()));
        }

        if (customRole)
        {
            ConstructorInfo constructor = typeof(DomainMarkerAttribute).GetConstructor([typeof(string), typeof(string)])!;
            typeBuilder.SetCustomAttribute(new CustomAttributeBuilder(constructor, [domain ?? "Sales", "UnknownModule"]));
        }

        if (secondCustomRole)
        {
            ConstructorInfo constructor = typeof(SecondMarkerAttribute).GetConstructor(Type.EmptyTypes)!;
            typeBuilder.SetCustomAttribute(new CustomAttributeBuilder(constructor, Array.Empty<object>()));
        }

        if (sameSimpleNameRole)
        {
            ConstructorInfo constructor = typeof(Contoso.EntityAttribute).GetConstructor(Type.EmptyTypes)!;
            typeBuilder.SetCustomAttribute(new CustomAttributeBuilder(constructor, Array.Empty<object>()));
        }

        if (unknownRole)
        {
            ConstructorInfo constructor = typeof(FutureRoleAttribute).GetConstructor(Type.EmptyTypes)!;
            typeBuilder.SetCustomAttribute(new CustomAttributeBuilder(constructor, Array.Empty<object>()));
        }

        return typeBuilder.CreateType()!;
    }

    private static ArchitectureCanonicalAnnotationDiagnostic ExtractConflict(
        IReadOnlyList<ArchitectureAttributeClassificationMapping> mappings,
        Type type)
    {
        var extractor = new ArchitectureAttributeRoleExtractor(
            new ArchitectureClassificationConfiguration { Attributes = mappings.ToList() }, [type]);
        ArchitectureTypeClassificationResult result = extractor.Extract(type);
        Assert.That(result.Role, Is.Null);
        return result.CanonicalAnnotationDiagnostics.Single(diagnostic => diagnostic.Code == "CanonicalRoleConflict");
    }
}
