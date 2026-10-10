using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Scanning;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class ArchitectureReferenceGraphTests
{
    [Test]
    public void GetReferencedTypes_MatchesDirectScannerOutput()
    {
        var graph = new ArchitectureReferenceGraph();

        IReadOnlyList<Type> fromGraph = graph.GetReferencedTypes(typeof(Dictionary<string, int>));
        var direct = ArchitectureReferenceScanner.GetReferencedTypes(typeof(Dictionary<string, int>)).ToList();

        Assert.That(fromGraph, Is.EqualTo(direct));
    }

    [Test]
    public void GetReferencedTypes_RepeatedCalls_ReturnSameCachedInstance()
    {
        var graph = new ArchitectureReferenceGraph();

        IReadOnlyList<Type> first = graph.GetReferencedTypes(typeof(List<int>));
        IReadOnlyList<Type> second = graph.GetReferencedTypes(typeof(List<int>));

        Assert.That(first, Is.SameAs(second));
    }

    [Test]
    public void TryGetReferencedTypes_UnloadableMember_PreservesBestEffortListAndReportsIncomplete()
    {
        using UnloadableFieldFixture fixture = UnloadableFieldFixture.Create();
        var graph = new ArchitectureReferenceGraph();

        bool isComplete = graph.TryGetReferencedTypes(fixture.SourceType, out IReadOnlyList<Type> referenced);

        Assert.Multiple(() =>
        {
            Assert.That(isComplete, Is.False);
            Assert.That(graph.GetReferencedTypes(fixture.SourceType), Is.SameAs(referenced));
            Assert.That(referenced.Any(type => type.Name == "UnloadableTargetType"), Is.False);
        });
    }

    [Test]
    public void EnumerateTransitiveReferencedTypes_MaterializedPaths_MatchesCompatibilityTraversal()
    {
        var graph = new ArchitectureReferenceGraph();
        Type source = typeof(List<string>);
        var expected = graph.GetTransitiveReferencedTypes(source)
            .Select(entry => (entry.referenced, entry.path.ToArray()))
            .ToArray();
        ArchitectureTransitiveReference[] traversals = graph
            .EnumerateTransitiveReferencedTypes(source)
            .ToArray();

        var actual = traversals
            .Select(entry => (entry.Referenced, entry.BuildPath().ToArray()))
            .ToArray();

        Assert.That(actual, Is.EqualTo(expected));
    }
}
