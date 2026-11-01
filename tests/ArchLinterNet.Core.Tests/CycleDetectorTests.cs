using ArchLinterNet.Core.Scanning;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class CycleDetectorTests
{
    [Test]
    public void FindCycles_NoCycles_ReturnsEmpty()
    {
        var graph = new Dictionary<string, HashSet<string>>
        {
            ["A"] = new() { "B" },
            ["B"] = new() { "C" },
            ["C"] = new()
        };

        IReadOnlyCollection<string> cycles = ArchitectureCycleDetector.FindCycles(graph);

        Assert.That(cycles, Is.Empty);
    }

    [Test]
    public void FindCycles_SimpleCycle_ReturnsCyclePaths()
    {
        var graph = new Dictionary<string, HashSet<string>>
        {
            ["A"] = new() { "B" },
            ["B"] = new() { "A" }
        };

        IReadOnlyCollection<string> cycles = ArchitectureCycleDetector.FindCycles(graph);

        Assert.That(cycles, Is.EqualTo((string[])["A -> B -> A", "B -> A -> B"]));
    }

    [Test]
    public void FindCycles_ComplexCycle_ReturnsCyclePaths()
    {
        var graph = new Dictionary<string, HashSet<string>>
        {
            ["A"] = new() { "B" },
            ["B"] = new() { "C" },
            ["C"] = new() { "A" }
        };

        IReadOnlyCollection<string> cycles = ArchitectureCycleDetector.FindCycles(graph);

        Assert.That(cycles, Is.EqualTo((string[])[
            "A -> B -> C -> A",
            "B -> C -> A -> B",
            "C -> A -> B -> C",
        ]));
    }

    [Test]
    public void FindCycles_MultipleComponentsAndAcyclicTails_ReturnsOnlySortedCycleRotations()
    {
        var graph = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal)
        {
            ["Tail"] = new(StringComparer.Ordinal) { "Sink" },
            ["D"] = new(StringComparer.Ordinal) { "C" },
            ["B"] = new(StringComparer.Ordinal) { "A" },
            ["Sink"] = new(StringComparer.Ordinal),
            ["C"] = new(StringComparer.Ordinal) { "D", "Tail" },
            ["A"] = new(StringComparer.Ordinal) { "B" },
            ["Isolated"] = new(StringComparer.Ordinal),
        };

        IReadOnlyCollection<string> cycles = ArchitectureCycleDetector.FindCycles(graph);

        Assert.That(cycles, Is.EqualTo((string[])[
            "A -> B -> A",
            "B -> A -> B",
            "C -> D -> C",
            "D -> C -> D",
        ]));

        var reordered = graph.Reverse()
            .ToDictionary(
                entry => entry.Key,
                entry => new HashSet<string>(entry.Value.Reverse(), StringComparer.Ordinal),
                StringComparer.Ordinal);
        Assert.That(ArchitectureCycleDetector.FindCycles(reordered), Is.EqualTo(cycles));
    }

    [Test]
    public void FindCycles_OverlappingCyclesAndOneWayEdgeBetweenCyclicComponents_ReturnsExactSortedCycles()
    {
        var graph = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal)
        {
            ["A"] = new(StringComparer.Ordinal) { "B", "C" },
            ["B"] = new(StringComparer.Ordinal) { "A", "C" },
            ["C"] = new(StringComparer.Ordinal) { "A", "D" },
            ["D"] = new(StringComparer.Ordinal) { "E" },
            ["E"] = new(StringComparer.Ordinal) { "D" },
        };

        IReadOnlyCollection<string> cycles = ArchitectureCycleDetector.FindCycles(graph);

        Assert.That(cycles, Is.EqualTo((string[])[
            "A -> B -> A",
            "A -> B -> C -> A",
            "A -> C -> A",
            "B -> A -> B",
            "B -> C -> A -> B",
            "C -> A -> B -> C",
            "C -> A -> C",
            "D -> E -> D",
            "E -> D -> E",
        ]));
    }

    [Test]
    public void FindCycles_SelfEdge_ReturnsSelfCycle()
    {
        var graph = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal)
        {
            ["A"] = new(StringComparer.Ordinal) { "A" },
        };

        IReadOnlyCollection<string> cycles = ArchitectureCycleDetector.FindCycles(graph);

        Assert.That(cycles, Is.EqualTo((string[])["A -> A"]));
    }

    [Test]
    public void FindCycles_DisconnectedGraph_NoFalseCycles()
    {
        var graph = new Dictionary<string, HashSet<string>>
        {
            ["A"] = new(),
            ["B"] = new(),
            ["C"] = new()
        };

        IReadOnlyCollection<string> cycles = ArchitectureCycleDetector.FindCycles(graph);

        Assert.That(cycles, Is.Empty);
    }

    [Test]
    public void FindCycles_EmptyGraph_ReturnsEmpty()
    {
        var graph = new Dictionary<string, HashSet<string>>();

        IReadOnlyCollection<string> cycles = ArchitectureCycleDetector.FindCycles(graph);

        Assert.That(cycles, Is.Empty);
    }

    [TestCase("dense", 10)]
    [TestCase("dense", 20)]
    [TestCase("dense", 30)]
    [TestCase("dense", 50)]
    [TestCase("layered", 10)]
    [TestCase("layered", 20)]
    [TestCase("layered", 30)]
    [TestCase("layered", 50)]
    public void FindCycles_AcyclicScaleShapes_UseBoundedGraphWork(string shape, int vertexCount)
    {
        Dictionary<string, HashSet<string>> graph = shape switch
        {
            "dense" => CreateCompleteOrderedDag(vertexCount),
            "layered" => CreateLayeredDag(vertexCount),
            _ => throw new ArgumentOutOfRangeException(nameof(shape), shape, "Unknown DAG shape."),
        };
        long edgeCount = graph.Values.Sum(targets => (long)targets.Count);
        ArchitectureCycleDetectorWorkCounter work = new();

        IReadOnlyCollection<string> cycles = ArchitectureCycleDetector.FindCycles(graph, work);

        Assert.That(cycles, Is.Empty);
        Assert.That(work.PreparedVertexCount, Is.EqualTo(vertexCount));
        Assert.That(work.PreparedEdgeCount, Is.EqualTo(edgeCount));
        Assert.That(work.AdjacencyListsSorted, Is.EqualTo(vertexCount));
        Assert.That(work.ReverseDegreeEdgeVisits, Is.EqualTo(edgeCount));
        Assert.That(work.ReverseFillEdgeVisits, Is.EqualTo(edgeCount));
        Assert.That(work.FirstPassVertexVisits, Is.EqualTo(vertexCount));
        Assert.That(work.FirstPassEdgeVisits, Is.EqualTo(edgeCount));
        Assert.That(work.SecondPassVertexVisits, Is.EqualTo(vertexCount));
        Assert.That(work.SecondPassEdgeVisits, Is.EqualTo(edgeCount));
        Assert.That(work.CycleAdjacencyFilterEdgeVisits, Is.Zero);
        Assert.That(work.CycleAdjacencyCopyEdgeVisits, Is.Zero);
        Assert.That(work.CycleSearchVertexVisits, Is.Zero);
        Assert.That(work.CycleSearchEdgeCandidates, Is.Zero);
        Assert.That(work.EmittedCycleCount, Is.Zero);
    }

    private static Dictionary<string, HashSet<string>> CreateCompleteOrderedDag(int vertexCount)
    {
        Dictionary<string, HashSet<string>> graph = new(StringComparer.Ordinal);
        for (int source = 0; source < vertexCount; source++)
        {
            HashSet<string> targets = new(StringComparer.Ordinal);
            for (int target = source + 1; target < vertexCount; target++)
            {
                targets.Add(NodeName(target));
            }

            graph.Add(NodeName(source), targets);
        }

        return graph;
    }

    private static Dictionary<string, HashSet<string>> CreateLayeredDag(int vertexCount)
    {
        const int LayerCount = 5;
        int layerWidth = vertexCount / LayerCount;
        Dictionary<string, HashSet<string>> graph = new(StringComparer.Ordinal);
        for (int layer = 0; layer < LayerCount; layer++)
        {
            for (int index = 0; index < layerWidth; index++)
            {
                HashSet<string> targets = new(StringComparer.Ordinal);
                if (layer < LayerCount - 1)
                {
                    for (int targetIndex = 0; targetIndex < layerWidth; targetIndex++)
                    {
                        targets.Add(LayerNodeName(layer + 1, targetIndex));
                    }
                }

                graph.Add(LayerNodeName(layer, index), targets);
            }
        }

        return graph;
    }

    private static string NodeName(int index)
    {
        return $"N{index:D2}";
    }

    private static string LayerNodeName(int layer, int index)
    {
        return $"L{layer:D2}N{index:D2}";
    }
}
