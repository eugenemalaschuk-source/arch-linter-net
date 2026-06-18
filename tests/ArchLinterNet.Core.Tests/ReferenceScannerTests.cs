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
}
