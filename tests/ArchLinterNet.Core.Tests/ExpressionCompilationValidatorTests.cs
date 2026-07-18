using ArchLinterNet.CEL.Compilation;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Families;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

// Coverage for issue #163 (openspec/changes/core-cel-integration): compiling `when` fields through
// ArchLinterNet.CEL at policy-load time, context-schema selection, compiled-predicate caching, the
// literal-only fast path, and the port-boundary/adapter-binding scope boundary (Decision D4).
[TestFixture]
public sealed partial class ExpressionCompilationValidatorTests
{
    private string _tempDir = null!;

    [SetUp]
    public void SetUp()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"arch-linter-expression-compilation-test-{Guid.NewGuid():N}");
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

    private string WritePolicy(string yaml, string fileName = "dependencies.arch.yml")
    {
        string path = Path.Combine(_tempDir, fileName);
        File.WriteAllText(path, yaml);
        return path;
    }

    private static string AssemblyName => typeof(ExpressionCompilationValidatorTests).Assembly.GetName().Name!;

    [Test]
    public void Load_LayerSelectorWhen_CompilesAndCaches()
    {
        string policyPath = WritePolicy($$"""
            version: 1
            name: Test
            analysis:
              target_assemblies: [{{AssemblyName}}]
            layers:
              sales:
                selector:
                  role: DomainLayer
                  when: >
                    subject.metadataText.containsKey("domain")
                    && subject.metadataText["domain"] == "Sales"
            contracts:
              strict: []
            """);

        ArchitectureContractDocument document = new ArchitecturePolicyDocumentLoader().Load(policyPath);

        CelCompiledPredicate? compiled = document.Layers["sales"].Selector!.CompiledWhen;
        Assert.That(compiled, Is.Not.Null);
    }

    [Test]
    public void Load_ContextDependencySourceWhen_Compiles()
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
                    when: source.metadataText.containsKey("domain")
                  forbidden:
                    - role: DomainLayer
                  reason: Test.
            """);

        ArchitectureContractDocument document = new ArchitecturePolicyDocumentLoader().Load(policyPath);

        Assert.That(document.Contracts.StrictContextDependencies[0].Source.CompiledWhen, Is.Not.Null);
    }

    [Test]
    public void Load_ContextDependencyForbiddenWhen_ComparesSourceAndTarget_Compiles()
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
                      when: >
                        source.metadataText.containsKey("domain")
                        && target.metadataText.containsKey("domain")
                        && target.metadataText["domain"] != source.metadataText["domain"]
                  reason: Test.
            """);

        ArchitectureContractDocument document = new ArchitecturePolicyDocumentLoader().Load(policyPath);

        Assert.That(document.Contracts.StrictContextDependencies[0].Forbidden[0].CompiledWhen, Is.Not.Null);
    }

    [Test]
    public void Load_ContextDependencyExcludeWhen_Compiles()
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
                      when: target.role == "SharedKernel"
                  reason: Test.
            """);

        ArchitectureContractDocument document = new ArchitecturePolicyDocumentLoader().Load(policyPath);

        Assert.That(document.Contracts.StrictContextDependencies[0].Exclude[0].CompiledWhen, Is.Not.Null);
    }

    [Test]
    public void Load_ContextAllowOnlySourceAllowedExcludeWhen_AllCompile()
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
                    when: source.role == "DomainLayer"
                  allowed:
                    - role: SharedKernel
                      when: target.role == "SharedKernel"
                  exclude:
                    - role: SharedKernel
                      when: target.metadataText.containsKey("domain")
                  reason: Test.
            """);

        ArchitectureContractDocument document = new ArchitecturePolicyDocumentLoader().Load(policyPath);

        ArchitectureContextAllowOnlyContract contract = document.Contracts.StrictContextAllowOnly[0];
        Assert.Multiple(() =>
        {
            Assert.That(contract.Source.CompiledWhen, Is.Not.Null);
            Assert.That(contract.Allowed[0].CompiledWhen, Is.Not.Null);
            Assert.That(contract.Exclude[0].CompiledWhen, Is.Not.Null);
        });
    }

    // dependency.* facts are populated with fixed, non-per-edge constants in this release (see
    // ArchitectureExpressionFactService.BuildDependencyFacts and
    // openspec/changes/cel-selector-contextual-integration/design.md Decision D6) — a `when` that
    // reads them would compile but then always evaluate the same way regardless of the real edge,
    // silently weakening the contract instead of failing closed. Policy loading rejects any
    // reference to `dependency` at a target-context `when` location until real per-edge facts exist.
    [Test]
    public void Load_ContextDependencyForbiddenWhen_ReferencesDependency_ThrowsActionableError()
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
                      when: dependency.viaMethodBody == false
                  reason: Test.
            """);

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() =>
            new ArchitecturePolicyDocumentLoader().Load(policyPath))!;

        Assert.Multiple(() =>
        {
            Assert.That(ex.Message, Does.Contain("dependency"));
            Assert.That(ex.Message, Does.Contain("forbidden[0].when"));
        });
    }

    // Regression: the rejection regex must catch the identifier `dependency` regardless of what
    // syntax reaches it, not just the literal substring "dependency." — CEL allows postfix member
    // access after any parenthesized expression, so `(dependency).viaMethodBody` names the same
    // root variable while never producing that substring.
    [Test]
    public void Load_ContextDependencyForbiddenWhen_ReferencesDependencyThroughParentheses_StillRejected()
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
                      when: (dependency).viaMethodBody == false
                  reason: Test.
            """);

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() =>
            new ArchitecturePolicyDocumentLoader().Load(policyPath))!;

        Assert.That(ex.Message, Does.Contain("dependency"));
    }

    // Deliberate, documented trade-off: the rejection is an unconditional whole-string bare-word
    // match, so the word "dependency" appearing inside a quoted string literal is also rejected,
    // even though it doesn't reference the `dependency` root variable. Two earlier attempts at
    // string/comment-aware precision each introduced a real bypass (see
    // ExpressionCompilationValidator's ReferencesDependencyIdentifier doc comment for the specific
    // cases) - accepting this over-rejection is the price of a check that cannot be bypassed.
    [Test]
    public void Load_ContextDependencyForbiddenWhen_ReferencesDependencyOnlyInsideStringLiteral_StillRejected()
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
                      when: target.metadataText["domain"] == "some-dependency-value"
                  reason: Test.
            """);

        Assert.Throws<InvalidOperationException>(() =>
            new ArchitecturePolicyDocumentLoader().Load(policyPath));
    }

    // Regression for the specific bypass a hand-rolled quote-tracking scanner had: CEL raw strings
    // (r'...') don't treat backslash as an escape character, so r'\' is a complete 3-character raw
    // string that closes at the quote immediately after the backslash. A scanner that always treats
    // backslash as escaping the next character (correct for ordinary strings, wrong for raw ones)
    // would misread this as "still inside a string" and let the real `dependency` reference past
    // unnoticed. The unconditional whole-string match has no such state to get wrong.
    [Test]
    public void Load_ContextDependencyForbiddenWhen_ReferencesDependencyAfterRawStringBackslash_StillRejected()
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
                      when: r'\' == "x" || dependency.viaMethodBody == false
                  reason: Test.
            """);

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() =>
            new ArchitecturePolicyDocumentLoader().Load(policyPath))!;

        Assert.That(ex.Message, Does.Contain("dependency"));
    }

    // Safety net (still applicable): the unconditional match rejects an expression containing a
    // triple-quote opener too, the same as everywhere else in this file.
    [Test]
    public void Load_ContextDependencyForbiddenWhen_TripleQuotedStringContainingDependency_StillRejected()
    {
        string policyPath = WritePolicy($$"""""
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
                      when: target.metadataText["domain"] == """some-dependency-value"""
                  reason: Test.
            """"");

        Assert.Throws<InvalidOperationException>(() =>
            new ArchitecturePolicyDocumentLoader().Load(policyPath));
    }

    [Test]
    public void Load_ContextAllowOnlyExcludeWhen_ReferencesDependency_ThrowsActionableError()
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
                    - role: SharedKernel
                  exclude:
                    - role: SharedKernel
                      when: dependency.kind == "type_reference"
                  reason: Test.
            """);

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() =>
            new ArchitecturePolicyDocumentLoader().Load(policyPath))!;

        Assert.Multiple(() =>
        {
            Assert.That(ex.Message, Does.Contain("dependency"));
            Assert.That(ex.Message, Does.Contain("exclude[0].when"));
        });
    }

    [Test]
    public void Load_SyntaxError_ThrowsBeforeReturningDocument()
    {
        string policyPath = WritePolicy($$"""
            version: 1
            name: Test
            analysis:
              target_assemblies: [{{AssemblyName}}]
            layers:
              sales:
                selector:
                  role: DomainLayer
                  when: "subject.role =="
            contracts:
              strict: []
            """);

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() =>
            new ArchitecturePolicyDocumentLoader().Load(policyPath))!;

        Assert.That(ex.Message, Does.Contain("sales"));
    }

    [Test]
    public void Load_UnknownMember_ThrowsBeforeReturningDocument()
    {
        string policyPath = WritePolicy($$"""
            version: 1
            name: Test
            analysis:
              target_assemblies: [{{AssemblyName}}]
            layers:
              sales:
                selector:
                  role: DomainLayer
                  when: subject.metadata.domain == "Sales"
            contracts:
              strict: []
            """);

        Assert.Throws<InvalidOperationException>(() =>
            new ArchitecturePolicyDocumentLoader().Load(policyPath));
    }

    [Test]
    public void Load_NonBooleanResult_ThrowsBeforeReturningDocument()
    {
        string policyPath = WritePolicy($$"""
            version: 1
            name: Test
            analysis:
              target_assemblies: [{{AssemblyName}}]
            layers:
              sales:
                selector:
                  role: DomainLayer
                  when: subject.role
            contracts:
              strict: []
            """);

        Assert.Throws<InvalidOperationException>(() =>
            new ArchitecturePolicyDocumentLoader().Load(policyPath));
    }

    [Test]
    public void Load_SourceWhenReferencesTarget_ThrowsBecauseTargetNotInSourceSchema()
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
                    when: target.role == "DomainLayer"
                  forbidden:
                    - role: DomainLayer
                  reason: Test.
            """);

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() =>
            new ArchitecturePolicyDocumentLoader().Load(policyPath))!;

        Assert.That(ex.Message, Does.Contain("source.when"));
    }

    [Test]
    public void Load_PortBoundarySourceWhen_ThrowsUnknownProperty()
    {
        string policyPath = WritePolicy($$"""
            version: 1
            name: Test
            analysis:
              target_assemblies: [{{AssemblyName}}]
            contracts:
              strict_port_boundaries:
                - name: port-boundary
                  source: { role: ApplicationLayer, when: "source.role == \"ApplicationLayer\"" }
                  target_context: { metadata: { domain: Catalog } }
                  allowed_seams: [{ role: Port }]
                  forbidden: [{ role: DomainLayer }]
                  reason: Test.
            """);

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() =>
            new ArchitecturePolicyDocumentLoader().Load(policyPath))!;

        Assert.That(ex.Message, Does.Contain("unknown property 'when'"));
    }

    [Test]
    public void Load_AdapterBindingWhen_ThrowsUnknownProperty()
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
                      allowed_contexts: [{ role: Adapter, when: "target.role == \"Adapter\"" }]
                  reason: Test.
            """);

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() =>
            new ArchitecturePolicyDocumentLoader().Load(policyPath))!;

        Assert.That(ex.Message, Does.Contain("unknown property 'when'"));
    }

    [Test]
    public void Load_NoWhenFields_CompiledWhenStaysNull()
    {
        string policyPath = WritePolicy($$"""
            version: 1
            name: Test
            analysis:
              target_assemblies: [{{AssemblyName}}]
            layers:
              sales:
                selector:
                  role: DomainLayer
            contracts:
              strict_context_dependencies:
                - name: domain-isolation
                  source:
                    role: DomainLayer
                  forbidden:
                    - role: DomainLayer
                  reason: Test.
            """);

        ArchitectureContractDocument document = new ArchitecturePolicyDocumentLoader().Load(policyPath);

        Assert.Multiple(() =>
        {
            Assert.That(document.Layers["sales"].Selector!.CompiledWhen, Is.Null);
            Assert.That(document.Contracts.StrictContextDependencies[0].Source.CompiledWhen, Is.Null);
            Assert.That(document.Contracts.StrictContextDependencies[0].Forbidden[0].CompiledWhen, Is.Null);
        });
    }

    [Test]
    public void Load_SamePolicyTwice_ProducesIndependentCompiledPredicates()
    {
        string policyPath = WritePolicy($$"""
            version: 1
            name: Test
            analysis:
              target_assemblies: [{{AssemblyName}}]
            layers:
              sales:
                selector:
                  role: DomainLayer
                  when: subject.role == "DomainLayer"
            contracts:
              strict: []
            """);

        ArchitectureContractDocument first = new ArchitecturePolicyDocumentLoader().Load(policyPath);
        ArchitectureContractDocument second = new ArchitecturePolicyDocumentLoader().Load(policyPath);

        CelCompiledPredicate? firstCompiled = first.Layers["sales"].Selector!.CompiledWhen;
        CelCompiledPredicate? secondCompiled = second.Layers["sales"].Selector!.CompiledWhen;

        Assert.Multiple(() =>
        {
            Assert.That(firstCompiled, Is.Not.Null);
            Assert.That(secondCompiled, Is.Not.Null);
            Assert.That(ReferenceEquals(firstCompiled, secondCompiled), Is.False,
                "compiled predicates from two Load() calls must not share a static cache instance");
        });
    }

    [Test]
    public void Load_ComposedPolicyWithWhenInAllowedLocation_PassesEffectiveSchemaValidation()
    {
        string root = Path.Combine(_tempDir, "root.yml");
        File.WriteAllText(root, $$"""
            version: 1
            name: Test
            imports:
              - fragment.yml
            analysis:
              target_assemblies: [{{AssemblyName}}]
            contracts:
              strict: []
            """);
        File.WriteAllText(Path.Combine(_tempDir, "fragment.yml"), """
            layers:
              sales:
                selector:
                  role: DomainLayer
                  when: subject.role == "DomainLayer"
            """);

        ArchitectureContractDocument document = new ArchitecturePolicyDocumentLoader().Load(root);

        Assert.That(document.Layers["sales"].Selector!.CompiledWhen, Is.Not.Null);
    }

}
