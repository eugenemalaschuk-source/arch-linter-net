using ArchLinterNet.Core.Scanning;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class TransitiveReferenceScannerTests
{
    [Test]
    public void GetTransitiveReferencedTypes_TypeWithProperties_ReturnsPropertyTypes()
    {
        Type type = typeof(Dictionary<string, List<int>>);

        var transitive = ArchitectureReferenceScanner.GetTransitiveReferencedTypes(type)
            .Select(x => x.referenced)
            .Distinct()
            .ToList();

        Assert.That(transitive, Does.Contain(typeof(string)));
        Assert.That(transitive, Does.Contain(typeof(int)));
    }

    [Test]
    public void GetTransitiveReferencedTypes_ReturnsPathWithSourceAsFirstElement()
    {
        Type type = typeof(List<string>);

        var results = ArchitectureReferenceScanner.GetTransitiveReferencedTypes(type).ToList();

        Assert.That(results, Is.Not.Empty);
        foreach (var (_, path) in results)
        {
            Assert.That(path.First(), Is.EqualTo(type));
        }
    }

    [Test]
    public void GetTransitiveReferencedTypes_DoesNotContainSelfReference()
    {
        Type type = typeof(List<string>);

        var results = ArchitectureReferenceScanner.GetTransitiveReferencedTypes(type)
            .Select(x => x.referenced)
            .ToList();

        Assert.That(results, Does.Not.Contain(type));
    }

    [Test]
    public void GetTransitiveReferencedTypes_PathEndsWithReferencedType()
    {
        Type type = typeof(Dictionary<string, int>);

        var results = ArchitectureReferenceScanner.GetTransitiveReferencedTypes(type).ToList();

        Assert.That(results, Is.Not.Empty);
        foreach (var (referenced, path) in results)
        {
            Assert.That(path.Last(), Is.EqualTo(referenced));
        }
    }

    [Test]
    public void GetTransitiveReferencedTypes_TransitivePathLongerThanDirect()
    {
        Type type = typeof(Dictionary<string, List<int>>);

        var direct = ArchitectureReferenceScanner.GetReferencedTypes(type)
            .Select(ArchitectureTypeNames.SafeFullName)
            .Where(n => !string.IsNullOrEmpty(n))
            .ToHashSet();

        var transitive = ArchitectureReferenceScanner.GetTransitiveReferencedTypes(type)
            .Select(x => ArchitectureTypeNames.SafeFullName(x.referenced))
            .Where(n => !string.IsNullOrEmpty(n))
            .ToHashSet();

        Assert.That(transitive.IsSupersetOf(direct), Is.True);
        Assert.That(transitive.Count, Is.GreaterThanOrEqualTo(direct.Count));
    }
}
