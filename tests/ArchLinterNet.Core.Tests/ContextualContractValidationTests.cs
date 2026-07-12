using ArchLinterNet.Core.Contracts;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

// Regression coverage for two code-review findings:
// 1. A contextual contract's `source` selector silently accepted 'not-equal-to-source'
//    ('!{source.metadata.<key>}'), which ArchitectureContextSelectorMatcher always resolves as a
//    non-match when there is no other source to compare against - turning a strict contract into a
//    false-negative no-op instead of a load-time error.
// 2. The production loading path never runs the public JSON schema and every contextual selector
//    field has a YAML-safe empty-value default (Role: "", Forbidden/Allowed: new List<>()), so a
//    typo'd key (e.g. "forbiden") or an omitted `source`/`forbidden`/`allowed` deserialized cleanly
//    into a contract whose checker matched zero source types or zero target selectors - a strict
//    contract silently reporting zero violations instead of failing to load.
// See openspec/changes/add-contextual-dependency-contracts/design.md Decision 2/3 and
// src/ArchLinterNet.Core/Contracts/Validators/ContextualContractValidator.cs.
[TestFixture]
public sealed class ContextualContractValidationTests
{
    private string _tempDir = null!;

    [SetUp]
    public void SetUp()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"arch-linter-context-source-selector-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, true);
        }
    }

    private string WritePolicy(string yaml)
    {
        string path = Path.Combine(_tempDir, "dependencies.arch.yml");
        File.WriteAllText(path, yaml);
        return path;
    }

    private static string AssemblyName => typeof(ContextualContractValidationTests).Assembly.GetName().Name!;

    [Test]
    public void ContextDependency_NotEqualToSourceOnSourceSelector_ThrowsActionableError()
    {
        string policyPath = WritePolicy($$"""
            version: 1
            name: Test
            analysis:
              target_assemblies: [{{AssemblyName}}]
            contracts:
              strict_context_dependencies:
                - name: sales-no-cross-domain
                  source:
                    role: DomainLayer
                    metadata:
                      domain: "!{source.metadata.domain}"
                  forbidden:
                    - role: DomainLayer
                  reason: Invalid contextual dependency contract.
            """);

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() =>
            new ArchitecturePolicyDocumentLoader().Load(policyPath))!;

        Assert.Multiple(() =>
        {
            Assert.That(ex.Message, Does.Contain("sales-no-cross-domain"));
            Assert.That(ex.Message, Does.Contain("'source' selector"));
            Assert.That(ex.Message, Does.Contain("domain"));
        });
    }

    [Test]
    public void ContextAllowOnly_NotEqualToSourceOnSourceSelector_ThrowsActionableError()
    {
        string policyPath = WritePolicy($$"""
            version: 1
            name: Test
            analysis:
              target_assemblies: [{{AssemblyName}}]
            contracts:
              strict_context_allow_only:
                - name: sales-allow-only
                  source:
                    role: DomainLayer
                    metadata:
                      domain: "!{source.metadata.domain}"
                  allowed:
                    - role: SharedKernel
                  reason: Invalid contextual allow-only contract.
            """);

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() =>
            new ArchitecturePolicyDocumentLoader().Load(policyPath))!;

        Assert.Multiple(() =>
        {
            Assert.That(ex.Message, Does.Contain("sales-allow-only"));
            Assert.That(ex.Message, Does.Contain("'source' selector"));
        });
    }

    [Test]
    public void ContextDependency_NotEqualToSourceOnForbiddenSelector_LoadsSuccessfully()
    {
        // not-equal-to-source is exactly the intended operator on forbidden/allowed/exclude - only
        // the contract's own `source` selector is rejected.
        string policyPath = WritePolicy($$"""
            version: 1
            name: Test
            analysis:
              target_assemblies: [{{AssemblyName}}]
            contracts:
              strict_context_dependencies:
                - name: sales-no-cross-domain
                  source:
                    role: DomainLayer
                    metadata:
                      domain: "*"
                  forbidden:
                    - role: DomainLayer
                      metadata:
                        domain: "!{source.metadata.domain}"
                  reason: Valid contextual dependency contract.
            """);

        Assert.DoesNotThrow(() => new ArchitecturePolicyDocumentLoader().Load(policyPath));
    }

    [Test]
    public void ContextDependency_MissingSourceRole_ThrowsActionableError()
    {
        string policyPath = WritePolicy($$"""
            version: 1
            name: Test
            analysis:
              target_assemblies: [{{AssemblyName}}]
            contracts:
              strict_context_dependencies:
                - name: domain-isolation
                  source:
                    metadata:
                      domain: Sales
                  forbidden:
                    - role: DomainLayer
                  reason: Missing source role.
            """);

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() =>
            new ArchitecturePolicyDocumentLoader().Load(policyPath))!;

        Assert.Multiple(() =>
        {
            Assert.That(ex.Message, Does.Contain("domain-isolation"));
            Assert.That(ex.Message, Does.Contain("non-empty 'source.role'"));
        });
    }

    [Test]
    public void ContextDependency_TypoedForbiddenKeyLeavesListEmpty_ThrowsActionableError()
    {
        // The exact failure mode from the code-review report: "forbiden" (typo) is silently dropped
        // by IgnoreUnmatchedProperties(), leaving Forbidden as its empty-list default. Without this
        // validator the contract would load, match zero forbidden selectors, and a strict contract
        // would silently report zero violations.
        string policyPath = WritePolicy($$"""
            version: 1
            name: Test
            analysis:
              target_assemblies: [{{AssemblyName}}]
            contracts:
              strict_context_dependencies:
                - name: domain-isolation
                  source:
                    role: DomainLayer
                  forbiden:
                    - role: DomainLayer
                  reason: Typo'd forbidden key.
            """);

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() =>
            new ArchitecturePolicyDocumentLoader().Load(policyPath))!;

        Assert.Multiple(() =>
        {
            Assert.That(ex.Message, Does.Contain("domain-isolation"));
            Assert.That(ex.Message, Does.Contain("at least one 'forbidden' selector"));
        });
    }

    [Test]
    public void ContextAllowOnly_EmptyAllowedList_ThrowsActionableError()
    {
        string policyPath = WritePolicy($$"""
            version: 1
            name: Test
            analysis:
              target_assemblies: [{{AssemblyName}}]
            contracts:
              strict_context_allow_only:
                - name: sales-allow-only
                  source:
                    role: DomainLayer
                  allowed: []
                  reason: Empty allowed list.
            """);

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() =>
            new ArchitecturePolicyDocumentLoader().Load(policyPath))!;

        Assert.That(ex.Message, Does.Contain("at least one 'allowed' selector"));
    }

    [Test]
    public void ContextDependency_ForbiddenSelectorMissingRole_ThrowsActionableError()
    {
        string policyPath = WritePolicy($$"""
            version: 1
            name: Test
            analysis:
              target_assemblies: [{{AssemblyName}}]
            contracts:
              strict_context_dependencies:
                - name: domain-isolation
                  source:
                    role: DomainLayer
                  forbidden:
                    - metadata:
                        domain: Inventory
                  reason: Forbidden selector missing role.
            """);

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() =>
            new ArchitecturePolicyDocumentLoader().Load(policyPath))!;

        Assert.Multiple(() =>
        {
            Assert.That(ex.Message, Does.Contain("domain-isolation"));
            Assert.That(ex.Message, Does.Contain("'forbidden' selector with no non-empty 'role'"));
        });
    }

    [Test]
    public void ContextDependency_ExcludeSelectorMissingRole_ThrowsActionableError()
    {
        string policyPath = WritePolicy($$"""
            version: 1
            name: Test
            analysis:
              target_assemblies: [{{AssemblyName}}]
            contracts:
              strict_context_dependencies:
                - name: domain-isolation
                  source:
                    role: DomainLayer
                  forbidden:
                    - role: DomainLayer
                  exclude:
                    - metadata:
                        domain: SharedKernel
                  reason: Exclude selector missing role.
            """);

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() =>
            new ArchitecturePolicyDocumentLoader().Load(policyPath))!;

        Assert.That(ex.Message, Does.Contain("'exclude' selector with no non-empty 'role'"));
    }

    [Test]
    public void ContextAllowOnly_AllowedSelectorMissingRole_ThrowsActionableError()
    {
        string policyPath = WritePolicy($$"""
            version: 1
            name: Test
            analysis:
              target_assemblies: [{{AssemblyName}}]
            contracts:
              strict_context_allow_only:
                - name: sales-allow-only
                  source:
                    role: DomainLayer
                  allowed:
                    - metadata:
                        domain: Sales
                  reason: Allowed selector missing role.
            """);

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() =>
            new ArchitecturePolicyDocumentLoader().Load(policyPath))!;

        Assert.That(ex.Message, Does.Contain("'allowed' selector with no non-empty 'role'"));
    }

    [Test]
    public void ContextAllowOnly_TypoedMetadataKeyOnAllowedSelector_ThrowsActionableError()
    {
        // The exact failure mode from the code-review report: "metdata" (typo) is silently dropped
        // by IgnoreUnmatchedProperties(), leaving the allowed selector's Metadata at its empty
        // default - a structurally valid role-only shape indistinguishable from an intentional one
        // after deserialization. Role-only silently broadens the allowed selector to match any
        // DomainLayer type regardless of domain, turning a metadata-scoped allow-list into a
        // false-negative that admits cross-context references. Only a raw-YAML pass (before
        // deserialization discards the unknown key) can catch this.
        string policyPath = WritePolicy($$"""
            version: 1
            name: Test
            analysis:
              target_assemblies: [{{AssemblyName}}]
            contracts:
              strict_context_allow_only:
                - name: sales-allow-only
                  source:
                    role: DomainLayer
                    metadata:
                      domain: Sales
                  allowed:
                    - role: DomainLayer
                      metdata:
                        domain: Sales
                  reason: Typo'd metadata key on allowed selector.
            """);

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() =>
            new ArchitecturePolicyDocumentLoader().Load(policyPath))!;

        Assert.Multiple(() =>
        {
            Assert.That(ex.Message, Does.Contain("sales-allow-only"));
            Assert.That(ex.Message, Does.Contain("unknown property 'metdata'"));
            Assert.That(ex.Message, Does.Contain("'allowed' selector"));
        });
    }

    [Test]
    public void ContextDependency_UnknownPropertyOnSourceSelector_ThrowsActionableError()
    {
        string policyPath = WritePolicy($$"""
            version: 1
            name: Test
            analysis:
              target_assemblies: [{{AssemblyName}}]
            contracts:
              strict_context_dependencies:
                - name: domain-isolation
                  source:
                    role: DomainLayer
                    metdata:
                      domain: Sales
                  forbidden:
                    - role: DomainLayer
                  reason: Typo'd metadata key on source selector.
            """);

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() =>
            new ArchitecturePolicyDocumentLoader().Load(policyPath))!;

        Assert.Multiple(() =>
        {
            Assert.That(ex.Message, Does.Contain("unknown property 'metdata'"));
            Assert.That(ex.Message, Does.Contain("'source' selector"));
        });
    }

    [Test]
    public void ContextDependency_UnknownPropertyOnExcludeSelector_ThrowsActionableError()
    {
        string policyPath = WritePolicy($$"""
            version: 1
            name: Test
            analysis:
              target_assemblies: [{{AssemblyName}}]
            contracts:
              strict_context_dependencies:
                - name: domain-isolation
                  source:
                    role: DomainLayer
                  forbidden:
                    - role: DomainLayer
                  exclude:
                    - role: SharedKernel
                      unexpected: true
                  reason: Unknown property on exclude selector.
            """);

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() =>
            new ArchitecturePolicyDocumentLoader().Load(policyPath))!;

        Assert.Multiple(() =>
        {
            Assert.That(ex.Message, Does.Contain("unknown property 'unexpected'"));
            Assert.That(ex.Message, Does.Contain("'exclude' selector"));
        });
    }

    [Test]
    public void ContextDependency_WellFormedContract_LoadsSuccessfully()
    {
        string policyPath = WritePolicy($$"""
            version: 1
            name: Test
            analysis:
              target_assemblies: [{{AssemblyName}}]
            contracts:
              strict_context_dependencies:
                - name: domain-isolation
                  source:
                    role: DomainLayer
                    metadata:
                      domain: Sales
                  forbidden:
                    - role: DomainLayer
                      metadata:
                        domain: Inventory
                  exclude:
                    - role: SharedKernel
                  reason: Well-formed contract.
            """);

        Assert.DoesNotThrow(() => new ArchitecturePolicyDocumentLoader().Load(policyPath));
    }

    [Test]
    public void PortBoundary_WellFormedContract_LoadsSuccessfully()
    {
        string policyPath = WritePolicy($$"""
            version: 1
            name: Test
            analysis:
              target_assemblies: [{{AssemblyName}}]
            contracts:
              strict_port_boundaries:
                - name: port-boundary
                  source: { role: ApplicationLayer }
                  target_context: { metadata: { domain: Catalog } }
                  allowed_seams: [{ role: Port }]
                  forbidden: [{ role: DomainLayer }]
                  exclude: [{ role: SharedKernel }]
                  adapter_bindings:
                    - adapter: { role: Adapter }
                      expected_port: { role: Port }
                      allowed_contexts: [{ role: Adapter }]
                  reason: Well-formed port-boundary contract.
            """);

        Assert.DoesNotThrow(() => new ArchitecturePolicyDocumentLoader().Load(policyPath));
    }

    [Test]
    public void PortBoundary_MissingTargetContextMetadata_ThrowsActionableError()
    {
        string policyPath = WritePolicy($$"""
            version: 1
            name: Test
            analysis:
              target_assemblies: [{{AssemblyName}}]
            contracts:
              strict_port_boundaries:
                - name: port-boundary
                  source: { role: ApplicationLayer }
                  target_context: { metadata: {} }
                  allowed_seams: [{ role: Port }]
                  forbidden: [{ role: DomainLayer }]
                  reason: Test.
            """);

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() =>
            new ArchitecturePolicyDocumentLoader().Load(policyPath))!;

        Assert.That(ex.Message, Does.Contain("non-empty 'target_context.metadata'"));
    }

    [Test]
    public void PortBoundary_UnknownNestedAdapterBindingProperty_ThrowsActionableError()
    {
        string policyPath = WritePolicy($$"""
            version: 1
            name: Test
            analysis:
              target_assemblies: [{{AssemblyName}}]
            contracts:
              strict_port_boundaries:
                - name: port-boundary
                  source: { role: ApplicationLayer }
                  target_context: { metadata: { domain: Catalog } }
                  allowed_seams: [{ role: Port }]
                  forbidden: [{ role: DomainLayer }]
                  adapter_bindings:
                    - adapter: { role: Adapter }
                      expected_port: { role: Port }
                      allowed_context: [{ role: Adapter }]
                  reason: Test nested validation.
            """);

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() =>
            new ArchitecturePolicyDocumentLoader().Load(policyPath))!;

        Assert.That(ex.Message, Does.Contain("unknown property 'allowed_context'"));
    }

    [Test]
    public void PortBoundary_MissingReason_ThrowsActionableError()
    {
        string policyPath = WritePolicy($$"""
            version: 1
            name: Test
            analysis:
              target_assemblies: [{{AssemblyName}}]
            contracts:
              strict_port_boundaries:
                - name: port-boundary
                  source: { role: ApplicationLayer }
                  target_context: { metadata: { domain: Catalog } }
                  allowed_seams: [{ role: Port }]
                  forbidden: [{ role: DomainLayer }]
            """);

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() =>
            new ArchitecturePolicyDocumentLoader().Load(policyPath))!;

        Assert.That(ex.Message, Does.Contain("non-empty 'reason'"));
    }

    [Test]
    public void PortBoundary_EmptyReason_ThrowsActionableError()
    {
        string policyPath = WritePolicy($$"""
            version: 1
            name: Test
            analysis:
              target_assemblies: [{{AssemblyName}}]
            contracts:
              strict_port_boundaries:
                - name: port-boundary
                  source: { role: ApplicationLayer }
                  target_context: { metadata: { domain: Catalog } }
                  allowed_seams: [{ role: Port }]
                  forbidden: [{ role: DomainLayer }]
                  reason: "   "
            """);

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() =>
            new ArchitecturePolicyDocumentLoader().Load(policyPath))!;

        Assert.That(ex.Message, Does.Contain("non-empty 'reason'"));
    }
}
