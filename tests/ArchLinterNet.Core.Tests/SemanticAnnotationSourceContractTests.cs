using System.Reflection;
using System.Text.Json;
using ArchLinterNet.Annotations;
using ArchLinterNet.Annotations.CSharp9Consumer;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Scanning;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class SemanticAnnotationSourceContractTests
{
    private static readonly string[] _expectedMetadataProperties =
    [
        "Domain",
        "BoundedContext",
        "Module",
        "Feature",
        "Platform",
        "Runtime"
    ];

    [TestCase("layered-clean", 9)]
    [TestCase("ddd", 8)]
    public void RoleAttributes_MatchManifestIdentityAndTargetContract(string family, int expectedCount)
    {
        using JsonDocument manifest = JsonDocument.Parse(File.ReadAllText(ManifestPath()));
        JsonElement[] roles = manifest.RootElement.GetProperty("roles")
            .EnumerateArray()
            .Where(role => string.Equals(role.GetProperty("family").GetString(), family, StringComparison.Ordinal))
            .ToArray();

        Assert.That(roles, Has.Length.EqualTo(expectedCount));

        JsonElement roleAttributeContract = manifest.RootElement.GetProperty("roleAttributeContract");
        string baseTypeName = roleAttributeContract.GetProperty("baseType").GetString()!;
        string baseTypeRelationship = roleAttributeContract.GetProperty("baseTypeRelationship").GetString()!;
        Type requiredBaseType = Type.GetType(baseTypeName, throwOnError: true)!;

        Assert.That(baseTypeRelationship, Is.EqualTo("assignable-ancestor"));

        Assembly annotationAssembly = typeof(DomainLayerConsumerFixture).Assembly;
        foreach (JsonElement role in roles)
        {
            string roleName = role.GetProperty("role").GetString()!;
            string expectedFullName = role.GetProperty("fqn").GetString()!;
            Type annotationType = annotationAssembly.GetType(expectedFullName, throwOnError: true)!;
            string message = $"{family} role {roleName}";
            ConstructorInfo? parameterlessConstructor = annotationType.GetConstructor(Type.EmptyTypes);
            PropertyInfo[] properties = annotationType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(property => property.DeclaringType != typeof(Attribute))
                .ToArray();

            Assert.Multiple(() =>
            {
                Assert.That(annotationType.IsNotPublic, Is.True, $"{message} must remain internal");
                Assert.That(annotationType.IsSealed, Is.True, $"{message} must be sealed");
                Assert.That(annotationType.IsAbstract, Is.False, $"{message} must be constructible");
                Assert.That(annotationType.IsSubclassOf(requiredBaseType), Is.True,
                    $"{message} must descend from manifest base type {baseTypeName}");
                Assert.That(parameterlessConstructor, Is.Not.Null,
                    $"{message} must have a public parameterless constructor");

                AttributeUsageAttribute? usage = annotationType.GetCustomAttribute<AttributeUsageAttribute>(inherit: false);
                Assert.That(usage, Is.Not.Null, $"{message} must declare its own target contract");
                Assert.That(usage!.ValidOn, Is.EqualTo(ReadTargets(role.GetProperty("attributeTargets"))),
                    $"{message} must match its manifest targets");
                Assert.That(usage.AllowMultiple, Is.False, $"{message} must reject accumulated primary roles");
                Assert.That(usage.Inherited, Is.False, $"{message} must not apply through inheritance");

                Assert.That(properties.Select(property => property.Name), Is.EquivalentTo(_expectedMetadataProperties),
                    $"{message} must expose only the bounded metadata properties");
                foreach (PropertyInfo property in properties)
                {
                    Assert.That(property.PropertyType, Is.EqualTo(typeof(string)), $"{message}.{property.Name} must be a string");
                    Assert.That(property.GetMethod?.IsPublic, Is.True, $"{message}.{property.Name} must be publicly readable");
                    Assert.That(property.SetMethod?.IsPublic, Is.True, $"{message}.{property.Name} must be publicly settable");
                }
            });

            if (parameterlessConstructor is null)
            {
                continue;
            }

            object instance = parameterlessConstructor.Invoke(null);
            foreach (PropertyInfo property in properties)
            {
                string expectedValue = $"coverage-{roleName}-{property.Name}";
                property.SetValue(instance, expectedValue);
                Assert.That(property.GetValue(instance), Is.EqualTo(expectedValue),
                    $"{message}.{property.Name} must retain an assigned metadata value");
            }
        }
    }

    [Test]
    public void ExplicitlyMappedCanonicalSource_EntersExistingRoleIndexWithMetadata()
    {
        ArchitectureClassificationConfiguration configuration = new();
        configuration.Attributes.Add(new ArchitectureAttributeClassificationMapping
        {
            Attribute = typeof(AggregateRootAttribute).FullName!,
            Role = "AggregateRoot",
            Metadata = new Dictionary<string, object>
            {
                ["boundedContext"] = "property:BoundedContext",
                ["module"] = "property:Module"
            }
        });

        ArchitectureRoleIndex index = new(configuration, new ArchitectureTypeIndex([typeof(AnnotatedOrder).Assembly]));

        Assert.That(index.TryGetRole(typeof(AnnotatedOrder), out ArchitectureTypeClassificationResult descriptor), Is.True);
        Assert.That(descriptor.Role, Is.EqualTo("AggregateRoot"));
        Assert.That(descriptor.Source, Is.EqualTo(ArchitectureClassificationSource.TypeAttribute));
        Assert.That(descriptor.Metadata["boundedContext"], Is.EqualTo("Sales"));
        Assert.That(descriptor.Metadata["module"], Is.EqualTo("Orders"));
    }

    [Test]
    public void ExplicitlyMappedAssemblyAttribute_EntersExistingRoleIndexWithMetadata()
    {
        ArchitectureClassificationConfiguration configuration = new();
        configuration.AssemblyAttributes.Add(new ArchitectureAttributeClassificationMapping
        {
            Attribute = "ArchLinterNet.Annotations.DomainLayerAttribute",
            Role = "DomainLayer",
            Metadata = new Dictionary<string, object>
            {
                ["boundedContext"] = "property:BoundedContext",
                ["module"] = "property:Module"
            }
        });

        ArchitectureRoleIndex index = new(configuration, new ArchitectureTypeIndex([typeof(DomainLayerConsumerFixture).Assembly]));

        Assert.That(index.TryGetRole(typeof(DomainLayerConsumerFixture), out ArchitectureTypeClassificationResult descriptor), Is.True);
        Assert.That(descriptor.Role, Is.EqualTo("DomainLayer"));
        Assert.That(descriptor.Source, Is.EqualTo(ArchitectureClassificationSource.AssemblyAttribute));
        Assert.That(descriptor.Metadata["boundedContext"], Is.EqualTo("Commerce"));
        Assert.That(descriptor.Metadata["module"], Is.EqualTo("Orders"));
    }

    private static string ManifestPath() => Path.Combine(
        TestContext.CurrentContext.TestDirectory,
        "architecture",
        "semantic-annotations.v1.json");

    private static AttributeTargets ReadTargets(JsonElement targets)
    {
        AttributeTargets combined = 0;
        foreach (JsonElement target in targets.EnumerateArray())
        {
            combined |= Enum.Parse<AttributeTargets>(target.GetString()!, ignoreCase: true);
        }

        return combined;
    }

    [AggregateRoot(BoundedContext = "Sales", Module = "Orders")]
    private sealed class AnnotatedOrder;
}
