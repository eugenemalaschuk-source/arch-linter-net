using ArchLinterNet.Core.Scanning;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class ReferenceScannerTests
{
    [Test]
    public void GetReferencedTypes_TypeWithInterfaces_ReturnsInterfaceTypes()
    {
        Type type = typeof(List<int>);

        List<Type> referenced = ArchitectureReferenceScanner.GetReferencedTypes(type).ToList();

        Assert.That(referenced, Does.Contain(typeof(int)));
    }

    [Test]
    public void GetReferencedTypes_TypeWithProperties_ReturnsPropertyTypes()
    {
        Type type = typeof(Dictionary<string, int>);

        List<Type> referenced = ArchitectureReferenceScanner.GetReferencedTypes(type).ToList();

        Assert.That(referenced, Does.Contain(typeof(string)));
        Assert.That(referenced, Does.Contain(typeof(int)));
    }

    [Test]
    public void GetReferencedTypes_ReturnsNonEmptyList()
    {
        Type type = typeof(object);

        List<Type> referenced = ArchitectureReferenceScanner.GetReferencedTypes(type).ToList();

        Assert.That(referenced, Is.Not.Empty);
    }

    [Test]
    public void TryGetReferencedTypes_LoadableType_ReturnsTrue()
    {
        bool complete = ArchitectureReferenceScanner.TryGetReferencedTypes(typeof(Dictionary<string, int>), out List<Type> types);

        Assert.That(complete, Is.True);
        Assert.That(types, Does.Contain(typeof(string)));
    }

    [Test]
    public void TryGetReferencedTypes_TypeWithUnloadableFieldReference_ReturnsFalse()
    {
        // Regression (#306 review): a member whose type comes from a missing/unresolvable
        // dependency assembly must be detectable, not just silently dropped - reproduced here with a
        // genuinely unloadable field type (via two on-disk assemblies, one of which is deleted before
        // the consumer is loaded), mirroring the exact FileNotFoundException the scanner's Safe*
        // helpers already catch in production.
        using UnloadableFieldFixture fixture = UnloadableFieldFixture.Create();

        bool complete = ArchitectureReferenceScanner.TryGetReferencedTypes(fixture.SourceType, out List<Type> types);

        Assert.That(complete, Is.False);
        Assert.That(types.Any(t => t.Name == "UnloadableTargetType"), Is.False,
            "the field whose type couldn't be loaded must not silently appear as a resolved reference");
    }
}
