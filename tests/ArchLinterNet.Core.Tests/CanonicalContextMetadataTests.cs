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
public sealed class CanonicalContextMetadataTests
{
    private const string DomainFqn = "ArchLinterNet.Annotations.DomainAttribute";
    private const string BoundedContextFqn = "ArchLinterNet.Annotations.BoundedContextAttribute";

    [Test]
    public void Extract_ComposesRoleThenTypeContextThenAssemblyDefaults()
    {
        (AssemblyBuilder assembly, ModuleBuilder module) = CreateAssembly("RoleAndContextComposition");
        AddAssemblyRole(assembly, typeof(DomainLayerAttribute), ("Domain", "LosingAssemblyRole"));
        AddAssemblyContext(assembly, typeof(DomainAttribute), "AssemblyDomain");
        AddAssemblyContext(assembly, typeof(BoundedContextAttribute), "AssemblyOrders");
        AddAssemblyContext(assembly, typeof(ModuleAttribute), "AssemblyModule");
        AddAssemblyContext(assembly, typeof(ArchLinterNet.Annotations.PlatformAttribute), "Server");

        Type type = DefineType(
            module,
            "CanonicalContextFixtures.Order",
            roleAttribute: typeof(EntityAttribute),
            roleMetadata: [("Domain", "RoleDomain")],
            contextAttributes:
            [
                (typeof(BoundedContextAttribute), "Orders"),
                (typeof(ModuleAttribute), "TypeModule")
            ]);

        ArchitectureTypeClassificationResult result = Extract(type);

        Assert.Multiple(() =>
        {
            Assert.That(result.Role, Is.EqualTo("Entity"));
            Assert.That(result.Metadata, Is.EquivalentTo(new Dictionary<string, object>
            {
                ["domain"] = "RoleDomain",
                ["boundedContext"] = "Orders",
                ["module"] = "TypeModule",
                ["platform"] = "Server"
            }));
            Assert.That(result.CanonicalAnnotationDiagnostics, Is.Empty);
        });
    }

    [Test]
    public void Extract_CustomMappedRoleMetadataRemainsAuthoritativeOverContext()
    {
        (AssemblyBuilder assembly, ModuleBuilder module) = CreateAssembly("CustomMappedContextComposition");
        AddAssemblyContext(assembly, typeof(DomainAttribute), "AssemblyDomain");
        Type type = DefineType(
            module,
            "CanonicalContextFixtures.CustomMappedOrder",
            roleAttribute: typeof(DomainMarkerAttribute),
            roleConstructorArguments: ["Sales", "Orders"],
            contextAttributes: [(typeof(DomainAttribute), "TypeDomain")]);

        ArchitectureClassificationConfiguration configuration = new();
        configuration.Attributes.Add(new ArchitectureAttributeClassificationMapping
        {
            Attribute = typeof(DomainMarkerAttribute).FullName!,
            Role = "Entity",
            Metadata = new Dictionary<string, object> { ["domain"] = "constructor[0]" }
        });

        ArchitectureTypeClassificationResult result = new ArchitectureAttributeRoleExtractor(
            configuration, [type]).Extract(type);

        Assert.Multiple(() =>
        {
            Assert.That(result.Role, Is.EqualTo("Entity"));
            Assert.That(result.Metadata["domain"], Is.EqualTo("Sales"));
            Assert.That(result.Metadata, Does.Not.ContainKey("boundedContext"));
        });
    }

    [Test]
    public void Extract_ContextOnlyEvidenceDoesNotCreateRole()
    {
        (AssemblyBuilder _, ModuleBuilder module) = CreateAssembly("ContextOnly");
        Type type = DefineType(
            module,
            "CanonicalContextFixtures.ContextOnly",
            contextAttributes: [(typeof(FeatureAttribute), "Inventory")]);

        ArchitectureTypeClassificationResult result = Extract(type);

        Assert.Multiple(() =>
        {
            Assert.That(result.Role, Is.Null);
            Assert.That(result.Metadata, Is.Empty);
            Assert.That(result.CanonicalAnnotationDiagnostics, Is.Empty);
        });
    }

    [Test]
    public void Extract_SameScopeContextConflictOmitsOnlyThatKeyAndIsDeterministic()
    {
        (AssemblyBuilder firstAssembly, ModuleBuilder firstModule) = CreateAssembly("ContextConflictForward");
        AddAssemblyContext(firstAssembly, typeof(BoundedContextAttribute), "AssemblyDefault");
        Type firstType = DefineType(
            firstModule,
            "CanonicalContextFixtures.ContextConflict",
            roleAttribute: typeof(EntityAttribute),
            contextAttributes:
            [
                (typeof(BoundedContextAttribute), "Zulu"),
                (typeof(BoundedContextAttribute), "Alpha"),
                (typeof(ModuleAttribute), "Orders")
            ]);
        ArchitectureCanonicalAnnotationDiagnostic firstDiagnostic = Extract(firstType)
            .CanonicalAnnotationDiagnostics.Single(diagnostic => diagnostic.Code == "CanonicalContextConflict");

        (AssemblyBuilder reverseAssembly, ModuleBuilder reverseModule) = CreateAssembly("ContextConflictReverse");
        AddAssemblyContext(reverseAssembly, typeof(BoundedContextAttribute), "AssemblyDefault");
        Type reverseType = DefineType(
            reverseModule,
            "CanonicalContextFixtures.ContextConflict",
            roleAttribute: typeof(EntityAttribute),
            contextAttributes:
            [
                (typeof(BoundedContextAttribute), "Alpha"),
                (typeof(BoundedContextAttribute), "Zulu"),
                (typeof(ModuleAttribute), "Orders")
            ]);
        ArchitectureTypeClassificationResult reverseResult = Extract(reverseType);
        ArchitectureCanonicalAnnotationDiagnostic reverseDiagnostic = reverseResult
            .CanonicalAnnotationDiagnostics.Single(diagnostic => diagnostic.Code == "CanonicalContextConflict");

        Assert.Multiple(() =>
        {
            Assert.That(reverseResult.Role, Is.EqualTo("Entity"));
            Assert.That(reverseResult.Metadata, Does.Not.ContainKey("boundedContext"));
            Assert.That(reverseResult.Metadata["module"], Is.EqualTo("Orders"));
            Assert.That(firstDiagnostic.Message, Is.EqualTo(reverseDiagnostic.Message));
            Assert.That(firstDiagnostic.Message, Does.Contain("'Alpha'").And.Contain("'Zulu'"));
            Assert.That(firstDiagnostic.EvidenceSources, Is.EqualTo(new[] { BoundedContextFqn }));
        });
    }

    [Test]
    public void Extract_EquivalentSameScopeContextValuesCoalesce()
    {
        (AssemblyBuilder _, ModuleBuilder module) = CreateAssembly("ContextEquivalent");
        Type type = DefineType(
            module,
            "CanonicalContextFixtures.ContextEquivalent",
            roleAttribute: typeof(EntityAttribute),
            contextAttributes:
            [
                (typeof(DomainAttribute), "Sales"),
                (typeof(DomainAttribute), "Sales")
            ]);

        ArchitectureTypeClassificationResult result = Extract(type);

        Assert.Multiple(() =>
        {
            Assert.That(result.Role, Is.EqualTo("Entity"));
            Assert.That(result.Metadata["domain"], Is.EqualTo("Sales"));
            Assert.That(result.CanonicalAnnotationDiagnostics, Is.Empty);
        });
    }

    [Test]
    public void Extract_InvalidContextValueRetainsRoleAndReportsFailure()
    {
        (AssemblyBuilder assembly, ModuleBuilder module) = CreateAssembly("InvalidContext");
        AddAssemblyContext(assembly, typeof(DomainAttribute), "AssemblyDomain");
        Type type = DefineType(
            module,
            "CanonicalContextFixtures.InvalidContext",
            roleAttribute: typeof(EntityAttribute),
            contextAttributes: [(typeof(DomainAttribute), string.Empty)]);

        ArchitectureTypeClassificationResult result = Extract(type);

        Assert.Multiple(() =>
        {
            Assert.That(result.Role, Is.EqualTo("Entity"));
            Assert.That(result.Metadata, Does.Not.ContainKey("domain"));
            Assert.That(result.MetadataFailures.Select(failure => failure.MetadataKey), Is.EqualTo(new[] { "domain" }));
            Assert.That(result.CanonicalAnnotationDiagnostics.Select(diagnostic => diagnostic.Code),
                Does.Contain("InvalidCanonicalMetadata"));
        });
    }

    [Test]
    public void Extract_MalformedContextConstructorSignatureBlocksAssemblyFallback()
    {
        (AssemblyBuilder assembly, ModuleBuilder module) = CreateAssembly("MalformedContextConstructor");
        AddAssemblyContext(assembly, typeof(DomainAttribute), "AssemblyDomain");
        Type malformedDomainAttribute = DefineMalformedDomainAttribute(module);
        Type type = DefineType(
            module,
            "CanonicalContextFixtures.MalformedContextConstructor",
            roleAttribute: typeof(EntityAttribute),
            additionalContextAttributes: [(malformedDomainAttribute, [42])]);

        ArchitectureTypeClassificationResult result = Extract(type);

        Assert.Multiple(() =>
        {
            Assert.That(result.Role, Is.EqualTo("Entity"));
            Assert.That(result.Metadata, Does.Not.ContainKey("domain"));
            Assert.That(result.MetadataFailures.Select(failure => failure.MetadataKey), Does.Contain("domain"));
            Assert.That(result.CanonicalAnnotationDiagnostics.Select(diagnostic => diagnostic.Code),
                Does.Contain("InvalidCanonicalMetadata"));
        });
    }

    [Test]
    public void Extract_AssemblyContextConflictOmitsOnlyThatKeyAndReportsConflict()
    {
        (AssemblyBuilder assembly, ModuleBuilder module) = CreateAssembly("AssemblyContextConflict");
        AddAssemblyContext(assembly, typeof(BoundedContextAttribute), "Zulu");
        AddAssemblyContext(assembly, typeof(BoundedContextAttribute), "Alpha");
        AddAssemblyContext(assembly, typeof(ModuleAttribute), "Orders");
        Type type = DefineType(module, "CanonicalContextFixtures.AssemblyContextConflict", typeof(EntityAttribute));

        ArchitectureTypeClassificationResult result = Extract(type);

        Assert.Multiple(() =>
        {
            Assert.That(result.Role, Is.EqualTo("Entity"));
            Assert.That(result.Metadata, Does.Not.ContainKey("boundedContext"));
            Assert.That(result.Metadata["module"], Is.EqualTo("Orders"));
            Assert.That(result.CanonicalAnnotationDiagnostics.Select(diagnostic => diagnostic.Code),
                Does.Contain("CanonicalContextConflict"));
        });
    }

    [Test]
    public void ContextAnnotationConstructors_AcceptValuesForAllSixKeysInConsumerAssembly()
    {
        Assembly consumerAssembly = typeof(
            ArchLinterNet.Annotations.CSharp9Consumer.ContextMetadataConsumerFixture).Assembly;
        string[] annotationNames =
        {
            "DomainAttribute",
            "BoundedContextAttribute",
            "ModuleAttribute",
            "FeatureAttribute",
            "PlatformAttribute",
            "RuntimeAttribute"
        };

        foreach (string annotationName in annotationNames)
        {
            Type attributeType = consumerAssembly.GetType(
                $"ArchLinterNet.Annotations.{annotationName}", throwOnError: true)!;
            ConstructorInfo constructor = attributeType.GetConstructor(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null, [typeof(string)], null)!;

            Assert.That(constructor.Invoke(["coverage"]), Is.Not.Null, annotationName);
        }
    }

    [Test]
    public void Extract_ContextEvidenceWithoutCatalogIdentityIsSuppressed()
    {
        (AssemblyBuilder _, ModuleBuilder module) = CreateAssembly("ContextWithoutIdentity", includeIdentity: false);
        Type type = DefineType(
            module,
            "CanonicalContextFixtures.ContextWithoutIdentity",
            contextAttributes: [(typeof(DomainAttribute), "Sales")]);

        ArchitectureTypeClassificationResult result = Extract(type);

        Assert.Multiple(() =>
        {
            Assert.That(result.Role, Is.Null);
            Assert.That(result.Metadata, Is.Empty);
            Assert.That(result.CanonicalAnnotationDiagnostics.Select(diagnostic => diagnostic.Code),
                Does.Contain("MissingCatalogIdentity"));
        });
    }

    [Test]
    public void Extract_DisabledTypeOrAssemblySourceSuppressesContextEvidence()
    {
        (AssemblyBuilder assembly, ModuleBuilder module) = CreateAssembly("DisabledContextSources");
        AddAssemblyRole(assembly, typeof(UnityRuntimeAttribute));
        AddAssemblyContext(assembly, typeof(DomainAttribute), "AssemblyDomain");
        Type type = DefineType(
            module,
            "CanonicalContextFixtures.DisabledContextSources",
            roleAttribute: typeof(EntityAttribute),
            contextAttributes: [(typeof(DomainAttribute), "TypeDomain")]);

        ArchitectureTypeClassificationResult typeDisabled = new ArchitectureAttributeRoleExtractor(
            new ArchitectureClassificationConfiguration { Precedence = ["assembly_attribute"] }, [type]).Extract(type);
        ArchitectureTypeClassificationResult assemblyDisabled = new ArchitectureAttributeRoleExtractor(
            new ArchitectureClassificationConfiguration { Precedence = ["type_attribute"] }, [type]).Extract(type);

        Assert.Multiple(() =>
        {
            Assert.That(typeDisabled.Role, Is.EqualTo("UnityRuntime"));
            Assert.That(typeDisabled.Metadata, Is.EqualTo(new Dictionary<string, object>
            {
                ["domain"] = "AssemblyDomain"
            }));
            Assert.That(assemblyDisabled.Role, Is.EqualTo("Entity"));
            Assert.That(assemblyDisabled.Metadata, Is.EqualTo(new Dictionary<string, object>
            {
                ["domain"] = "TypeDomain"
            }));
            Assert.That(assemblyDisabled.CanonicalAnnotationDiagnostics, Is.Empty);
        });
    }

    [Test]
    public void RoleIndex_ExposesComposedContextMetadataThroughTheExistingDescriptor()
    {
        (AssemblyBuilder assembly, ModuleBuilder module) = CreateAssembly("RoleIndexContext");
        AddAssemblyContext(assembly, typeof(BoundedContextAttribute), "Orders");
        Type type = DefineType(module, "CanonicalContextFixtures.RoleIndexOrder", typeof(EntityAttribute));

        ArchitectureRoleIndex index = new(
            new ArchitectureClassificationConfiguration(), new ArchitectureTypeIndex([assembly]));

        Assert.That(index.TryGetRole(type, out ArchitectureTypeClassificationResult descriptor), Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(descriptor.Role, Is.EqualTo("Entity"));
            Assert.That(descriptor.Metadata["boundedContext"], Is.EqualTo("Orders"));
        });
    }

    [Test]
    public void RoleIndex_ContextOnlyEvidenceDoesNotCreateADescriptor()
    {
        (AssemblyBuilder assembly, ModuleBuilder module) = CreateAssembly("RoleIndexContextOnly");
        Type type = DefineType(
            module,
            "CanonicalContextFixtures.RoleIndexContextOnly",
            contextAttributes: [(typeof(FeatureAttribute), "Inventory")]);

        ArchitectureRoleIndex index = new(
            new ArchitectureClassificationConfiguration(), new ArchitectureTypeIndex([assembly]));

        Assert.That(index.TryGetRole(type, out _), Is.False);
    }

    private static ArchitectureTypeClassificationResult Extract(Type type) =>
        new ArchitectureAttributeRoleExtractor(new ArchitectureClassificationConfiguration(), [type]).Extract(type);

    private static (AssemblyBuilder Assembly, ModuleBuilder Module) CreateAssembly(
        string name,
        bool includeIdentity = true)
    {
        AssemblyBuilder assembly = AssemblyBuilder.DefineDynamicAssembly(
            new AssemblyName($"CanonicalContextFixture.{name}.{Guid.NewGuid():N}"), AssemblyBuilderAccess.Run);
        ModuleBuilder module = assembly.DefineDynamicModule("fixture");
        if (includeIdentity)
        {
            ConstructorInfo markerConstructor = typeof(AnnotationCatalogIdentityAttribute)
                .GetConstructor([typeof(int), typeof(string)])!;
            assembly.SetCustomAttribute(new CustomAttributeBuilder(markerConstructor, [1, "0.10.0"]));
        }

        return (assembly, module);
    }

    private static Type DefineType(
        ModuleBuilder module,
        string fullName,
        Type? roleAttribute = null,
        object[]? roleConstructorArguments = null,
        IReadOnlyList<(string Property, string Value)>? roleMetadata = null,
        IReadOnlyList<(Type Attribute, string Value)>? contextAttributes = null,
        IReadOnlyList<(Type Attribute, object[] ConstructorArguments)>? additionalContextAttributes = null)
    {
        TypeBuilder typeBuilder = module.DefineType(fullName, TypeAttributes.Public | TypeAttributes.Class);
        typeBuilder.DefineDefaultConstructor(MethodAttributes.Public);

        if (roleAttribute is not null)
        {
            ConstructorInfo constructor = roleAttribute.GetConstructor(
                roleConstructorArguments is null
                    ? Type.EmptyTypes
                    : roleConstructorArguments.Select(value => value.GetType()).ToArray())!;
            PropertyInfo[] properties = (roleMetadata ?? Array.Empty<(string, string)>())
                .Select(entry => roleAttribute.GetProperty(entry.Property)!)
                .ToArray();
            object[] values = (roleMetadata ?? Array.Empty<(string, string)>())
                .Select(entry => (object)entry.Value)
                .ToArray();
            typeBuilder.SetCustomAttribute(new CustomAttributeBuilder(
                constructor,
                roleConstructorArguments ?? Array.Empty<object>(),
                properties,
                values));
        }

        if (contextAttributes is not null)
        {
            foreach ((Type attribute, string value) in contextAttributes)
            {
                ConstructorInfo constructor = attribute.GetConstructor([typeof(string)])!;
                typeBuilder.SetCustomAttribute(new CustomAttributeBuilder(constructor, [value]));
            }
        }

        if (additionalContextAttributes is not null)
        {
            foreach ((Type attribute, object[] constructorArguments) in additionalContextAttributes)
            {
                ConstructorInfo constructor = attribute.GetConstructor(
                    constructorArguments.Select(value => value.GetType()).ToArray())!;
                typeBuilder.SetCustomAttribute(new CustomAttributeBuilder(constructor, constructorArguments));
            }
        }

        return typeBuilder.CreateType()!;
    }

    private static Type DefineMalformedDomainAttribute(ModuleBuilder module)
    {
        TypeBuilder attributeBuilder = module.DefineType(
            "ArchLinterNet.Annotations.DomainAttribute",
            TypeAttributes.Public | TypeAttributes.Sealed,
            typeof(Attribute));
        ConstructorBuilder constructor = attributeBuilder.DefineConstructor(
            MethodAttributes.Public,
            CallingConventions.Standard,
            [typeof(int)]);
        ILGenerator il = constructor.GetILGenerator();
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Call, typeof(Attribute).GetConstructor(
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            null, Type.EmptyTypes, null)!);
        il.Emit(OpCodes.Ret);
        return attributeBuilder.CreateType()!;
    }

    private static void AddAssemblyContext(AssemblyBuilder assembly, Type attributeType, string value)
    {
        ConstructorInfo constructor = attributeType.GetConstructor([typeof(string)])!;
        assembly.SetCustomAttribute(new CustomAttributeBuilder(constructor, [value]));
    }

    private static void AddAssemblyRole(
        AssemblyBuilder assembly,
        Type roleAttributeType,
        params (string Property, string Value)[] metadata)
    {
        ConstructorInfo constructor = roleAttributeType.GetConstructor(Type.EmptyTypes)!;
        PropertyInfo[] properties = metadata.Select(entry => roleAttributeType.GetProperty(entry.Property)!).ToArray();
        object[] values = metadata.Select(entry => (object)entry.Value).ToArray();
        assembly.SetCustomAttribute(new CustomAttributeBuilder(constructor, Array.Empty<object>(), properties, values));
    }
}
