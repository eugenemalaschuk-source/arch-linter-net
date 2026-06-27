using System.Reflection;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Discovery;
using ArchLinterNet.Core.Execution;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class ArchitectureCoverageInventoryTests
{
    private static readonly Assembly[] _targetAssemblies = { typeof(ArchitectureCoverageInventoryTests).Assembly };

    private const string AlphaNamespace = "ArchLinterNet.Core.Tests.CoverageInventoryFixtures.Alpha";
    private const string BetaNamespace = "ArchLinterNet.Core.Tests.CoverageInventoryFixtures.Beta";

    private static ArchitectureAnalysisSession CreateSession()
    {
        var context = new ArchitectureAnalysisContext(
            repositoryRoot: AppContext.BaseDirectory,
            targetAssemblies: _targetAssemblies,
            missingAssemblyNames: Array.Empty<string>(),
            assemblyProbingPaths: Array.Empty<string>());

        return new ArchitectureAnalysisSession(context);
    }

    private static ArchitectureContractDocument CreateDocument()
    {
        var document = new ArchitectureContractDocument();
        document.Layers["alpha"] = new ArchitectureLayer { Namespace = AlphaNamespace };
        document.Layers["beta"] = new ArchitectureLayer { Namespace = BetaNamespace };
        document.Contracts.StrictLayerTemplates.Add(new ArchitectureLayerTemplateContract
        {
            Name = "fixture-template",
            Containers = { AlphaNamespace },
            Layers = { new ArchitectureTemplateLayer { Name = "Inner" } },
            Exhaustive = true,
            Reason = "fixture"
        });
        return document;
    }

    [Test]
    public void Build_CollectsNamespacesSortedOrdinallyWithRepresentativeType()
    {
        ArchitectureCoverageInventory inventory = ArchitectureCoverageInventory.Build(CreateDocument(), CreateSession());

        var alpha = inventory.Namespaces.Single(n => n.Namespace == AlphaNamespace);
        var beta = inventory.Namespaces.Single(n => n.Namespace == BetaNamespace);

        Assert.That(alpha.RepresentativeType, Is.EqualTo($"{AlphaNamespace}.AlphaOtherType"));
        Assert.That(beta.RepresentativeType, Is.EqualTo($"{BetaNamespace}.BetaOtherType"));

        var ordered = inventory.Namespaces.Select(n => n.Namespace).ToList();
        var expectedOrder = ordered.OrderBy(n => n, StringComparer.Ordinal).ToList();
        Assert.That(ordered, Is.EqualTo(expectedOrder));
    }

    [Test]
    public void Build_RepeatedBuilds_ProduceIdenticalNamespaceOrderingAndRepresentativeTypes()
    {
        ArchitectureContractDocument document = CreateDocument();
        ArchitectureCoverageInventory first = ArchitectureCoverageInventory.Build(document, CreateSession());
        ArchitectureCoverageInventory second = ArchitectureCoverageInventory.Build(document, CreateSession());

        Assert.That(first.Namespaces, Is.EqualTo(second.Namespaces));
    }

    [Test]
    public void DependencyEdges_DeduplicatesAndExcludesSelfEdges_SortedBySourceThenTarget()
    {
        ArchitectureCoverageInventory inventory = ArchitectureCoverageInventory.Build(CreateDocument(), CreateSession());

        var edges = inventory.DependencyEdges;

        Assert.That(edges.Count(e => e.SourceNamespace == AlphaNamespace && e.TargetNamespace == BetaNamespace), Is.EqualTo(1));
        Assert.That(edges.Any(e => e.SourceNamespace == AlphaNamespace && e.TargetNamespace == AlphaNamespace), Is.False);

        var orderedBySource = edges.OrderBy(e => e.SourceNamespace, StringComparer.Ordinal)
            .ThenBy(e => e.TargetNamespace, StringComparer.Ordinal)
            .ToList();
        Assert.That(edges, Is.EqualTo(orderedBySource));
    }

    [Test]
    public void Build_PreservesExhaustiveLayerTemplateExpansion()
    {
        ArchitectureCoverageInventory inventory = ArchitectureCoverageInventory.Build(CreateDocument(), CreateSession());

        var expansion = inventory.ExpandedLayerTemplates.Single();

        Assert.That(expansion.Exhaustive, Is.True);
        Assert.That(expansion.ContainerNamespace, Is.EqualTo(AlphaNamespace));
    }

    [Test]
    public void Build_WithProjectDiscoveryResult_ExposesItVerbatim()
    {
        var discoveryResult = new ProjectDiscoveryResult(
            new[] { "Fixture.Assembly" },
            new[] { "bin/Debug/net10.0" },
            new[] { "src/Fixture" },
            Array.Empty<ArchitectureProjectDiscoveryDiagnostic>());

        ArchitectureCoverageInventory inventory = ArchitectureCoverageInventory.Build(
            CreateDocument(), CreateSession(), discoveryResult);

        Assert.That(inventory.ProjectDiscovery, Is.SameAs(discoveryResult));
    }

    [Test]
    public void Build_WithoutProjectDiscoveryResult_IsAbsent()
    {
        ArchitectureCoverageInventory inventory = ArchitectureCoverageInventory.Build(CreateDocument(), CreateSession());

        Assert.That(inventory.ProjectDiscovery, Is.Null);
    }

    [Test]
    public void Session_ExposesCoverageInventoryOnlyThroughExplicitAccessor()
    {
        ArchitectureAnalysisSession session = CreateSession();

        ArchitectureCoverageInventory inventory = session.BuildCoverageInventory(CreateDocument());

        Assert.That(inventory.Namespaces, Is.Not.Empty);
    }
}
