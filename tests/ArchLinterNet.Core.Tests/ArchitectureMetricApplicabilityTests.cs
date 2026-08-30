using System.Reflection;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Families;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Model;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class ArchitectureMetricApplicabilityTests
{
    [Test]
    public void Evaluate_AmbiguousTopologyTarget_IsUnassessableWithoutPartialValue()
    {
        Assembly coreAssembly = typeof(ArchitectureMetricMeasurement).Assembly;
        ArchitectureContractDocument document = new()
        {
            Name = "metric-ambiguous-topology",
            Topology = new ArchitectureTopology
            {
                Mode = "partial",
                SubjectKind = "type",
                Scope = new ArchitectureTopologyScope
                {
                    Selectors = [new ArchitectureTopologySubjectSelector { Namespace = "ArchLinterNet.Core.Model" }],
                },
                Nodes =
                [
                    Node("first", "ArchLinterNet.Core.Model"),
                    Node("second", "ArchLinterNet.Core.Model"),
                ],
            },
            Metrics = [TopologyMetric("type-count", ArchitectureMetricKinds.TopologyTypeCount, "first")],
        };
        using ArchitectureAnalysisContext context = CreateContext(coreAssembly);
        var session = new ArchitectureAnalysisSession(context, document, null, false, null);

        ArchitectureMetricMeasurement measurement = ArchitectureMetricEvaluator.Evaluate(session, document.Metrics)
            .Measurements.Single();

        AssertUnassessable(measurement);
    }

    [Test]
    public void Evaluate_UnresolvedPublicSurface_IsUnassessableWithoutPartialValue()
    {
        Assembly coreAssembly = typeof(ArchitectureMetricMeasurement).Assembly;
        ArchitectureContractDocument document = new()
        {
            Name = "metric-unresolved-public-surface",
            Contracts = new ArchitectureContractGroups
            {
                StrictPublicApiSurface =
                [
                    new ArchitecturePublicApiSurfaceContract
                    {
                        Id = "missing-public-api",
                        Name = "Missing public API",
                        Assemblies = ["Missing.Assembly"],
                    },
                ],
            },
            Metrics =
            [
                new ArchitectureMetricDefinition
                {
                    Id = "missing-public-surface",
                    Kind = ArchitectureMetricKinds.PublicContractSurfaceCount,
                    PublicApiSurface = "missing-public-api",
                },
            ],
        };
        using ArchitectureAnalysisContext context = CreateContext(coreAssembly);
        var session = new ArchitectureAnalysisSession(context, document, null, false, null);

        ArchitectureMetricMeasurement measurement = ArchitectureMetricEvaluator.Evaluate(session, document.Metrics)
            .Measurements.Single();

        AssertUnassessable(measurement);
    }

    [Test]
    public void Evaluate_ProjectFootprintWithoutCanonicalOwner_IsUnassessableWithoutPartialValue()
    {
        Assembly coreAssembly = typeof(ArchitectureMetricMeasurement).Assembly;
        ArchitectureContractDocument document = new()
        {
            Name = "metric-missing-project-owner",
            Topology = TypeTopology(),
            Metrics =
            [
                new ArchitectureMetricDefinition
                {
                    Id = "project-footprint",
                    Kind = ArchitectureMetricKinds.ComponentFootprintCount,
                    TopologyNode = "model",
                    Unit = "project",
                },
            ],
        };
        using ArchitectureAnalysisContext context = CreateContext(coreAssembly);
        var session = new ArchitectureAnalysisSession(context, document, null, false, null);

        ArchitectureMetricMeasurement measurement = ArchitectureMetricEvaluator.Evaluate(session, document.Metrics)
            .Measurements.Single();

        AssertUnassessable(measurement);
    }

    [Test]
    public void Evaluate_ComponentRelationWithEndpointOutsideTopologyScope_IsUnassessableWithoutPartialValue()
    {
        Assembly testAssembly = typeof(ExternalDependencyContractTestsFixtures.Core.CoreTypeWithPropertyAccess).Assembly;
        const string CoreNamespace = "ExternalDependencyContractTestsFixtures.Core";
        ArchitectureContractDocument document = new()
        {
            Name = "metric-unmapped-component-endpoint",
            Topology = new ArchitectureTopology
            {
                Mode = "partial",
                SubjectKind = "type",
                Scope = new ArchitectureTopologyScope
                {
                    Selectors = [new ArchitectureTopologySubjectSelector { Namespace = CoreNamespace }],
                },
                Nodes = [Node("core", CoreNamespace)],
            },
            Metrics = [TopologyMetric("core-outgoing", ArchitectureMetricKinds.OutgoingComponentCount, "core")],
        };
        using ArchitectureAnalysisContext context = CreateContext(testAssembly);
        var session = new ArchitectureAnalysisSession(context, document, null, false, null);

        ArchitectureMetricMeasurement measurement = ArchitectureMetricEvaluator.Evaluate(session, document.Metrics)
            .Measurements.Single();

        AssertUnassessable(measurement);
    }

    [Test]
    public void Evaluate_StaleMetricTopologyNode_IsUnassessableWithStaleDeclarationEvidence()
    {
        Assembly testAssembly = typeof(ExternalDependencyContractTestsFixtures.Core.PureCoreType).Assembly;
        const string CoreNamespace = "ExternalDependencyContractTestsFixtures.Core";
        ArchitectureContractDocument document = new()
        {
            Name = "metric-stale-topology-node",
            Topology = new ArchitectureTopology
            {
                Mode = "partial",
                SubjectKind = "type",
                StaleDeclarations = true,
                Scope = new ArchitectureTopologyScope
                {
                    Selectors = [new ArchitectureTopologySubjectSelector { Namespace = CoreNamespace }],
                },
                Nodes =
                [
                    Node("core", CoreNamespace),
                    Node("stale", "ExternalDependencyContractTestsFixtures.Adapters"),
                ],
            },
            Metrics = [TopologyMetric("stale-type-count", ArchitectureMetricKinds.TopologyTypeCount, "stale")],
        };
        using ArchitectureAnalysisContext context = CreateContext(testAssembly);
        var session = new ArchitectureAnalysisSession(context, document, null, false, null);

        ArchitectureMetricMeasurementOutcome outcome = ArchitectureMetricEvaluator.Evaluate(session, document.Metrics);
        ArchitectureMetricMeasurement measurement = outcome.Measurements.Single();
        ArchitectureApplicabilityRecord record = outcome.Applicability!.Controls.Single().Record!;

        Assert.Multiple(() =>
        {
            AssertUnassessable(measurement);
            Assert.That(record.Reasons.Select(reason => reason.Code),
                Is.EqualTo(new[] { ArchitectureApplicabilityReasonCodes.StaleDeclaration }));
        });
    }

    [Test]
    public void Evaluate_ExternalGroupMetric_IncludesMethodBodyOnlyFact()
    {
        Assembly testAssembly = typeof(ExternalDependencyContractTestsFixtures.Core.CoreTypeWithMethodCall).Assembly;
        const string CoreNamespace = "ExternalDependencyContractTestsFixtures.Core";
        ArchitectureContractDocument document = new()
        {
            Name = "metric-il-external-group",
            ExternalDependencies = new Dictionary<string, ArchitectureExternalDependencyGroup>(StringComparer.Ordinal)
            {
                ["vendor-sdk"] = new ArchitectureExternalDependencyGroup
                {
                    NamespacePrefixes = ["ExternalDependencyContractTestsFixtures.VendorSdk"],
                },
            },
            Topology = new ArchitectureTopology
            {
                Mode = "partial",
                SubjectKind = "type",
                Scope = new ArchitectureTopologyScope
                {
                    Selectors = [new ArchitectureTopologySubjectSelector { Namespace = CoreNamespace }],
                },
                Nodes = [Node("core", CoreNamespace)],
            },
            Metrics = [TopologyMetric("core-external", ArchitectureMetricKinds.ExternalDependencyGroupCount, "core")],
        };
        using ArchitectureAnalysisContext context = CreateContext(testAssembly);
        var session = new ArchitectureAnalysisSession(context, document, null, false, null);

        ArchitectureMetricMeasurement measurement = ArchitectureMetricEvaluator.Evaluate(session, document.Metrics)
            .Measurements.Single();

        Assert.Multiple(() =>
        {
            Assert.That(measurement.IsEvaluable, Is.True);
            Assert.That(measurement.Value, Is.EqualTo(1));
            Assert.That(measurement.Contributors, Is.EqualTo(new[] { "vendor-sdk" }));
        });
    }

    private static ArchitectureAnalysisContext CreateContext(Assembly assembly) => new(
        Path.GetTempPath(), [assembly], Array.Empty<string>(), Array.Empty<string>());

    private static void AssertUnassessable(ArchitectureMetricMeasurement measurement)
    {
        Assert.Multiple(() =>
        {
            Assert.That(measurement.IsUnassessable, Is.True);
            Assert.That(measurement.Value, Is.Null);
            Assert.That(measurement.Contributors, Is.Empty);
        });
    }

    private static ArchitectureMetricDefinition TopologyMetric(string id, string kind, string node) => new()
    {
        Id = id,
        Kind = kind,
        TopologyNode = node,
    };

    private static ArchitectureTopologyNode Node(string id, string @namespace) => new()
    {
        Id = id,
        Mappings = [new ArchitectureTopologySubjectSelector { Namespace = @namespace }],
    };

    private static ArchitectureTopology TypeTopology() => new()
    {
        Mode = "partial",
        SubjectKind = "type",
        Scope = new ArchitectureTopologyScope
        {
            Selectors = [new ArchitectureTopologySubjectSelector { Namespace = "ArchLinterNet.Core.Model" }],
        },
        Nodes = [Node("model", "ArchLinterNet.Core.Model")],
    };
}
