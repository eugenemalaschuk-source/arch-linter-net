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

        Assert.That(cycles.Count, Is.GreaterThanOrEqualTo(1));
        Assert.That(cycles, Has.All.Contain("A"));
        Assert.That(cycles, Has.All.Contain("B"));
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

        Assert.That(cycles.Count, Is.GreaterThanOrEqualTo(1));
        Assert.That(cycles, Does.Contain("A -> B -> C -> A"));
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
}
