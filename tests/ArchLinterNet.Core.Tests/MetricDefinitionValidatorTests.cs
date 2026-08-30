using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Families;
using ArchLinterNet.Core.Contracts.Validators;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class MetricDefinitionValidatorTests
{
    [TestCaseSource(nameof(InvalidMetrics))]
    public void Validate_RejectsInvalidMetricDefinitions(
        ArchitectureContractDocument document,
        string expectedMessage)
    {
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(
            () => new MetricDefinitionValidator().Validate(document))!;

        Assert.That(error.Message, Is.EqualTo(expectedMessage));
    }

    [Test]
    public void Validate_AcceptsPublicMetricForDeclaredSurface()
    {
        ArchitectureContractDocument document = Document(
            new ArchitectureMetricDefinition
            {
                Id = "public-surface",
                Kind = ArchitectureMetricKinds.PublicContractSurfaceCount,
                PublicApiSurface = "surface",
            });
        document.Contracts = new ArchitectureContractGroups
        {
            StrictPublicApiSurface =
            [
                new ArchitecturePublicApiSurfaceContract
                {
                    Id = "surface",
                    Name = "Surface",
                    Assemblies = ["Fixture"],
                },
            ],
        };

        Assert.DoesNotThrow(() => new MetricDefinitionValidator().Validate(document));
    }

    private static IEnumerable<TestCaseData> InvalidMetrics()
    {
        yield return Case(
            Document(Metric(id: " ")),
            "Every metric definition must declare a non-empty id.");
        yield return Case(
            WithTopology(Document(Metric(), Metric())),
            "Duplicate metric id 'metric'.");
        yield return Case(
            Document(Metric(kind: "unknown")),
            "Metric 'metric' declares unsupported kind 'unknown'. Supported values are " +
            "'outgoing_component_count', 'incoming_component_count', 'external_dependency_group_count', " +
            "'component_footprint_count', 'topology_type_count', 'public_contract_surface_count'.");
        yield return Case(
            Document(Metric(topologyNode: null)),
            "Metric 'metric' kind 'outgoing_component_count' requires exactly one 'topology_node' target.");
        yield return Case(
            Document(new ArchitectureMetricDefinition
            {
                Id = "metric",
                Kind = ArchitectureMetricKinds.PublicContractSurfaceCount,
                TopologyNode = "node",
                PublicApiSurface = "surface",
            }),
            "Metric 'metric' kind 'public_contract_surface_count' requires exactly one 'public_api_surface' target.");
        yield return Case(
            WithTopology(Document(Metric(
                kind: ArchitectureMetricKinds.ComponentFootprintCount,
                unit: "namespace"))),
            "Metric 'metric' footprint must select unit 'project' or 'assembly'.");
        yield return Case(
            WithTopology(Document(Metric(unit: "assembly"))),
            "Metric 'metric' kind 'outgoing_component_count' does not accept 'unit'.");
        yield return Case(
            WithTopology(Document(Metric(unit: " "))),
            "Metric 'metric' kind 'outgoing_component_count' does not accept 'unit'.");
        yield return Case(
            WithTopology(Document(new ArchitectureMetricDefinition
            {
                Id = "metric",
                Kind = ArchitectureMetricKinds.OutgoingComponentCount,
                TopologyNode = "node",
                PublicApiSurface = " ",
            })),
            "Metric 'metric' kind 'outgoing_component_count' requires exactly one 'topology_node' target.");
        yield return Case(
            Document(new ArchitectureMetricDefinition
            {
                Id = "metric",
                Kind = ArchitectureMetricKinds.PublicContractSurfaceCount,
                TopologyNode = " ",
                PublicApiSurface = "surface",
            }),
            "Metric 'metric' kind 'public_contract_surface_count' requires exactly one 'public_api_surface' target.");
        yield return Case(
            Document(Metric()),
            "Metric 'metric' targets topology node 'node', but no topology is declared.");
        yield return Case(
            WithTopology(Document(Metric(topologyNode: "missing"))),
            "Metric 'metric' references undeclared topology node 'missing'.");
        yield return Case(
            WithTopology(
                Document(Metric(kind: ArchitectureMetricKinds.TopologyTypeCount)),
                subjectKind: "namespace"),
            "Metric 'metric' topology_type_count requires a topology with subject_kind 'type'.");
        yield return Case(
            Document(new ArchitectureMetricDefinition
            {
                Id = "metric",
                Kind = ArchitectureMetricKinds.PublicContractSurfaceCount,
                PublicApiSurface = "missing",
            }),
            "Metric 'metric' references unknown public API surface 'missing'.");
    }

    private static TestCaseData Case(ArchitectureContractDocument document, string expectedMessage) =>
        new TestCaseData(document, expectedMessage).SetName(expectedMessage);

    private static ArchitectureContractDocument Document(params ArchitectureMetricDefinition[] metrics) => new()
    {
        Name = "metric-validator",
        Metrics = [.. metrics],
    };

    private static ArchitectureContractDocument WithTopology(
        ArchitectureContractDocument document,
        string subjectKind = "type")
    {
        document.Topology = new ArchitectureTopology
        {
            Mode = "partial",
            SubjectKind = subjectKind,
            Nodes =
            [
                new ArchitectureTopologyNode
                {
                    Id = "node",
                    Mappings = [new ArchitectureTopologySubjectSelector { Namespace = "Fixture" }],
                },
            ],
        };
        return document;
    }

    private static ArchitectureMetricDefinition Metric(
        string id = "metric",
        string kind = ArchitectureMetricKinds.OutgoingComponentCount,
        string? topologyNode = "node",
        string? unit = null) => new()
        {
            Id = id,
            Kind = kind,
            TopologyNode = topologyNode,
            Unit = unit,
        };
}
