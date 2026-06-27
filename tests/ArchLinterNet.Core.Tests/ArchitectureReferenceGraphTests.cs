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
}
