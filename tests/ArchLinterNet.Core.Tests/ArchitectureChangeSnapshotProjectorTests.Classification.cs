using System.Reflection;
using System.Reflection.Emit;
using ArchLinterNet.Core.Change;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Model;
using AttributeRoleExtractionTestFixtures;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

public sealed partial class ArchitectureChangeSnapshotProjectorTests
{
    [Test]
    public void Project_DeduplicatesEquivalentRoleFactsRegardlessOfMetadataEnumerationOrder()
    {
        ArchitectureClassificationRoleFact[] roles =
        [
            new(
                "Acme.Order",
                "aggregate",
                ArchitectureClassificationSource.TypeAttribute,
                "Acme.Marker",
                new Dictionary<string, object>
                {
                    ["bounded_context"] = "Sales",
                    ["module"] = "Ordering",
                }),
            new(
                "Acme.Order",
                "aggregate",
                ArchitectureClassificationSource.TypeAttribute,
                "Acme.Marker",
                new Dictionary<string, object>
                {
                    ["module"] = "Ordering",
                    ["bounded_context"] = "Sales",
                }),
        ];

        ArchitectureChangeSnapshot snapshot = ArchitectureChangeSnapshotProjector.Project(
            "strict",
            Outcome("/repo", "/repo/src/Acme/Acme.csproj", roles: roles),
            EmptyGraph(),
            EmptyGraph(),
            Array.Empty<ArchitectureBaselineComparisonEntry>());

        Assert.Multiple(() =>
        {
            Assert.That(snapshot.Entries.Count(static entry => entry.Kind == "semantic_role"), Is.EqualTo(1));
            Assert.That(snapshot.Entries.Count(static entry => entry.Kind == "semantic_context"), Is.EqualTo(2));
            Assert.DoesNotThrow(() => ArchitectureChangeReports.SerializeSnapshot(snapshot));
        });
    }

    [Test]
    public void Project_PreservesStructurallyDifferentRoleFactsThatCollideInSerializedIdentity()
    {
        ArchitectureClassificationRoleFact[] roles =
        [
            new(
                "Acme.Order",
                "aggregate",
                ArchitectureClassificationSource.TypeAttribute,
                "Acme.Marker",
                new Dictionary<string, object> { ["tag"] = "x;y=z" }),
            new(
                "Acme.Order",
                "aggregate",
                ArchitectureClassificationSource.TypeAttribute,
                "Acme.Marker",
                new Dictionary<string, object>
                {
                    ["tag"] = "x",
                    ["y"] = "z",
                }),
        ];

        ArchitectureChangeSnapshot snapshot = ArchitectureChangeSnapshotProjector.Project(
            "strict",
            Outcome("/repo", "/repo/src/Acme/Acme.csproj", roles: roles),
            EmptyGraph(),
            EmptyGraph(),
            Array.Empty<ArchitectureBaselineComparisonEntry>());

        Assert.That(snapshot.Entries.Count(static entry => entry.Kind == "semantic_role"), Is.EqualTo(2));
        ArgumentException? exception = Assert.Throws<ArgumentException>(
            () => ArchitectureChangeReports.SerializeSnapshot(snapshot));

        Assert.That(exception!.Message, Does.Contain("duplicate or empty entry identities"));
    }

    [Test]
    public void Project_DeduplicatesEquivalentFactsFromDistinctDynamicAssemblies()
    {
        Type firstType = DefineLinkedMarkerType("ArchitectureChangeSnapshotProjectorTests.First");
        Type secondType = DefineLinkedMarkerType("ArchitectureChangeSnapshotProjectorTests.Second");
        ArchitectureClassificationConfiguration classification = new()
        {
            Attributes =
            {
                new ArchitectureAttributeClassificationMapping
                {
                    Attribute = "AttributeRoleExtractionTestFixtures.DomainMarkerAttribute",
                    Role = "DomainLayer",
                    Metadata = new Dictionary<string, object> { ["domain"] = "constructor[0]" },
                },
            },
        };
        ArchitectureContractDocument document = new()
        {
            Version = 1,
            Name = "Test",
            Classification = classification,
        };

        using ArchitectureAnalysisContext context = new(
            "/repo",
            new[] { firstType.Assembly, secondType.Assembly },
            Array.Empty<string>(),
            Array.Empty<string>());
        ArchitectureAnalysisSession session = new(context, document, null, false, null);

        IReadOnlyCollection<Type> classifiedTypes = session.RoleIndex.ClassifiedTypes();
        IReadOnlyList<ArchitectureClassificationRoleFact> roles = session.CheckClassificationRoles();
        ArchitectureChangeSnapshot snapshot = ArchitectureChangeSnapshotProjector.Project(
            "strict",
            Outcome("/repo", "/repo/src/Acme/Acme.csproj", roles: roles),
            EmptyGraph(),
            EmptyGraph(),
            Array.Empty<ArchitectureBaselineComparisonEntry>());

        Assert.Multiple(() =>
        {
            Assert.That(firstType.Assembly, Is.Not.SameAs(secondType.Assembly));
            Assert.That(firstType, Is.Not.SameAs(secondType));
            Assert.That(classifiedTypes, Has.Count.EqualTo(2));
            Assert.That(classifiedTypes, Does.Contain(firstType));
            Assert.That(classifiedTypes, Does.Contain(secondType));
            Assert.That(roles, Has.Count.EqualTo(2));
            Assert.That(roles.Select(static role => role.Subject).Distinct(StringComparer.Ordinal), Has.Length.EqualTo(1));
            Assert.That(roles.Select(static role => role.Role), Is.All.EqualTo("DomainLayer"));
            Assert.That(roles.Select(static role => role.Metadata["domain"]), Is.All.EqualTo("Sales"));
            Assert.That(snapshot.Entries.Count(static entry => entry.Kind == "semantic_role"), Is.EqualTo(1));
            Assert.That(snapshot.Entries.Count(static entry => entry.Kind == "semantic_context"), Is.EqualTo(1));
            Assert.DoesNotThrow(() => ArchitectureChangeReports.SerializeSnapshot(snapshot));
        });
    }

    private static Type DefineLinkedMarkerType(string assemblyName)
    {
        AssemblyBuilder assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(
            new AssemblyName(assemblyName), AssemblyBuilderAccess.Run);
        ModuleBuilder moduleBuilder = assemblyBuilder.DefineDynamicModule(assemblyName + "Module");
        TypeBuilder typeBuilder = moduleBuilder.DefineType(
            "LinkedMarkerEquivalent.Domain.Order",
            TypeAttributes.Public | TypeAttributes.Class);
        ConstructorInfo markerConstructor = typeof(DomainMarkerAttribute).GetConstructor(
            new[] { typeof(string), typeof(string) })!;
        typeBuilder.SetCustomAttribute(
            new CustomAttributeBuilder(markerConstructor, new object[] { "Sales", "UnknownModule" }));
        return typeBuilder.CreateType()!;
    }
}
