using System.Reflection;
using ArchLinterNet.Core.Composition;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Families;
using ArchLinterNet.Core.Discovery;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Validation;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed partial class ArchitectureMetricMeasurementTests
{
    private string _temporaryDirectory = null!;
    private string _policyPath = null!;

    [SetUp]
    public void SetUp()
    {
        _temporaryDirectory = Path.Combine(Path.GetTempPath(), $"arch-linter-metrics-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_temporaryDirectory);
        _policyPath = Path.Combine(_temporaryDirectory, "dependencies.arch.yml");
        File.WriteAllText(_policyPath, """
            version: 1
            name: Metric measurement test
            analysis:
              target_assemblies: [ArchLinterNet.Core]
            topology:
              mode: partial
              subject_kind: namespace
              scope:
                selectors:
                  - namespace: ArchLinterNet.Core.Model
              nodes:
                - id: model
                  mappings:
                    - namespace: ArchLinterNet.Core.Model
            metrics:
              - id: model-external-groups
                kind: external_dependency_group_count
                topology_node: model
            contracts: {}
            """);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_temporaryDirectory))
        {
            Directory.Delete(_temporaryDirectory, recursive: true);
        }
    }

    [Test]
    public void Measure_CompleteEmptyExternalGroupScope_ReturnsZeroWithoutFindings()
    {
        using ArchitectureEngine engine = new ArchitectureEngineBuilder().AddArchLinterNetCore().Build();

        ArchitectureMetricMeasurementOutcome outcome = engine.Measure(new ArchitectureMetricMeasurementRequest
        {
            PolicyPath = _policyPath,
        });

        ArchitectureMetricMeasurement measurement = outcome.Measurements.Single();
        Assert.Multiple(() =>
        {
            Assert.That(measurement.Id, Is.EqualTo("model-external-groups"));
            Assert.That(measurement.IsEvaluable, Is.True);
            Assert.That(measurement.Value, Is.Zero);
            Assert.That(measurement.Contributors, Is.Empty);
            Assert.That(outcome.Completion!.State, Is.EqualTo(ArchitectureAssessmentCompletionState.Pass));
            Assert.That(outcome.Applicability!.Findings, Is.Empty);
        });
    }

    [Test]
    public void Measure_UnknownSelection_IsAConfigurationError()
    {
        using ArchitectureEngine engine = new ArchitectureEngineBuilder().AddArchLinterNetCore().Build();

        ArgumentException exception = Assert.Throws<ArgumentException>(() => engine.Measure(
            new ArchitectureMetricMeasurementRequest
            {
                PolicyPath = _policyPath,
                MetricIds = ["does-not-exist"],
            }))!;

        Assert.That(exception.Message, Does.Contain("Unknown metric IDs: does-not-exist."));
    }

    [Test]
    public void Measure_NoDeclaredMetrics_IsACompleteEmptyReport()
    {
        ArchitectureMetricMeasurementOutcome outcome = new(Array.Empty<ArchitectureMetricMeasurement>(), null, null);

        Assert.Multiple(() =>
        {
            Assert.That(outcome.Measurements, Is.Empty);
            Assert.That(outcome.IsComplete, Is.True);
        });
    }

    [Test]
    public void UnassessableCoreMetricEvidence_DoesNotExposeAnUnknownContributorUniverseAsEmpty()
    {
        ArchitectureMetricMeasurement measurement = new(
            "incomplete", ArchitectureMetricKinds.OutgoingComponentCount, "component", null, "component",
            ArchitectureApplicabilityRecordState.Unassessable, null, Array.Empty<string>());
        ArchitectureMetricEvidence evidence = new(
            "incomplete", ArchitectureMetricKinds.OutgoingComponentCount, "component", null, "component",
            null, Array.Empty<string>());

        Assert.Multiple(() =>
        {
            Assert.That(measurement.Value, Is.Null);
            Assert.That(measurement.Contributors, Is.Null);
            Assert.That(measurement.ContributorCount, Is.Null);
            Assert.That(evidence.Value, Is.Null);
            Assert.That(evidence.Contributors, Is.Null);
            Assert.That(evidence.ContributorCount, Is.Null);
        });
    }

    [Test]
    public void PolicyLoad_AcceptsValidDefinitionsAndLeavesLegacyPoliciesUnchanged()
    {
        string metricsPolicy = WritePolicy("""
            version: 1
            name: Valid metrics
            analysis:
              target_assemblies: []
            topology:
              mode: partial
              subject_kind: type
              scope:
                selectors: [{ namespace: App }]
              nodes:
                - id: application
                  mappings: [{ namespace: App }]
            metrics:
              - id: outgoing
                kind: outgoing_component_count
                topology_node: application
              - id: project-footprint
                kind: component_footprint_count
                topology_node: application
                unit: project
              - id: type-count
                kind: topology_type_count
                topology_node: application
            contracts: {}
            """);
        string legacyPolicy = WritePolicy("""
            version: 1
            name: Legacy policy
            analysis:
              target_assemblies: []
            contracts: {}
            """);

        ArchitectureContractDocument metrics = new ArchitecturePolicyDocumentLoader().Load(metricsPolicy);
        ArchitectureContractDocument legacy = new ArchitecturePolicyDocumentLoader().Load(legacyPolicy);

        Assert.Multiple(() =>
        {
            Assert.That(metrics.Metrics.Select(definition => definition.Id), Is.EqualTo(new[]
            {
                "outgoing", "project-footprint", "type-count",
            }));
            Assert.That(legacy.Metrics, Is.Empty);
        });
    }

    [Test]
    public void PolicyLoad_RejectsDuplicateAndUnsupportedMetricDefinitions()
    {
        string duplicatePolicy = WritePolicy("""
            version: 1
            name: Duplicate metrics
            analysis:
              target_assemblies: []
            topology:
              mode: partial
              subject_kind: type
              scope:
                selectors: [{ namespace: App }]
              nodes:
                - id: application
                  mappings: [{ namespace: App }]
            metrics:
              - id: duplicate
                kind: outgoing_component_count
                topology_node: application
              - id: duplicate
                kind: incoming_component_count
                topology_node: application
            contracts: {}
            """);
        string unsupportedPolicy = WritePolicy("""
            version: 1
            name: Unsupported metric definition
            analysis:
              target_assemblies: []
            topology:
              mode: partial
              subject_kind: type
              scope:
                selectors: [{ namespace: App }]
              nodes:
                - id: application
                  mappings: [{ namespace: App }]
            metrics:
              - id: invalid-unit
                kind: outgoing_component_count
                topology_node: application
                unit: project
            contracts: {}
            """);

        InvalidOperationException duplicate = Assert.Throws<InvalidOperationException>(
            () => new ArchitecturePolicyDocumentLoader().Load(duplicatePolicy))!;
        InvalidOperationException unsupported = Assert.Throws<InvalidOperationException>(
            () => new ArchitecturePolicyDocumentLoader().Load(unsupportedPolicy))!;

        Assert.Multiple(() =>
        {
            Assert.That(duplicate.Message, Does.Contain("Duplicate metric id 'duplicate'."));
            Assert.That(unsupported.Message, Does.Contain("does not accept 'unit'."));
        });
    }

    [Test]
    public void Evaluate_ClosedTopologyMetricCatalog_UsesCanonicalContributorSets()
    {
        Assembly coreAssembly = typeof(ArchitectureMetricMeasurement).Assembly;
        ArchitectureMetricDefinition[] definitions =
        [
            TopologyMetric("incoming", ArchitectureMetricKinds.IncomingComponentCount),
            TopologyMetric("outgoing", ArchitectureMetricKinds.OutgoingComponentCount),
            TopologyMetric("external", ArchitectureMetricKinds.ExternalDependencyGroupCount),
            new ArchitectureMetricDefinition
            {
                Id = "project-footprint",
                Kind = ArchitectureMetricKinds.ComponentFootprintCount,
                TopologyNode = "model",
                Unit = "project",
            },
            new ArchitectureMetricDefinition
            {
                Id = "assembly-footprint",
                Kind = ArchitectureMetricKinds.ComponentFootprintCount,
                TopologyNode = "model",
                Unit = "assembly",
            },
            TopologyMetric("type-count", ArchitectureMetricKinds.TopologyTypeCount),
        ];
        ArchitectureContractDocument document = new()
        {
            Name = "metric-facts",
            Topology = TypeTopology(),
            Metrics = definitions.ToList(),
        };
        using ArchitectureAnalysisContext context = new(
            Path.GetTempPath(), [coreAssembly], Array.Empty<string>(), Array.Empty<string>(),
            projectDiscovery: ProjectDiscoveryFor(coreAssembly));
        var session = new ArchitectureAnalysisSession(context, document, null, false, null);

        ArchitectureMetricMeasurementOutcome outcome = ArchitectureMetricEvaluator.Evaluate(session, definitions);

        Assert.Multiple(() =>
        {
            Assert.That(outcome.Measurements.Select(measurement => measurement.Id), Is.EqualTo(new[]
            {
                "assembly-footprint", "external", "incoming", "outgoing", "project-footprint", "type-count",
            }));
            Assert.That(outcome.Measurements.All(measurement => measurement.IsEvaluable), Is.True);
            Assert.That(outcome.Measurements.All(measurement =>
                measurement.Value == measurement.ContributorCount), Is.True);
            Assert.That(outcome.Measurements.All(measurement =>
                measurement.Contributors!.SequenceEqual(measurement.Contributors!.OrderBy(value => value, StringComparer.Ordinal))), Is.True);
            Assert.That(outcome.Measurements.Single(measurement => measurement.Id == "external").Value, Is.Zero);
            Assert.That(outcome.Applicability!.Findings, Is.Empty);
        });
    }

    [Test]
    public void Evaluate_PublicSurfaceMetric_UsesSelectedObservedExports()
    {
        Assembly coreAssembly = typeof(ArchitectureMetricMeasurement).Assembly;
        string assemblyName = coreAssembly.GetName().Name!;
        ArchitectureContractDocument document = new()
        {
            Name = "metric-public-surface",
            Analysis = new ArchitectureAnalysisConfiguration { TargetAssemblies = [assemblyName] },
            Contracts = new ArchitectureContractGroups
            {
                StrictPublicApiSurface =
                [
                    new ArchitecturePublicApiSurfaceContract
                    {
                        Id = "core-public-api",
                        Name = "Core public API",
                        Assemblies = [assemblyName],
                    },
                ],
            },
            Metrics =
            [
                new ArchitectureMetricDefinition
                {
                    Id = "core-public-surface",
                    Kind = ArchitectureMetricKinds.PublicContractSurfaceCount,
                    PublicApiSurface = "core-public-api",
                },
            ],
        };
        using ArchitectureAnalysisContext context = new(
            Path.GetTempPath(), [coreAssembly], Array.Empty<string>(), Array.Empty<string>(),
            projectDiscovery: ProjectDiscoveryFor(coreAssembly));
        var session = new ArchitectureAnalysisSession(context, document, null, false, null);

        ArchitectureMetricMeasurement measurement = ArchitectureMetricEvaluator.Evaluate(session, document.Metrics)
            .Measurements.Single();

        Assert.Multiple(() =>
        {
            Assert.That(measurement.IsEvaluable, Is.True);
            Assert.That(measurement.Value, Is.GreaterThan(0));
            Assert.That(measurement.Value, Is.EqualTo(measurement.ContributorCount));
            string canonicalAssembly = ArchitectureTopologyMetricObserver.ResolveCanonicalAssemblyIdentity(coreAssembly);
            Assert.That(measurement.Contributors!.All(value => value.StartsWith(canonicalAssembly + "|", StringComparison.Ordinal)), Is.True);
        });
    }

    [TestCase("namespace")]
    [TestCase("project")]
    [TestCase("assembly")]
    public void Evaluate_NativeTopologyProjections_KeepEachMetricEvaluable(string subjectKind)
    {
        Assembly coreAssembly = typeof(ArchitectureMetricMeasurement).Assembly;
        string assemblyName = coreAssembly.GetName().Name!;
        ArchitectureTopologySubjectSelector selector = subjectKind switch
        {
            "namespace" => new ArchitectureTopologySubjectSelector { Namespace = "ArchLinterNet.Core" },
            "project" => new ArchitectureTopologySubjectSelector { Project = assemblyName },
            "assembly" => new ArchitectureTopologySubjectSelector { Assembly = assemblyName },
            _ => throw new ArgumentOutOfRangeException(nameof(subjectKind)),
        };
        ArchitectureMetricDefinition[] definitions =
        [
            TopologyMetric("incoming", ArchitectureMetricKinds.IncomingComponentCount, "component"),
            TopologyMetric("outgoing", ArchitectureMetricKinds.OutgoingComponentCount, "component"),
            TopologyMetric("external", ArchitectureMetricKinds.ExternalDependencyGroupCount, "component"),
            new ArchitectureMetricDefinition
            {
                Id = "assembly-footprint",
                Kind = ArchitectureMetricKinds.ComponentFootprintCount,
                TopologyNode = "component",
                Unit = "assembly",
            },
        ];
        ArchitectureContractDocument document = new()
        {
            Name = $"metric-{subjectKind}-projection",
            Topology = new ArchitectureTopology
            {
                Mode = "partial",
                SubjectKind = subjectKind,
                Scope = new ArchitectureTopologyScope { Selectors = [selector] },
                Nodes = [new ArchitectureTopologyNode { Id = "component", Mappings = [selector] }],
            },
            Metrics = definitions.ToList(),
        };
        using ArchitectureAnalysisContext context = new(
            Path.GetTempPath(), [coreAssembly], Array.Empty<string>(), Array.Empty<string>(),
            projectDiscovery: ProjectDiscoveryFor(coreAssembly));
        var session = new ArchitectureAnalysisSession(context, document, null, false, null);

        ArchitectureMetricMeasurementOutcome outcome = ArchitectureMetricEvaluator.Evaluate(session, document.Metrics);

        Assert.Multiple(() =>
        {
            Assert.That(outcome.Measurements.All(measurement => measurement.IsEvaluable), Is.True);
            Assert.That(outcome.Measurements.All(measurement => measurement.Value == measurement.ContributorCount), Is.True);
            Assert.That(outcome.Applicability!.Findings, Is.Empty);
        });
    }

    private static ArchitectureMetricDefinition TopologyMetric(string id, string kind, string topologyNode = "model") => new()
    {
        Id = id,
        Kind = kind,
        TopologyNode = topologyNode,
    };

    private static ArchitectureTopology TypeTopology() => new()
    {
        Mode = "partial",
        SubjectKind = "type",
        Scope = new ArchitectureTopologyScope
        {
            Selectors = [new ArchitectureTopologySubjectSelector { Namespace = "ArchLinterNet.Core" }],
        },
        Nodes =
        [
            new ArchitectureTopologyNode
            {
                Id = "model",
                Mappings = [new ArchitectureTopologySubjectSelector { Namespace = "ArchLinterNet.Core" }],
            },
        ],
    };

    private string WritePolicy(string content)
    {
        string path = Path.Combine(_temporaryDirectory, $"policy-{Guid.NewGuid():N}.yml");
        File.WriteAllText(path, content);
        return path;
    }

    private static ProjectDiscoveryResult ProjectDiscoveryFor(Assembly assembly)
    {
        string assemblyName = assembly.GetName().Name!;
        return new ProjectDiscoveryResult(
            [assemblyName], Array.Empty<string>(), Array.Empty<string>(),
            Array.Empty<ArchitectureProjectDiscoveryDiagnostic>())
        {
            DiscoveredProjects = [new ArchitectureDiscoveredProject("src/Core/Core.csproj", assemblyName, ["net10.0"])],
            ResolvedAssemblyPathsByNormalizedProjectPath = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["src/Core/Core.csproj"] = assembly.Location,
            },
        };
    }
}
