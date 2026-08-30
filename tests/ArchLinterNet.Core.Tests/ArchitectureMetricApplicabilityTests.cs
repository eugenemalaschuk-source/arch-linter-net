using System.Reflection;
using System.Reflection.Emit;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Families;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Model;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed partial class ArchitectureMetricApplicabilityTests
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
            session, projection, "owner-a", new List<string>(), facts);
        HashSet<string> second = ArchitectureMetricEvaluator.ExternalGroups(
            session, projection, "owner-b", new List<string>(), facts);

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
            "shared-second", "Shared", "shared-second-canonical", "Shared, Version=2.0.0.0");
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

    [Test]
    public void Evaluate_AmbiguousSelectedAssemblyEndpoint_IsUnassessableWithoutTrustedZero()
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
        ArchitectureTopologyEvaluator.ObservedSubject sourceFirst = AssemblySubject(
            "source-first", "Source", "source-first-canonical", "Source, Version=1.0.0.0");
        ArchitectureTopologyEvaluator.ObservedSubject sourceSecond = AssemblySubject(
            "source-second", "Source", "source-second-canonical", "Source, Version=2.0.0.0");
        ArchitectureTopologyEvaluator.ObservedSubject target = AssemblySubject(
            "target", "Target", "target-canonical", "Target, Version=1.0.0.0");
        ArchitectureTopologyEvaluator.ObservedDependency dependency = ArchitectureTopologyEvaluator.BindAssemblyDependencies(
            [sourceFirst, sourceSecond, target],
            [
                new ArchitectureTopologyEvaluator.AssemblyDependencyObservation(
                    "Source",
                    "source-first-canonical",
                    [new ArchitectureTopologyEvaluator.AssemblyReferenceObservation(
                        "Target", "Target, Version=1.0.0.0")]),
            ]).Single();
        ArchitectureTopologyEvaluator.Result topology = ArchitectureTopologyEvaluator.Evaluate(
            session: null,
            topology: topologyDefinition,
            observedSubjects: [sourceFirst, sourceSecond, target],
            observedDependencies: [dependency]);
        ArchitectureContractDocument document = new()
        {
            Name = "metric-ambiguous-selected-assembly-endpoint",
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
            Assert.That(dependency.SourceBinding,
                Is.EqualTo(ArchitectureTopologyEvaluator.AssemblyEndpointBinding.Ambiguous));
            Assert.That(dependency.SourceAssemblyName, Is.EqualTo("Source"));
            AssertUnassessable(measurement);
            Assert.That(record.Reasons.Select(reason => reason.Code),
                Is.EqualTo(new[] { ArchitectureApplicabilityReasonCodes.AmbiguousSubject }));
        });
    }

    [Test]
    public void BindAssemblyDependencies_ExcludesExternalMetadataReferenceFromRetainedFirstPartyGraph()
    {
        ArchitectureTopologyEvaluator.ObservedSubject source = AssemblySubject(
            "source", "Source", "source-canonical", "Source, Version=1.0.0.0");

        IReadOnlyList<ArchitectureTopologyEvaluator.ObservedDependency> dependencies =
            ArchitectureTopologyEvaluator.BindAssemblyDependencies(
                [source],
                [
                    new ArchitectureTopologyEvaluator.AssemblyDependencyObservation(
                        "Source",
                        "source-canonical",
                        [new ArchitectureTopologyEvaluator.AssemblyReferenceObservation(
                            "External.Library", "External.Library, Version=1.0.0.0")]),
                ]);

        Assert.That(dependencies, Is.Empty);
    }

    [Test]
    public void Evaluate_CanonicalTypeAndAssemblyContributorsDoNotCollapseSameNames()
    {
        const string CanonicalFirst = "Shared, Version=1.0.0.0|mvid=aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        const string CanonicalSecond = "Shared, Version=1.0.0.0|mvid=bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
        ArchitectureTopology topologyDefinition = new()
        {
            Mode = "partial",
            SubjectKind = "type",
            Scope = new ArchitectureTopologyScope
            {
                Selectors = [new ArchitectureTopologySubjectSelector { Assembly = "Shared" }],
            },
            Nodes = [AssemblyNode("component", "Shared")],
        };
        ArchitectureTopologyEvaluator.ObservedSubject first = TypeSubject(CanonicalFirst);
        ArchitectureTopologyEvaluator.ObservedSubject second = TypeSubject(CanonicalSecond);
        ArchitectureTopologyEvaluator.Result topology = ArchitectureTopologyEvaluator.Evaluate(
            session: null,
            topology: topologyDefinition,
            observedSubjects: [first, second],
            observedDependencies: Array.Empty<ArchitectureTopologyEvaluator.ObservedDependency>());
        ArchitectureContractDocument document = new()
        {
            Name = "metric-canonical-contributors",
            Topology = topologyDefinition,
            Metrics =
            [
                new ArchitectureMetricDefinition
                {
                    Id = "assembly-footprint",
                    Kind = ArchitectureMetricKinds.ComponentFootprintCount,
                    TopologyNode = "component",
                    Unit = "assembly",
                },
                TopologyMetric("type-count", ArchitectureMetricKinds.TopologyTypeCount, "component"),
            ],
        };
        using ArchitectureAnalysisContext context = CreateContext(typeof(ArchitectureMetricMeasurement).Assembly);
        var session = new ArchitectureAnalysisSession(context, document, null, false, null);

        ArchitectureMetricMeasurementOutcome outcome = ArchitectureMetricEvaluator.Evaluate(
            session, document.Metrics, topology);

        Assert.Multiple(() =>
        {
            Assert.That(outcome.Measurements.Single(item => item.Id == "assembly-footprint").Contributors,
                Is.EqualTo(new[] { CanonicalFirst, CanonicalSecond }));
            Assert.That(outcome.Measurements.Single(item => item.Id == "type-count").Contributors,
                Is.EqualTo(new[] { first.Identity, second.Identity }.OrderBy(item => item, StringComparer.Ordinal)));
            Assert.That(outcome.Measurements.All(item => item.Value == 2), Is.True);
        });
    }

    [Test]
    public void Evaluate_EmptyTopologyScopeHonorsAllowEmpty()
    {
        ArchitectureTopology allowedTopology = EmptyTypeTopology(allowEmpty: true);
        ArchitectureContractDocument allowedDocument = new()
        {
            Name = "metric-allowed-empty-topology",
            Topology = allowedTopology,
            Metrics = [TopologyMetric("empty", ArchitectureMetricKinds.TopologyTypeCount, "empty")],
        };
        ArchitectureTopology requiredTopology = EmptyTypeTopology(allowEmpty: false);
        ArchitectureContractDocument requiredDocument = new()
        {
            Name = "metric-required-empty-topology",
            Topology = requiredTopology,
            Metrics = [TopologyMetric("empty", ArchitectureMetricKinds.TopologyTypeCount, "empty")],
        };
        ArchitectureTopology selectedEmptyTopology = EmptySelectedNodeTopology();
        ArchitectureContractDocument selectedEmptyDocument = new()
        {
            Name = "metric-allowed-empty-selected-node",
            Topology = selectedEmptyTopology,
            Metrics = [TopologyMetric("empty", ArchitectureMetricKinds.TopologyTypeCount, "empty")],
        };
        using ArchitectureAnalysisContext allowedContext = CreateContext(typeof(ArchitectureMetricMeasurement).Assembly);
        using ArchitectureAnalysisContext requiredContext = CreateContext(typeof(ArchitectureMetricMeasurement).Assembly);
        using ArchitectureAnalysisContext selectedEmptyContext = CreateContext(typeof(ArchitectureMetricMeasurement).Assembly);
        var allowedSession = new ArchitectureAnalysisSession(allowedContext, allowedDocument, null, false, null);
        var requiredSession = new ArchitectureAnalysisSession(requiredContext, requiredDocument, null, false, null);
        var selectedEmptySession = new ArchitectureAnalysisSession(
            selectedEmptyContext, selectedEmptyDocument, null, false, null);

        ArchitectureMetricMeasurement allowed = ArchitectureMetricEvaluator.Evaluate(allowedSession, allowedDocument.Metrics)
            .Measurements.Single();
        ArchitectureMetricMeasurement required = ArchitectureMetricEvaluator.Evaluate(requiredSession, requiredDocument.Metrics)
            .Measurements.Single();
        ArchitectureMetricMeasurement selectedEmpty = ArchitectureMetricEvaluator.Evaluate(
                selectedEmptySession, selectedEmptyDocument.Metrics)
            .Measurements.Single();

        Assert.Multiple(() =>
        {
            Assert.That(allowed.IsEvaluable, Is.True);
            Assert.That(allowed.Value, Is.Zero);
            Assert.That(allowed.Contributors, Is.Empty);
            Assert.That(selectedEmpty.IsEvaluable, Is.True);
            Assert.That(selectedEmpty.Value, Is.Zero);
            AssertUnassessable(required);
        });
    }

    [Test]
    public void ExternalGroups_UnrelatedProjectFactDoesNotRequireAnOwner()
    {
        Type selected = CreateDynamicType("MetricSelectedProject", "Metric.Selected.Type");
        Type unrelated = CreateDynamicType("MetricUnrelatedProject", "Metric.Unrelated.Type");
        string selectedProject = selected.Assembly.GetName().Name!;
        ArchitectureTopology topologyDefinition = new()
        {
            Mode = "partial",
            SubjectKind = "project",
            Scope = new ArchitectureTopologyScope
            {
                Selectors = [new ArchitectureTopologySubjectSelector { Project = selectedProject }],
            },
            Nodes = [ProjectNode("selected", selectedProject)],
        };
        ArchitectureContractDocument document = new()
        {
            Name = "metric-unrelated-project-fact",
            Topology = topologyDefinition,
        };
        using ArchitectureAnalysisContext context = CreateContext(selected.Assembly, unrelated.Assembly);
        var session = new ArchitectureAnalysisSession(context, document, null, false, null);
        ArchitectureTopologyEvaluator.Projection projection = ArchitectureTopologyEvaluator.Project(session, topologyDefinition);
        var reasons = new List<string>();

        HashSet<string> contributors = ArchitectureMetricEvaluator.ExternalGroups(
            session,
            projection,
            "selected",
            reasons,
            [new ArchitectureExternalDependencyFact(unrelated, "Vendor.Client", "vendor-sdk")]);

        Assert.Multiple(() =>
        {
            Assert.That(contributors, Is.Empty);
            Assert.That(reasons, Is.Empty);
        });
    }

    [Test]
    public void Project_AssemblyTopologyIncludesResolvedAssemblyWithoutLoadableTypes()
    {
        Assembly emptyAssembly = CreateEmptyDynamicAssembly("MetricEmptyAssembly");
        string assemblyName = emptyAssembly.GetName().Name!;
        ArchitectureTopology topology = new()
        {
            Mode = "partial",
            SubjectKind = "assembly",
            Scope = new ArchitectureTopologyScope
            {
                Selectors = [new ArchitectureTopologySubjectSelector { Assembly = assemblyName }],
            },
            Nodes = [AssemblyNode("empty", assemblyName)],
        };
        ArchitectureContractDocument document = new()
        {
            Name = "metric-empty-resolved-assembly",
            Topology = topology,
        };
        using ArchitectureAnalysisContext context = CreateContext(emptyAssembly);
        var session = new ArchitectureAnalysisSession(context, document, null, false, null);

        ArchitectureTopologyEvaluator.Projection projection = ArchitectureTopologyEvaluator.Project(session, topology);

        Assert.Multiple(() =>
        {
            Assert.That(emptyAssembly.GetTypes(), Is.Empty);
            Assert.That(projection.ObservedSubjects.Select(subject => subject.Assembly), Is.EqualTo(new[] { assemblyName }));
            Assert.That(projection.Classifications.Single().Disposition,
                Is.EqualTo(ArchitectureTopologyEvaluator.Disposition.Mapped));
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

    private static Assembly CreateEmptyDynamicAssembly(string assemblyPrefix)
    {
        AssemblyName name = new($"{assemblyPrefix}-{Guid.NewGuid():N}");
        AssemblyBuilder assembly = AssemblyBuilder.DefineDynamicAssembly(name, AssemblyBuilderAccess.Run);
        _ = assembly.DefineDynamicModule(name.Name!);
        return assembly;
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

    private static ArchitectureTopology EmptyTypeTopology(bool allowEmpty) => new()
    {
        Mode = "partial",
        SubjectKind = "type",
        Scope = new ArchitectureTopologyScope
        {
            AllowEmpty = allowEmpty,
            Selectors = [new ArchitectureTopologySubjectSelector { Namespace = "Metric.No.Types" }],
        },
        Nodes = [Node("empty", "Metric.No.Types")],
    };

    private static ArchitectureTopology EmptySelectedNodeTopology() => new()
    {
        Mode = "partial",
        SubjectKind = "type",
        Scope = new ArchitectureTopologyScope
        {
            AllowEmpty = true,
            Selectors = [new ArchitectureTopologySubjectSelector { Namespace = "ArchLinterNet.Core.Model" }],
        },
        Nodes = [Node("empty", "Metric.No.Types")],
    };

    private static ArchitectureTopologyEvaluator.ObservedSubject TypeSubject(string canonicalAssembly) => new(
        ArchitectureTopologyEvaluator.BuildMetricSubjectIdentity(
            "type", "Shared", "Shared", canonicalAssembly, "Metric.Shared.Type"),
        "Shared",
        "Shared",
        "Metric.Shared.Type",
        CanonicalAssemblyIdentity: canonicalAssembly,
        AssemblyReferenceIdentity: "Shared, Version=1.0.0.0");
}
