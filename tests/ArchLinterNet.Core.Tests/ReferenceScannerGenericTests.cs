using ArchLinterNet.Core.Scanning;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class ReferenceScannerGenericTests
{
    [Test]
    public void GetReferencedTypes_ListOfT_ContainsTypeArgument()
    {
        Type type = typeof(List<string>);

        List<Type> referenced = ArchitectureReferenceScanner.GetReferencedTypes(type).ToList();

        Assert.That(referenced, Does.Contain(typeof(string)));
    }

    [Test]
    public void GetReferencedTypes_dictionaryWithKeyValue_ContainsBothArguments()
    {
        Type type = typeof(Dictionary<int, string>);

        List<Type> referenced = ArchitectureReferenceScanner.GetReferencedTypes(type).ToList();

        Assert.That(referenced, Does.Contain(typeof(int)));
        Assert.That(referenced, Does.Contain(typeof(string)));
    }

    [Test]
    public void GetReferencedTypes_TaskOfT_ContainsTypeArgument()
    {
        Type type = typeof(Task<string>);

        List<Type> referenced = ArchitectureReferenceScanner.GetReferencedTypes(type).ToList();

        Assert.That(referenced, Does.Contain(typeof(string)));
    }

    [Test]
    public void GetReferencedTypes_NullableOfT_ContainsTypeArgument()
    {
        Type type = typeof(int?);

        List<Type> referenced = ArchitectureReferenceScanner.GetReferencedTypes(type).ToList();

        Assert.That(referenced, Does.Contain(typeof(int)));
    }

    [Test]
    public void GetReferencedTypes_ArrayOfT_ContainsElementType()
    {
        Type type = typeof(string[]);

        List<Type> referenced = ArchitectureReferenceScanner.GetReferencedTypes(type).ToList();

        Assert.That(referenced, Does.Contain(typeof(string)));
    }

    [Test]
    public void GetReferencedTypes_NestedGeneric_ContainsNestedArguments()
    {
        Type type = typeof(List<Dictionary<int, string>>);

        List<Type> referenced = ArchitectureReferenceScanner.GetReferencedTypes(type).ToList();

        Assert.That(referenced, Does.Contain(typeof(Dictionary<int, string>)));
        Assert.That(referenced, Does.Contain(typeof(int)));
        Assert.That(referenced, Does.Contain(typeof(string)));
    }

    [Test]
    public void GetReferencedTypes_IReadOnlyCollectionOfT_ContainsTypeArgument()
    {
        Type type = typeof(IReadOnlyCollection<int>);

        List<Type> referenced = ArchitectureReferenceScanner.GetReferencedTypes(type).ToList();

        Assert.That(referenced, Does.Contain(typeof(int)));
    }
}
