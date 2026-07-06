using ArchLinterNet.Core.Graph;
using ArchLinterNet.Core.Model;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class ArchitectureGraphFormatterTests
{
    private static readonly ArchitectureGraphFormatter _formatter = new();

    private static ArchitectureDependencyGraph CreateGraphWithIsolatedNode()
    {
        return new ArchitectureDependencyGraph(
            new[]
            {
                new ArchitectureGraphNode("A", ArchitectureGraphNodeKind.Namespace),
                new ArchitectureGraphNode("B", ArchitectureGraphNodeKind.Namespace),
                new ArchitectureGraphNode("Isolated", ArchitectureGraphNodeKind.Namespace),
            },
            new[]
            {
                new ArchitectureGraphEdge(
                    "A", "B", ArchitectureGraphNodeKind.Namespace, ArchitectureGraphNodeKind.Namespace,
                    Array.Empty<string>()),
            });
    }

    [Test]
    public void FormatAsDot_IsolatedNode_IsStillDeclared()
    {
        string dot = _formatter.FormatAsDot(CreateGraphWithIsolatedNode());

        Assert.That(dot, Does.Contain("\"A\" -> \"B\""));
        Assert.That(dot, Does.Contain("\"Isolated\";"));
    }

    [Test]
    public void FormatAsMermaid_IsolatedNode_IsStillDeclared()
    {
        string mermaid = _formatter.FormatAsMermaid(CreateGraphWithIsolatedNode());

        Assert.That(mermaid, Does.Contain("\"Isolated\""));
    }

    [Test]
    public void FormatAsJson_IncludesIsolatedNode()
    {
        string json = _formatter.FormatAsJson(CreateGraphWithIsolatedNode());

        Assert.That(json, Does.Contain("\"Isolated\""));
    }
}
