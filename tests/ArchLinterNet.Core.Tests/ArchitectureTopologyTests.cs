using ArchLinterNet.Core.Contracts;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class ArchitectureTopologyTests
{
    private string _temporaryDirectory = null!;

    [SetUp]
    public void SetUp()
    {
        _temporaryDirectory = Path.Combine(Path.GetTempPath(), $"arch-linter-topology-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_temporaryDirectory);
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
    public void Load_WithoutTopology_PreservesExistingPolicyCompatibility()
    {
        ArchitectureContractDocument document = LoadPolicy();

        Assert.Multiple(() =>
        {
            Assert.That(document.Topology, Is.Null);
            Assert.That(document.Layers, Contains.Key("core"));
            Assert.That(document.Analysis.TargetAssemblies, Is.Empty);
            Assert.That(document.Contracts.Strict, Is.Empty);
        });
    }

    [TestCase("partial", false)]
    [TestCase("exhaustive", true)]
    public void Load_ValidTopologyModes_BindsModeScopeAndCompletenessSettings(string mode, bool allowEmpty)
    {
        ArchitectureContractDocument document = LoadPolicy($$"""
            topology:
              mode: {{mode}}
              subject_kind: namespace
              scope:
                allow_empty: {{allowEmpty.ToString().ToLowerInvariant()}}
                selectors:
                  - namespace: App.*
              nodes:
                - id: core
                  mappings:
                    - namespace: App.Core
              allowed_edges: []
              out_of_scope: []
              stale_declarations: true
            """);

        ArchitectureTopology topology = document.Topology!;

        Assert.Multiple(() =>
        {
            Assert.That(topology.Mode, Is.EqualTo(mode));
            Assert.That(topology.SubjectKind, Is.EqualTo("namespace"));
            Assert.That(topology.Scope.AllowEmpty, Is.EqualTo(allowEmpty));
            Assert.That(topology.Scope.Selectors, Has.Count.EqualTo(1));
            Assert.That(topology.Scope.Selectors[0].Namespace, Is.EqualTo("App.*"));
            Assert.That(topology.Nodes.Single().Id, Is.EqualTo("core"));
            Assert.That(topology.Nodes.Single().Mappings.Single().Namespace, Is.EqualTo("App.Core"));
            Assert.That(topology.StaleDeclarations, Is.True);
        });
    }

    [Test]
    public void Load_ValidTopology_BindsAllSubjectKindsAndSelectorForms()
    {
        ArchitectureContractDocument document = LoadPolicy($"""
            topology:
              mode: exhaustive
              subject_kind: assembly
              scope:
                selectors:
                  - layer: core
                  - namespace: App.Feature.*
                    namespace_suffix: Generated
                  - project: App.Feature
                  - assembly: App.Feature
                  - context:
                      role: DomainLayer
                      metadata:
                        domain: Sales
                        enabled: true
                      when: subject.role == "DomainLayer"
              nodes:
                - id: mapped
                  mappings:
                    - layer: core
                    - namespace: App.Feature.*
                      namespace_suffix: Generated
                    - project: App.Feature
                    - assembly: App.Feature
                    - context:
                        role: DomainLayer
                        metadata:
                          domain: Sales
                          enabled: true
                        when: subject.role == "DomainLayer"
                - id: generated
                  mappings:
                    - namespace: App.Generated
              allowed_edges:
                - from: mapped
                  to: generated
              out_of_scope:
                - id: generated-code
                  selector:
                    namespace: App.Generated.Code
                  reason: Generated code is reviewed separately.
              stale_declarations: false
            """);

        ArchitectureTopology topology = document.Topology!;
        ArchitectureTopologySubjectSelector contextSelector = topology.Nodes[0].Mappings[4];

        Assert.Multiple(() =>
        {
            Assert.That(topology.SubjectKind, Is.EqualTo("assembly"));
            Assert.That(topology.Scope.Selectors, Has.Count.EqualTo(5));
            Assert.That(topology.Scope.Selectors[0].Layer, Is.EqualTo("core"));
            Assert.That(topology.Scope.Selectors[1].NamespaceSuffix, Is.EqualTo("Generated"));
            Assert.That(topology.Scope.Selectors[2].Project, Is.EqualTo("App.Feature"));
            Assert.That(topology.Scope.Selectors[3].Assembly, Is.EqualTo("App.Feature"));
            Assert.That(contextSelector.Context!.Role, Is.EqualTo("DomainLayer"));
            Assert.That(contextSelector.Context.Metadata["domain"], Is.EqualTo("Sales"));
            Assert.That(contextSelector.Context.Metadata["enabled"], Is.EqualTo(true));
            Assert.That(contextSelector.Context.When, Is.EqualTo("subject.role == \"DomainLayer\""));
            Assert.That(contextSelector.Context.CompiledWhen, Is.Not.Null);
            Assert.That(topology.AllowedEdges.Single().From, Is.EqualTo("mapped"));
            Assert.That(topology.AllowedEdges.Single().To, Is.EqualTo("generated"));
            Assert.That(topology.OutOfScope.Single().Id, Is.EqualTo("generated-code"));
            Assert.That(topology.OutOfScope.Single().Reason, Is.EqualTo("Generated code is reviewed separately."));
        });
    }

    [Test]
    public void Load_TopologyDefaultsOptionalCompletenessSettingsToFalse()
    {
        ArchitectureContractDocument document = LoadPolicy("""
            topology:
              subject_kind: project
              scope:
                selectors:
                  - project: App.Core
              nodes:
                - id: core
                  mappings:
                    - project: App.Core
            """);

        Assert.Multiple(() =>
        {
            Assert.That(document.Topology!.Mode, Is.EqualTo("partial"));
            Assert.That(document.Topology.Scope.AllowEmpty, Is.False);
            Assert.That(document.Topology.StaleDeclarations, Is.False);
            Assert.That(document.Topology.AllowedEdges, Is.Empty);
            Assert.That(document.Topology.OutOfScope, Is.Empty);
        });
    }

    [TestCase("mode: invalid", "Topology mode must be either")]
    [TestCase("subject_kind: member", "subject_kind must be one")]
    public void Load_InvalidTopologyEnumValues_ThrowsDeterministicValidationError(string setting, string message)
    {
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => LoadPolicy($"""
            topology:
              {setting}
              scope:
                selectors:
                  - namespace: App.*
              nodes:
                - id: core
                  mappings:
                    - namespace: App.Core
            """))!;

        Assert.That(exception.Message, Does.Contain(message));
    }

    [Test]
    public void Load_TopologyScopeMustDeclareAtLeastOneSelector()
    {
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => LoadPolicy("""
            topology:
              subject_kind: namespace
              scope:
                selectors: []
              nodes:
                - id: core
                  mappings:
                    - namespace: App.Core
            """))!;

        Assert.That(exception.Message, Does.Contain("scope must declare at least one bounded selector"));
    }

    [Test]
    public void Load_TopologyRequiresAtLeastOneNodeAndOneMapping()
    {
        InvalidOperationException noNodes = Assert.Throws<InvalidOperationException>(() => LoadPolicy("""
            topology:
              subject_kind: namespace
              scope:
                selectors:
                  - namespace: App.*
              nodes: []
            """))!;

        InvalidOperationException noMappings = Assert.Throws<InvalidOperationException>(() => LoadPolicy("""
            topology:
              subject_kind: namespace
              scope:
                selectors:
                  - namespace: App.*
              nodes:
                - id: core
                  mappings: []
            """))!;

        Assert.Multiple(() =>
        {
            Assert.That(noNodes.Message, Does.Contain("must declare at least one node"));
            Assert.That(noMappings.Message, Does.Contain("must declare at least one mapping selector"));
        });
    }

    [TestCase("- {}", "must declare exactly one of layer, namespace, project, assembly, or context")]
    [TestCase("- { namespace: App.Core, project: App.Core }", "must declare exactly one of layer, namespace, project, assembly, or context")]
    [TestCase("- { project: App.Core, namespace_suffix: Generated }", "namespace_suffix requires namespace")]
    [TestCase("- namespace: App.**", "Recursive wildcard")]
    [TestCase("- context:\n                      metadata:\n                        domain: Sales", "context must declare a non-empty role")]
    [TestCase("- context:\n                      role: DomainLayer\n                      metadata: null", "context metadata must be an object")]
    public void Load_InvalidTopologySelectors_ThrowsDeterministicValidationError(string selector, string message)
    {
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => LoadPolicy($"""
            topology:
              subject_kind: namespace
              scope:
                selectors:
                  {selector}
              nodes:
                - id: core
                  mappings:
                    - namespace: App.Core
            """))!;

        Assert.That(exception.Message, Does.Contain(message));
    }

    [Test]
    public void Load_TopologyContextMetadataRequiresSupportedNonEmptyValues()
    {
        InvalidOperationException emptyString = Assert.Throws<InvalidOperationException>(() => LoadPolicy("""
            topology:
              subject_kind: namespace
              scope:
                selectors:
                  - context:
                      role: DomainLayer
                      metadata:
                        domain: ""
              nodes:
                - id: core
                  mappings:
                    - namespace: App.Core
            """))!;

        InvalidOperationException unsupportedValue = Assert.Throws<InvalidOperationException>(() => LoadPolicy("""
            topology:
              subject_kind: namespace
              scope:
                selectors:
                  - context:
                      role: DomainLayer
                      metadata:
                        values:
                          - Sales
              nodes:
                - id: core
                  mappings:
                    - namespace: App.Core
            """))!;

        Assert.Multiple(() =>
        {
            Assert.That(emptyString.Message, Does.Contain("metadata key 'domain' must not be an empty string"));
            Assert.That(unsupportedValue.Message, Does.Contain("must be a string, boolean, or finite numeric scalar"));
        });
    }

    [Test]
    public void Load_TopologyRejectsUnknownLayerAndNodeReferences()
    {
        InvalidOperationException unknownLayer = Assert.Throws<InvalidOperationException>(() => LoadPolicy("""
            topology:
              subject_kind: namespace
              scope:
                selectors:
                  - layer: missing
              nodes:
                - id: core
                  mappings:
                    - namespace: App.Core
            """))!;

        InvalidOperationException unknownNode = Assert.Throws<InvalidOperationException>(() => LoadPolicy("""
            topology:
              subject_kind: namespace
              scope:
                selectors:
                  - namespace: App.*
              nodes:
                - id: core
                  mappings:
                    - namespace: App.Core
              allowed_edges:
                - from: core
                  to: missing
            """))!;

        Assert.Multiple(() =>
        {
            Assert.That(unknownLayer.Message, Does.Contain("references undeclared layer 'missing'"));
            Assert.That(unknownNode.Message, Does.Contain("references an undeclared node"));
        });
    }

    [Test]
    public void Load_TopologyRejectsDuplicateNodeIdsAndMappingSelectors()
    {
        InvalidOperationException duplicateNode = Assert.Throws<InvalidOperationException>(() => LoadPolicy("""
            topology:
              subject_kind: namespace
              scope:
                selectors:
                  - namespace: App.*
              nodes:
                - id: core
                  mappings:
                    - namespace: App.Core
                - id: core
                  mappings:
                    - namespace: App.Other
            """))!;

        InvalidOperationException duplicateMapping = Assert.Throws<InvalidOperationException>(() => LoadPolicy("""
            topology:
              subject_kind: namespace
              scope:
                selectors:
                  - namespace: App.*
              nodes:
                - id: core
                  mappings:
                    - namespace: App.Core
                    - namespace: App.Core
            """))!;

        InvalidOperationException crossNodeMapping = Assert.Throws<InvalidOperationException>(() => LoadPolicy("""
            topology:
              subject_kind: namespace
              scope:
                selectors:
                  - namespace: App.*
              nodes:
                - id: core
                  mappings:
                    - context:
                        role: DomainLayer
                        metadata:
                          b: Sales
                          a: Shared
                - id: duplicate
                  mappings:
                    - context:
                        role: DomainLayer
                        metadata:
                          a: Shared
                          b: Sales
            """))!;

        Assert.Multiple(() =>
        {
            Assert.That(duplicateNode.Message, Does.Contain("duplicate node id 'core'"));
            Assert.That(duplicateMapping.Message, Does.Contain("declares duplicate mapping selector"));
            Assert.That(crossNodeMapping.Message, Does.Contain("same mapping selector").And.Contain("unambiguously ambiguous"));
        });
    }

    [Test]
    public void Load_TopologyRejectsDuplicateDirectedEdgesAndOutOfScopeIds()
    {
        InvalidOperationException duplicateEdge = Assert.Throws<InvalidOperationException>(() => LoadPolicy("""
            topology:
              subject_kind: namespace
              scope:
                selectors:
                  - namespace: App.*
              nodes:
                - id: core
                  mappings:
                    - namespace: App.Core
                - id: api
                  mappings:
                    - namespace: App.Api
              allowed_edges:
                - from: core
                  to: api
                - from: core
                  to: api
            """))!;

        InvalidOperationException duplicateOutOfScope = Assert.Throws<InvalidOperationException>(() => LoadPolicy("""
            topology:
              subject_kind: namespace
              scope:
                selectors:
                  - namespace: App.*
              nodes:
                - id: core
                  mappings:
                    - namespace: App.Core
              out_of_scope:
                - id: generated
                  selector:
                    namespace: App.Generated
                  reason: Generated code.
                - id: generated
                  selector:
                    namespace: App.Generated.Tests
                  reason: Generated tests.
            """))!;

        Assert.Multiple(() =>
        {
            Assert.That(duplicateEdge.Message, Does.Contain("duplicate allowed edge 'core->api'"));
            Assert.That(duplicateOutOfScope.Message, Does.Contain("duplicate out_of_scope id 'generated'"));
        });
    }

    [Test]
    public void Load_TopologyOutOfScopeRequiresStableIdReasonAndSelector()
    {
        InvalidOperationException missingReason = Assert.Throws<InvalidOperationException>(() => LoadPolicy("""
            topology:
              subject_kind: namespace
              scope:
                selectors:
                  - namespace: App.*
              nodes:
                - id: core
                  mappings:
                    - namespace: App.Core
              out_of_scope:
                - id: generated
                  selector:
                    namespace: App.Generated
                  reason: ""
            """))!;

        InvalidOperationException missingSelector = Assert.Throws<InvalidOperationException>(() => LoadPolicy("""
            topology:
              subject_kind: namespace
              scope:
                selectors:
                  - namespace: App.*
              nodes:
                - id: core
                  mappings:
                    - namespace: App.Core
              out_of_scope:
                - id: generated
                  reason: Generated code.
            """))!;

        Assert.Multiple(() =>
        {
            Assert.That(missingReason.Message, Does.Contain("must declare a non-empty reason"));
            Assert.That(missingSelector.Message, Does.Contain("must declare exactly one of layer, namespace, project, assembly, or context"));
        });
    }

    [Test]
    public void Load_TopologyRawShapeRejectsUnknownPropertiesBeforeDeserialization()
    {
        InvalidOperationException unknownTopologyProperty = Assert.Throws<InvalidOperationException>(() => LoadPolicy("""
            topology:
              subject_kind: namespace
              selector:
                namespace: App.*
              scope:
                selectors:
                  - namespace: App.*
              nodes:
                - id: core
                  mappings:
                    - namespace: App.Core
            """))!;

        InvalidOperationException unknownContextProperty = Assert.Throws<InvalidOperationException>(() => LoadPolicy("""
            topology:
              subject_kind: namespace
              scope:
                selectors:
                  - context:
                      role: DomainLayer
                      metdata:
                        domain: Sales
              nodes:
                - id: core
                  mappings:
                    - namespace: App.Core
            """))!;

        Assert.Multiple(() =>
        {
            Assert.That(unknownTopologyProperty.Message, Does.Contain("topology contains unknown property 'selector'"));
            Assert.That(unknownContextProperty.Message, Does.Contain("contains unknown property 'metdata'"));
        });
    }

    private ArchitectureContractDocument LoadPolicy(string topology = "")
    {
        string path = Path.Combine(_temporaryDirectory, "dependencies.arch.yml");
        File.WriteAllText(path, $$"""
            version: 1
            name: Topology tests
            layers:
              core:
                namespace: App.Core
            analysis:
              target_assemblies: []
            contracts:
              strict: []
              audit: []
              strict_layers: []
              audit_layers: []
              strict_allow_only: []
              audit_allow_only: []
              strict_cycles: []
              audit_cycles: []
              strict_method_body: []
              audit_method_body: []
              strict_asmdef: []
              audit_asmdef: []
              strict_independence: []
              audit_independence: []
            {{topology}}
            """);

        return new ArchitecturePolicyDocumentLoader().Load(path);
    }
}
