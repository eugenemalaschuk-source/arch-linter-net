using System.Reflection;
using System.Reflection.Emit;
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

    [Test]
    public void ExternalGroups_NamespaceTopologyBindsOnlyTheCanonicalOwner()
    {
        Type ownerA = CreateDynamicType("MetricNamespaceOwnerA", "Metric.Shared.Namespace.OwnerA");
        Type ownerB = CreateDynamicType("MetricNamespaceOwnerB", "Metric.Shared.Namespace.OwnerB");
        string ownerAAssembly = ownerA.Assembly.GetName().Name!;
        string ownerBAssembly = ownerB.Assembly.GetName().Name!;
        ArchitectureTopology topologyDefinition = new()
        {
            Mode = "partial",
            SubjectKind = "namespace",
            Scope = new ArchitectureTopologyScope
            {
                Selectors = [new ArchitectureTopologySubjectSelector { Namespace = "Metric.Shared.Namespace" }],
            },
            Nodes =
            [
                ProjectNode("owner-a", ownerAAssembly),
                ProjectNode("owner-b", ownerBAssembly),
            ],
        };
        ArchitectureContractDocument document = new()
        {
            Name = "metric-namespace-owner-binding",
            Topology = topologyDefinition,
        };
        using ArchitectureAnalysisContext context = CreateContext(ownerA.Assembly, ownerB.Assembly);
        var session = new ArchitectureAnalysisSession(context, document, null, false, null);
        ArchitectureTopologyEvaluator.ObservedSubject[] subjects =
        [
            NamespaceSubject(session, ownerA),
            NamespaceSubject(session, ownerB),
        ];
        ArchitectureTopologyEvaluator.Result topology = ArchitectureTopologyEvaluator.Evaluate(
            session, topologyDefinition, subjects, Array.Empty<ArchitectureTopologyEvaluator.ObservedDependency>());
        ArchitectureTopologyEvaluator.Projection projection = topology.FactProjection!;
        ArchitectureExternalDependencyFact[] facts = [new(ownerA, "Vendor.Client", "vendor-sdk")];

        HashSet<string> first = ArchitectureMetricEvaluator.ExternalGroups(
            session, projection, projection.Classifications, "owner-a", new List<string>(), facts);
        HashSet<string> second = ArchitectureMetricEvaluator.ExternalGroups(
            session, projection, projection.Classifications, "owner-b", new List<string>(), facts);

        Assert.Multiple(() =>
        {
            Assert.That(first, Is.EqualTo(new[] { "vendor-sdk" }));
            Assert.That(second, Is.Empty);
        });
    }

    [Test]
    public void Evaluate_AmbiguousAssemblyEndpoint_IsUnassessableWithoutSelectingAnOwner()
    {
        ArchitectureTopology topologyDefinition = new()
        {
            Mode = "partial",
            SubjectKind = "assembly",
            Scope = new ArchitectureTopologyScope
            {
                Selectors = [new ArchitectureTopologySubjectSelector { Assembly = "Source" }],
            },
            Nodes = [AssemblyNode("source", "Source")],
        };
        ArchitectureTopologyEvaluator.ObservedSubject source = AssemblySubject(
            "source", "Source", "source-canonical", "Source, Version=1.0.0.0");
        ArchitectureTopologyEvaluator.ObservedSubject targetFirst = AssemblySubject(
            "shared-first", "Shared", "shared-first-canonical", "Shared, Version=1.0.0.0");
        ArchitectureTopologyEvaluator.ObservedSubject targetSecond = AssemblySubject(
            "shared-second", "Shared", "shared-second-canonical", "Shared, Version=1.0.0.0");
        ArchitectureTopologyEvaluator.ObservedDependency dependency = ArchitectureTopologyEvaluator.BindAssemblyDependencies(
            [source, targetFirst, targetSecond],
            [
                new ArchitectureTopologyEvaluator.AssemblyDependencyObservation(
                    "Source",
                    "source-canonical",
                    [new ArchitectureTopologyEvaluator.AssemblyReferenceObservation(
                        "Shared", "Shared, Version=1.0.0.0")]),
            ]).Single();
        ArchitectureTopologyEvaluator.ObservedDependency unmatchedIdentityDependency =
            ArchitectureTopologyEvaluator.BindAssemblyDependencies(
                [source, targetFirst],
                [
                    new ArchitectureTopologyEvaluator.AssemblyDependencyObservation(
                        "Source",
                        "source-canonical",
                        [new ArchitectureTopologyEvaluator.AssemblyReferenceObservation(
                            "Shared", "Shared, Version=2.0.0.0")]),
                ]).Single();
        ArchitectureTopologyEvaluator.Result topology = ArchitectureTopologyEvaluator.Evaluate(
            session: null,
            topology: topologyDefinition,
            observedSubjects: [source, targetFirst, targetSecond],
            observedDependencies: [dependency]);
        ArchitectureContractDocument document = new()
        {
            Name = "metric-ambiguous-assembly-endpoint",
            Topology = topologyDefinition,
            Metrics = [TopologyMetric("source-outgoing", ArchitectureMetricKinds.OutgoingComponentCount, "source")],
        };
        using ArchitectureAnalysisContext context = CreateContext(typeof(ArchitectureMetricMeasurement).Assembly);
        var session = new ArchitectureAnalysisSession(context, document, null, false, null);

        ArchitectureMetricMeasurementOutcome outcome = ArchitectureMetricEvaluator.Evaluate(
            session, document.Metrics, topology);
        ArchitectureMetricMeasurement measurement = outcome.Measurements.Single();
        ArchitectureApplicabilityRecord record = outcome.Applicability!.Controls.Single().Record!;

        Assert.Multiple(() =>
        {
            Assert.That(dependency.TargetBinding,
                Is.EqualTo(ArchitectureTopologyEvaluator.AssemblyEndpointBinding.Ambiguous));
            Assert.That(unmatchedIdentityDependency.TargetBinding,
                Is.EqualTo(ArchitectureTopologyEvaluator.AssemblyEndpointBinding.Ambiguous));
            AssertUnassessable(measurement);
            Assert.That(record.Reasons.Select(reason => reason.Code),
                Is.EqualTo(new[] { ArchitectureApplicabilityReasonCodes.AmbiguousSubject }));
        });
    }

    private static ArchitectureAnalysisContext CreateContext(params Assembly[] assemblies) => new(
        Path.GetTempPath(), assemblies, Array.Empty<string>(), Array.Empty<string>());

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

    private static ArchitectureTopologyNode ProjectNode(string id, string project) => new()
    {
        Id = id,
        Mappings = [new ArchitectureTopologySubjectSelector { Project = project }],
    };

    private static ArchitectureTopologyNode AssemblyNode(string id, string assembly) => new()
    {
        Id = id,
        Mappings = [new ArchitectureTopologySubjectSelector { Assembly = assembly }],
    };

    private static ArchitectureTopologyEvaluator.ObservedSubject NamespaceSubject(
        ArchitectureAnalysisSession session,
        Type type)
    {
        string assembly = type.Assembly.GetName().Name!;
        string project = ArchitectureTopologyEvaluator.ResolveProjectForMetric(session, type);
        string canonicalAssembly = ArchitectureTopologyEvaluator.ResolveCanonicalAssemblyIdentityForMetric(type);
        string @namespace = type.Namespace!;
        return new ArchitectureTopologyEvaluator.ObservedSubject(
            ArchitectureTopologyEvaluator.BuildMetricSubjectIdentity(
                "namespace", project, assembly, canonicalAssembly, @namespace),
            project,
            assembly,
            @namespace,
            type,
            canonicalAssembly,
            type.Assembly.FullName);
    }

    private static ArchitectureTopologyEvaluator.ObservedSubject AssemblySubject(
        string id,
        string assembly,
        string canonicalAssembly,
        string assemblyReference) => new(
        Identity: id,
        Project: assembly,
        Assembly: assembly,
        Subject: assembly,
        CanonicalAssemblyIdentity: canonicalAssembly,
        AssemblyReferenceIdentity: assemblyReference);

    private static Type CreateDynamicType(string assemblyPrefix, string typeName)
    {
        AssemblyName name = new($"{assemblyPrefix}-{Guid.NewGuid():N}");
        AssemblyBuilder assembly = AssemblyBuilder.DefineDynamicAssembly(name, AssemblyBuilderAccess.Run);
        return assembly.DefineDynamicModule(name.Name!).DefineType(typeName).CreateType()!;
    }

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
