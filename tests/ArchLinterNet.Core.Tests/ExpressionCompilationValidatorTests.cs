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
                      when: dependency.viaMethodBody == false
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
                      when: dependency.kind == "type_reference"
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
