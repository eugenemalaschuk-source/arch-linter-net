using ArchLinterNet.Core.Contracts;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

// Regression coverage for the code-review finding that a contextual contract's `source` selector
// silently accepted 'not-equal-to-source' ('!{source.metadata.<key>}'), which
// ArchitectureContextSelectorMatcher always resolves as a non-match when there is no other source
// to compare against - turning a strict contract into a false-negative no-op instead of a load-time
// error. See openspec/changes/add-contextual-dependency-contracts/design.md Decision 2/3 and
// src/ArchLinterNet.Core/Contracts/Validators/ContextualSourceSelectorValidator.cs.
[TestFixture]
public sealed class ContextualSourceSelectorValidationTests
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

    private static string AssemblyName => typeof(ContextualSourceSelectorValidationTests).Assembly.GetName().Name!;

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
}
