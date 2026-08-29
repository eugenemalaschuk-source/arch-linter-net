using ArchLinterNet.Core.Contracts;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class TopologyPolicyDocumentValidationTests
{
    [Test]
    public void Load_ValidExhaustiveTopology_RetainsNativeDeclarations()
    {
        ArchitectureContractDocument document = Load("""
            version: 1
            name: Native topology
            layers:
              application: { namespace: Sample.Application }
              domain: { namespace: Sample.Domain }
            topology:
              mode: exhaustive
              subject_kind: type
              scope:
                allow_empty: false
                selectors: [{ layer: application }, { layer: domain }]
              nodes:
                - id: application
                  mappings: [{ layer: application }]
                - id: domain
                  mappings:
                    - namespace: Sample.Domain
              allowed_edges: [{ from: application, to: domain }]
              out_of_scope:
                - id: generated
                  selector:
                    context:
                      role: Generated
                      metadata: { origin: source-generator }
                  reason: Generated types are reviewed separately.
              stale_declarations: true
            """);

        ArchitectureTopology topology = document.Topology!;
        Assert.Multiple(() =>
        {
            Assert.That(topology, Is.Not.Null);
            Assert.That(topology.Mode, Is.EqualTo("exhaustive"));
            Assert.That(topology.SubjectKind, Is.EqualTo("type"));
            Assert.That(topology.Scope.AllowEmpty, Is.False);
            Assert.That(topology.Scope.Selectors.Select(selector => selector.Layer), Is.EqualTo(new[] { "application", "domain" }));
            Assert.That(topology.Nodes.Select(node => node.Id), Is.EqualTo(new[] { "application", "domain" }));
            Assert.That(topology.AllowedEdges.Single().From, Is.EqualTo("application"));
            Assert.That(topology.OutOfScope.Single().Selector.Context!.Role, Is.EqualTo("Generated"));
            Assert.That(topology.StaleDeclarations, Is.True);
        });
    }

    [Test]
    public void Load_PolicyWithoutTopology_RemainsCompatible()
    {
        ArchitectureContractDocument document = Load("""
            version: 1
            name: Existing policy
            layers:
              domain: { namespace: Sample.Domain }
            """);

        Assert.That(document.Topology, Is.Null);
    }

    [TestCase("""
        topology:
          mode: complete
          subject_kind: type
          scope: { selectors: [{ namespace: Sample }] }
          nodes: [{ id: sample, mappings: [{ namespace: Sample }] }]
        """, "Topology mode")]
    [TestCase("""
        topology:
          mode: exhaustive
          subject_kind: type
          scope: { selectors: [] }
          nodes: [{ id: sample, mappings: [{ namespace: Sample }] }]
        """, "Topology scope")]
    [TestCase("""
        topology:
          mode: partial
          subject_kind: type
          scope: { selectors: [{ namespace: Sample }] }
          nodes:
            - id: sample
              mappings: [{ namespace: Sample }]
            - id: sample
              mappings: [{ namespace: Sample.Other }]
        """, "duplicate node id")]
    [TestCase("""
        topology:
          mode: partial
          subject_kind: type
          scope: { selectors: [{ namespace: Sample }] }
          nodes:
            - id: first
              mappings: [{ namespace: Sample }]
            - id: second
              mappings: [{ namespace: Sample }]
        """, "unambiguously ambiguous")]
    [TestCase("""
        topology:
          mode: partial
          subject_kind: type
          scope: { selectors: [{ namespace: Sample }] }
          nodes: [{ id: sample, mappings: [{ namespace: Sample }] }]
          allowed_edges: [{ from: sample, to: missing }]
        """, "undeclared node")]
    [TestCase("""
        topology:
          mode: partial
          subject_kind: type
          scope: { selectors: [{ namespace: Sample, assembly: Sample }] }
          nodes: [{ id: sample, mappings: [{ namespace: Sample }] }]
        """, "exactly one")]
    [TestCase("""
        topology:
          mode: partial
          subject_kind: type
          scope: { selectors: [{ namespace: Sample }] }
          nodes: [{ id: sample, mappings: [{ namespace: Sample }] }]
          out_of_scope:
            - id: generated
              selector: { namespace: Sample.Generated }
        """, "non-empty reason")]
    public void Load_InvalidTopology_FailsWithActionableDiagnostic(string topology, string expectedMessage)
    {
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => Load($"""
            version: 1
            name: Invalid topology
            {topology}
            """))!;

        Assert.That(exception.Message, Does.Contain(expectedMessage));
    }

    [Test]
    public void Load_TopologySelectorUnknownProperty_FailsBeforeItCanBeIgnored()
    {
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => Load("""
            version: 1
            name: Invalid topology key
            topology:
              mode: partial
              subject_kind: type
              scope:
                selectors: [{ namespace: Sample, namespaces: Sample.Other }]
              nodes: [{ id: sample, mappings: [{ namespace: Sample }] }]
            """))!;

        Assert.That(exception.Message, Does.Contain("unknown property 'namespaces'"));
    }

    [Test]
    public void Load_TopologyContextWhen_UsesTheExistingCelSelectorCompiler()
    {
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => Load("""
            version: 1
            name: Invalid topology CEL
            topology:
              mode: partial
              subject_kind: type
              scope:
                selectors:
                  - context:
                      role: Domain
                      when: subject.unknown == true
              nodes:
                - id: domain
                  mappings:
                    - context: { role: Domain }
            """))!;

        Assert.That(exception.Message, Does.Contain("Topology selector 'context.when' expression failed to compile"));
    }

    private static ArchitectureContractDocument Load(string yaml)
    {
        string directory = Path.Combine(Path.GetTempPath(), $"arch-linter-topology-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "policy.yml");
        try
        {
            File.WriteAllText(path, yaml);
            return new ArchitecturePolicyDocumentLoader().Load(path);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
