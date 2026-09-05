using System.Reflection;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Families;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Model;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class ArchitectureMetricCalculatorSeamTests
{
    [Test]
    public void TopologyCalculate_AssemblyFootprint_ReturnsRawEvidence()
    {
        const string Node = "source";
        const string AssemblyName = "Source";
        const string CanonicalAssemblyIdentity = "source-canonical";
        ArchitectureTopology topologyDefinition = AssemblyTopology(Node, AssemblyName);
        ArchitectureTopologyObservedSubject source = AssemblySubject(
            "source-identity", AssemblyName, CanonicalAssemblyIdentity);
        ArchitectureTopologyEvaluator.Projection projection = ArchitectureTopologyEvaluator.Project(
            session: null,
            topologyDefinition,
            [source],
            Array.Empty<ArchitectureTopologyObservedDependency>());
        ArchitectureTopologyEvaluator.SubjectClassification[] scoped = projection.Classifications
            .Where(classification => classification.NodeIds.Contains(Node, StringComparer.Ordinal))
            .ToArray();
        ArchitectureMetricDefinition definition = new()
        {
            Id = "assembly-footprint",
            Kind = ArchitectureMetricKinds.ComponentFootprintCount,
            TopologyNode = Node,
            Unit = "assembly",
        };
        using ArchitectureAnalysisContext context = CreateContext();
        var session = new ArchitectureAnalysisSession(
            context,
            new ArchitectureContractDocument { Name = "metric-calculator-seam", Topology = topologyDefinition },
            null,
            false,
            null);

        ArchitectureMetricRawEvidence evidence = ArchitectureTopologyMetricCalculator.Calculate(
            session, definition, projection, Node, scoped);

        Assert.Multiple(() =>
        {
            Assert.That(evidence.Scope, Is.EqualTo(Node));
            Assert.That(evidence.Unit, Is.EqualTo("assembly"));
            Assert.That(evidence.ReasonCodes, Is.Empty);
            Assert.That(evidence.Contributors, Is.EqualTo(new[] { CanonicalAssemblyIdentity }));
        });
    }

    [Test]
    public void TopologyCalculate_IncompleteOutgoingEvidence_ReturnsMissingInputEvidence()
    {
        const string Node = "source";
        const string AssemblyName = "Source";
        ArchitectureTopology topologyDefinition = AssemblyTopology(Node, AssemblyName);
        ArchitectureTopologyObservedSubject source = AssemblySubject(
            "source-identity", AssemblyName, "source-canonical");
        ArchitectureTopologyEvaluator.Projection projection = ArchitectureTopologyEvaluator.Project(
            session: null,
            topologyDefinition,
            [source],
            Array.Empty<ArchitectureTopologyObservedDependency>(),
            new HashSet<string>(StringComparer.Ordinal) { source.Identity });
        ArchitectureTopologyEvaluator.SubjectClassification[] scoped = projection.Classifications
            .Where(classification => classification.NodeIds.Contains(Node, StringComparer.Ordinal))
            .ToArray();
        ArchitectureMetricDefinition definition = new()
        {
            Id = "outgoing",
            Kind = ArchitectureMetricKinds.OutgoingComponentCount,
            TopologyNode = Node,
        };
        using ArchitectureAnalysisContext context = CreateContext();
        var session = new ArchitectureAnalysisSession(
            context,
            new ArchitectureContractDocument { Name = "metric-calculator-seam", Topology = topologyDefinition },
            null,
            false,
            null);

        ArchitectureMetricRawEvidence evidence = ArchitectureTopologyMetricCalculator.Calculate(
            session, definition, projection, Node, scoped);

        Assert.Multiple(() =>
        {
            Assert.That(evidence.Scope, Is.EqualTo(Node));
            Assert.That(evidence.Unit, Is.Null);
            Assert.That(evidence.ReasonCodes,
                Is.EqualTo(new[] { ArchitectureApplicabilityReasonCodes.MissingRequiredInput }));
            Assert.That(evidence.Contributors, Is.Empty);
        });
    }

    [Test]
    public void PublicContractCalculate_ResolvedSurface_ReturnsRawEvidence()
    {
        Assembly assembly = typeof(ArchitectureMetricCalculatorSeamTests).Assembly;
        string assemblyName = assembly.GetName().Name!;
        const string SurfaceId = "core-public-api";
        ArchitecturePublicApiSurfaceContract contract = new()
        {
            Id = SurfaceId,
            Name = "Core public API",
            Assemblies = [assemblyName],
        };
        ArchitectureContractDocument document = new()
        {
            Name = "metric-public-calculator-seam",
            Contracts = new ArchitectureContractGroups
            {
                StrictPublicApiSurface = [contract],
            },
        };
        ArchitectureMetricDefinition definition = new()
        {
            Id = "public-surface",
            Kind = ArchitectureMetricKinds.PublicContractSurfaceCount,
            PublicApiSurface = SurfaceId,
        };
        using ArchitectureAnalysisContext context = new(Path.GetTempPath(), [assembly], [], []);
        var session = new ArchitectureAnalysisSession(context, document, null, false, null);

        ArchitectureMetricRawEvidence evidence = ArchitecturePublicContractMetricCalculator.Calculate(
            session, definition);
        string canonicalAssemblyIdentity = ArchitectureTopologyMetricObserver.ResolveCanonicalAssemblyIdentity(assembly);

        Assert.Multiple(() =>
        {
            Assert.That(evidence.Scope, Is.EqualTo(SurfaceId));
            Assert.That(evidence.Unit, Is.Null);
            Assert.That(evidence.ReasonCodes, Is.Empty);
            Assert.That(evidence.Contributors, Is.Not.Empty);
            Assert.That(evidence.Contributors,
                Is.All.StartsWith(canonicalAssemblyIdentity + "|"));
        });
    }

    [Test]
    public void PublicContractCalculate_MissingSurface_ReturnsMissingInputEvidence()
    {
        Assembly assembly = typeof(ArchitectureMetricCalculatorSeamTests).Assembly;
        ArchitectureContractDocument document = new()
        {
            Name = "metric-public-calculator-seam",
        };
        ArchitectureMetricDefinition definition = new()
        {
            Id = "missing-public-surface",
            Kind = ArchitectureMetricKinds.PublicContractSurfaceCount,
            PublicApiSurface = "missing-public-api",
        };
        using ArchitectureAnalysisContext context = new(Path.GetTempPath(), [assembly], [], []);
        var session = new ArchitectureAnalysisSession(context, document, null, false, null);

        ArchitectureMetricRawEvidence evidence = ArchitecturePublicContractMetricCalculator.Calculate(
            session, definition);

        Assert.Multiple(() =>
        {
            Assert.That(evidence.Scope, Is.EqualTo("missing-public-api"));
            Assert.That(evidence.Unit, Is.Null);
            Assert.That(evidence.ReasonCodes,
                Is.EqualTo(new[] { ArchitectureApplicabilityReasonCodes.MissingRequiredInput }));
            Assert.That(evidence.Contributors, Is.Empty);
        });
    }

    private static ArchitectureTopology AssemblyTopology(string node, string assembly) => new()
    {
        Mode = "partial",
        SubjectKind = "assembly",
        Scope = new ArchitectureTopologyScope
        {
            Selectors = [new ArchitectureTopologySubjectSelector { Assembly = assembly }],
        },
        Nodes =
        [
            new ArchitectureTopologyNode
            {
                Id = node,
                Mappings = [new ArchitectureTopologySubjectSelector { Assembly = assembly }],
            },
        ],
    };

    private static ArchitectureTopologyObservedSubject AssemblySubject(
        string identity,
        string assembly,
        string canonicalAssemblyIdentity) => new(
        Identity: identity,
        Project: assembly,
        Assembly: assembly,
        Subject: assembly,
        CanonicalAssemblyIdentity: canonicalAssemblyIdentity,
        AssemblyReferenceIdentity: $"{assembly}, Version=1.0.0.0");

    private static ArchitectureAnalysisContext CreateContext() => new(
        Path.GetTempPath(),
        [typeof(ArchitectureMetricCalculatorSeamTests).Assembly],
        Array.Empty<string>(),
        Array.Empty<string>());
}
