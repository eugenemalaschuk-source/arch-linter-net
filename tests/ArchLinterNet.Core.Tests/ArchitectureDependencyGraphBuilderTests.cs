using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Families;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Model;
using NUnit.Framework;
using ArchitectureContractGroups = ArchLinterNet.Core.Contracts.Families.ArchitectureContractGroups;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class ArchitectureDependencyGraphBuilderTests
{
    private const string ExecutionNamespace = "ArchLinterNet.Core.Execution";
    private const string ContractsNamespace = "ArchLinterNet.Core.Contracts";
    private const string ReportingNamespace = "ArchLinterNet.Core.Reporting";

    private static ArchitectureAnalysisContext CreateContext()
    {
        return new ArchitectureAnalysisContext(
            "/tmp",
            new[] { typeof(ArchitectureContractDocument).Assembly },
            Array.Empty<string>(),
            Array.Empty<string>());
    }

    private static ArchitectureContractDocument CreateDirectDependencyDocument(string? contractId = "no-execution-to-contracts")
    {
        return new ArchitectureContractDocument
        {
            Version = 1,
            Name = "Test",
            Layers = new Dictionary<string, ArchitectureLayer>
            {
                ["execution"] = new() { Namespace = ExecutionNamespace },
                ["contracts"] = new() { Namespace = ContractsNamespace },
            },
            Analysis = new ArchitectureAnalysisConfiguration
            {
                TargetAssemblies = new List<string> { "ArchLinterNet.Core" },
            },
            Contracts = new ArchitectureContractGroups
            {
                Strict = new List<ArchitectureDependencyContract>
                {
                    new()
                    {
                        Id = contractId,
                        Name = "execution-must-not-depend-on-contracts",
                        Source = "execution",
                        Forbidden = new List<string> { "contracts" },
                    },
                },
            },
        };
    }

    private static ArchitectureContractDocument CreateTransitiveDependencyDocument(string contractId = "no-transitive-execution-to-contracts")
    {
        ArchitectureContractDocument document = CreateDirectDependencyDocument(contractId);
        document.Contracts.Strict[0].DependencyDepth = DependencyDepthMode.Transitive;
        return document;
    }

    [Test]
    public void Build_RepeatedBuilds_ProduceIdenticalNodesAndEdges()
    {
        ArchitectureContractDocument document = CreateDirectDependencyDocument();
        var context = CreateContext();

        var runner1 = new ArchitectureContractRunner(context, document);
        var violations1 = runner1.CheckContract(document.Contracts.Strict[0]).ToList();
        ArchitectureDependencyGraph graph1 = ArchitectureDependencyGraphBuilder.Build(
            runner1.Session, ArchitectureGraphLevel.Namespace, violations1);

        var runner2 = new ArchitectureContractRunner(context, document);
        var violations2 = runner2.CheckContract(document.Contracts.Strict[0]).ToList();
        ArchitectureDependencyGraph graph2 = ArchitectureDependencyGraphBuilder.Build(
            runner2.Session, ArchitectureGraphLevel.Namespace, violations2);

        Assert.That(graph1.Nodes, Is.EqualTo(graph2.Nodes));
        Assert.That(graph1.Edges.Select(EdgeKey), Is.EqualTo(graph2.Edges.Select(EdgeKey)));

        static string EdgeKey(ArchitectureGraphEdge edge) =>
            $"{edge.SourceId}|{edge.TargetId}|{edge.SourceKind}|{edge.TargetKind}|{string.Join(",", edge.ContractIds)}";
    }

    [Test]
    public void Build_NodesAreSortedByKindThenId()
    {
        ArchitectureContractDocument document = CreateDirectDependencyDocument();
        var context = CreateContext();
        var runner = new ArchitectureContractRunner(context, document);
        var violations = runner.CheckContract(document.Contracts.Strict[0]).ToList();

        ArchitectureDependencyGraph graph = ArchitectureDependencyGraphBuilder.Build(
            runner.Session, ArchitectureGraphLevel.Namespace, violations);

        var expected = graph.Nodes
            .OrderBy(n => (int)n.Kind)
            .ThenBy(n => n.Id, StringComparer.Ordinal)
            .ToList();
        Assert.That(graph.Nodes, Is.EqualTo(expected));
    }

    [Test]
    public void Build_EdgesAreSortedBySourceThenTarget()
    {
        ArchitectureContractDocument document = CreateDirectDependencyDocument();
        var context = CreateContext();
        var runner = new ArchitectureContractRunner(context, document);
        var violations = runner.CheckContract(document.Contracts.Strict[0]).ToList();

        ArchitectureDependencyGraph graph = ArchitectureDependencyGraphBuilder.Build(
            runner.Session, ArchitectureGraphLevel.Namespace, violations);

        var expected = graph.Edges
            .OrderBy(e => e.SourceId, StringComparer.Ordinal)
            .ThenBy(e => e.TargetId, StringComparer.Ordinal)
            .ToList();
        Assert.That(graph.Edges, Is.EqualTo(expected));
    }

    [Test]
    public void Build_DirectViolation_TagsExactNamespaceEdgeWithContractId()
    {
        ArchitectureContractDocument document = CreateDirectDependencyDocument("no-execution-to-contracts");
        var context = CreateContext();
        var runner = new ArchitectureContractRunner(context, document);
        var violations = runner.CheckContract(document.Contracts.Strict[0]).ToList();

        Assert.That(violations, Is.Not.Empty);

        ArchitectureDependencyGraph graph = ArchitectureDependencyGraphBuilder.Build(
            runner.Session, ArchitectureGraphLevel.Namespace, violations);

        ArchitectureGraphEdge edge = graph.Edges.Single(
            e => e.SourceId == ExecutionNamespace && e.TargetId == ContractsNamespace);

        Assert.That(edge.ContractIds, Does.Contain("no-execution-to-contracts"));
    }

    [Test]
    public void Build_TransitiveViolation_TagsEveryHopAtTypeLevel()
    {
        ArchitectureContractDocument document = CreateTransitiveDependencyDocument("no-transitive-execution-to-contracts");
        var context = CreateContext();
        var runner = new ArchitectureContractRunner(context, document);
        var violations = runner.CheckContract(document.Contracts.Strict[0]).ToList();

        ArchitectureViolation? withPath = violations.FirstOrDefault(v => (v.Payload as ConfigurationPayload)?.DependencyPaths is { Count: > 0 });
        Assert.That(withPath, Is.Not.Null, "expected at least one violation with a multi-hop DependencyPaths entry");

        IReadOnlyCollection<string> path = ((ConfigurationPayload)withPath!.Payload!).DependencyPaths!.First(p => p.Count > 2);

        ArchitectureDependencyGraph graph = ArchitectureDependencyGraphBuilder.Build(
            runner.Session, ArchitectureGraphLevel.Type, violations);

        string[] hops = path.ToArray();
        for (int i = 0; i < hops.Length - 1; i++)
        {
            ArchitectureGraphEdge edge = graph.Edges.Single(e => e.SourceId == hops[i] && e.TargetId == hops[i + 1]);
            Assert.That(edge.ContractIds, Does.Contain("no-transitive-execution-to-contracts"),
                $"hop {hops[i]} -> {hops[i + 1]} should be tagged with the violating contract's id");
        }
    }

    [Test]
    public void Build_TypeLevel_ExcludesTransitiveOnlyReferenceFromDirectEdges()
    {
        // Direct-mode dependency check should find far fewer forbidden references than transitive mode;
        // the type-level graph built from direct-mode violations must not contain the transitive-only hops.
        ArchitectureContractDocument directDocument = CreateDirectDependencyDocument("direct-check");
        var context = CreateContext();
        var directRunner = new ArchitectureContractRunner(context, directDocument);
        var directViolations = directRunner.CheckContract(directDocument.Contracts.Strict[0]).ToList();

        ArchitectureContractDocument transitiveDocument = CreateTransitiveDependencyDocument("transitive-check");
        var transitiveRunner = new ArchitectureContractRunner(context, transitiveDocument);
        var transitiveViolations = transitiveRunner.CheckContract(transitiveDocument.Contracts.Strict[0]).ToList();

        var directTargets = directViolations.SelectMany(v => v.ForbiddenReferences).ToHashSet();
        var transitiveTargets = transitiveViolations.SelectMany(v => v.ForbiddenReferences).ToHashSet();

        Assert.That(transitiveTargets.IsSupersetOf(directTargets), Is.True);
    }

    [Test]
    public void Build_AssemblyLevel_ContainsDirectReferenceEdge()
    {
        var document = new ArchitectureContractDocument
        {
            Version = 1,
            Name = "Test",
            Analysis = new ArchitectureAnalysisConfiguration
            {
                TargetAssemblies = new List<string> { "ArchLinterNet.Core", "ArchLinterNet.Testing" },
            },
        };

        var context = new ArchitectureAnalysisContext(
            "/tmp",
            new[] { typeof(ArchitectureContractDocument).Assembly, typeof(ArchLinterNet.Testing.ArchitectureValidationBuilder).Assembly },
            Array.Empty<string>(),
            Array.Empty<string>());

        var runner = new ArchitectureContractRunner(context, document);

        ArchitectureDependencyGraph graph = ArchitectureDependencyGraphBuilder.Build(
            runner.Session, ArchitectureGraphLevel.Assembly, Array.Empty<ArchitectureViolation>());

        Assert.That(graph.Nodes.All(n => n.Kind == ArchitectureGraphNodeKind.Assembly), Is.True);
        Assert.That(graph.Edges.Any(e => e.SourceId == "ArchLinterNet.Testing" && e.TargetId == "ArchLinterNet.Core"), Is.True);
    }

    [Test]
    public void Build_ExternalViolation_AddsExternalNodeAndTagsEdge()
    {
        var document = new ArchitectureContractDocument
        {
            Version = 1,
            Name = "Test",
            Layers = new Dictionary<string, ArchitectureLayer>
            {
                ["reporting"] = new() { Namespace = ReportingNamespace },
            },
            ExternalDependencies = new Dictionary<string, ArchitectureExternalDependencyGroup>
            {
                ["json"] = new() { NamespacePrefixes = new List<string> { "System.Text.Json" } },
            },
            Analysis = new ArchitectureAnalysisConfiguration
            {
                TargetAssemblies = new List<string> { "ArchLinterNet.Core" },
            },
            Contracts = new ArchitectureContractGroups
            {
                StrictExternal = new List<ArchitectureExternalDependencyContract>
                {
                    new()
                    {
                        Id = "reporting-no-json",
                        Name = "reporting-must-not-use-json",
                        Source = "reporting",
                        Forbidden = new List<string> { "json" },
                    },
                },
            },
        };

        var context = CreateContext();
        var runner = new ArchitectureContractRunner(context, document);
        var violations = runner.CheckExternalContract(document.Contracts.StrictExternal[0]).ToList();

        Assert.That(violations, Is.Not.Empty, "ArchLinterNet.Core.Reporting is expected to use System.Text.Json");

        ArchitectureDependencyGraph graph = ArchitectureDependencyGraphBuilder.Build(
            runner.Session, ArchitectureGraphLevel.Namespace, violations);

        ArchitectureGraphNode externalNode = graph.Nodes.Single(n => n.Kind == ArchitectureGraphNodeKind.External);
        Assert.That(externalNode.Id, Is.EqualTo("json"));

        ArchitectureGraphEdge edge = graph.Edges.Single(e => e.SourceId == ReportingNamespace && e.TargetId == "json");
        Assert.That(edge.ContractIds, Does.Contain("reporting-no-json"));
    }

    [Test]
    public void Build_ExternalGroupWithNoContract_StillProducesNodeAndEdgeWithEmptyContractIds()
    {
        var document = new ArchitectureContractDocument
        {
            Version = 1,
            Name = "Test",
            ExternalDependencies = new Dictionary<string, ArchitectureExternalDependencyGroup>
            {
                ["json"] = new() { NamespacePrefixes = new List<string> { "System.Text.Json" } },
            },
            Analysis = new ArchitectureAnalysisConfiguration
            {
                TargetAssemblies = new List<string> { "ArchLinterNet.Core" },
            },
            Contracts = new ArchitectureContractGroups(),
        };

        var context = CreateContext();
        var runner = new ArchitectureContractRunner(context, document);

        ArchitectureDependencyGraph graph = ArchitectureDependencyGraphBuilder.Build(
            runner.Session, ArchitectureGraphLevel.Namespace, Array.Empty<ArchitectureViolation>());

        ArchitectureGraphNode externalNode = graph.Nodes.Single(n => n.Kind == ArchitectureGraphNodeKind.External);
        Assert.That(externalNode.Id, Is.EqualTo("json"));

        ArchitectureGraphEdge edge = graph.Edges.Single(
            e => e.SourceId == ReportingNamespace && e.TargetId == "json");
        Assert.That(edge.ContractIds, Is.Empty,
            "No contract references this group, so the edge is real but not tied to a violation");
    }

    [Test]
    public void Build_DeclaredExternalGroupWithNoMatchingReference_StillProducesIsolatedNode()
    {
        var document = new ArchitectureContractDocument
        {
            Version = 1,
            Name = "Test",
            ExternalDependencies = new Dictionary<string, ArchitectureExternalDependencyGroup>
            {
                ["never-used"] = new() { NamespacePrefixes = new List<string> { "TotallyUnreferenced.Namespace" } },
            },
            Analysis = new ArchitectureAnalysisConfiguration
            {
                TargetAssemblies = new List<string> { "ArchLinterNet.Core" },
            },
            Contracts = new ArchitectureContractGroups(),
        };

        var context = CreateContext();
        var runner = new ArchitectureContractRunner(context, document);

        ArchitectureDependencyGraph graph = ArchitectureDependencyGraphBuilder.Build(
            runner.Session, ArchitectureGraphLevel.Namespace, Array.Empty<ArchitectureViolation>());

        ArchitectureGraphNode externalNode = graph.Nodes.Single(n => n.Id == "never-used");
        Assert.That(externalNode.Kind, Is.EqualTo(ArchitectureGraphNodeKind.External));
        Assert.That(graph.Edges.Any(e => e.TargetId == "never-used"), Is.False);
    }
}
