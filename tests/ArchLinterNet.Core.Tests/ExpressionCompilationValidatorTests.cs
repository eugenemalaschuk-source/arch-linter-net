using ArchLinterNet.CEL.Compilation;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Families;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

// Coverage for issue #163 (openspec/changes/core-cel-integration): compiling `when` fields through
// ArchLinterNet.CEL at policy-load time, context-schema selection, compiled-predicate caching, the
// literal-only fast path, and the port-boundary/adapter-binding scope boundary (Decision D4).
[TestFixture]
public sealed class ExpressionCompilationValidatorTests
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

    // Regression coverage for PR #347 review findings (openspec/changes/core-cel-integration):
    // (1) 'when' outside the seven approved locations was silently dropped by
    //     IgnoreUnmatchedProperties() for monolithic (non-import) policies, because
    //     ArchitecturePolicyEffectiveSchemaValidator only runs when imports are present;
    // (2) an explicitly empty/null 'when' was treated as though the field were absent instead of
    //     failing the load;
    // (3) compile-failure diagnostics were attributed only to the owning layer/contract, not the
    //     exact '<field>[<index>].when' node, losing precision for composed-policy provenance.
    // See ArchitecturePolicyDocumentLoader.ValidateRawWhenFieldLocations and
    // ExpressionCompilationValidator's per-selector SetValidationSubject calls.

    [Test]
    public void Load_WhenUnderAnalysis_MonolithicPolicy_ThrowsUnsupportedLocation()
    {
        string policyPath = WritePolicy($$"""
            version: 1
            name: Test
            analysis:
              target_assemblies: [{{AssemblyName}}]
              when: "true"
            contracts:
              strict: []
            """);

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() =>
            new ArchitecturePolicyDocumentLoader().Load(policyPath))!;

        Assert.That(ex.Message, Does.Contain("analysis.when"));
    }

    [Test]
    public void Load_WhenOnBareContractEntry_MonolithicPolicy_ThrowsUnsupportedLocation()
    {
        // Non-contextual families (e.g. `contracts.strict`) have no `when`-eligible field at all -
        // a `when` key directly on the contract object must still be rejected, not silently dropped.
        string policyPath = WritePolicy($$"""
            version: 1
            name: Test
            analysis:
              target_assemblies: [{{AssemblyName}}]
            contracts:
              strict:
                - name: no-domain-to-application
                  source: Domain
                  forbidden: [Application]
                  when: "true"
            """);

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() =>
            new ArchitecturePolicyDocumentLoader().Load(policyPath))!;

        Assert.That(ex.Message, Does.Contain("contracts.strict.0.when"));
    }

    [Test]
    public void Load_MetadataKeyLiterallyNamedWhen_DoesNotTriggerLocationCheck()
    {
        // A user metadata entry legitimately named "when" (e.g. domain=Sales, when=onboarding) is
        // ordinary metadata content, not the CEL expression field, and must load normally.
        string policyPath = WritePolicy($$"""
            version: 1
            name: Test
            analysis:
              target_assemblies: [{{AssemblyName}}]
            layers:
              sales:
                selector:
                  role: DomainLayer
                  metadata:
                    when: onboarding
            contracts:
              strict: []
            """);

        Assert.DoesNotThrow(() => new ArchitecturePolicyDocumentLoader().Load(policyPath));
    }

    [Test]
    public void Load_EmptyWhenOnLayerSelector_ThrowsInsteadOfTreatingAsAbsent()
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
                  when: ""
            contracts:
              strict: []
            """);

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() =>
            new ArchitecturePolicyDocumentLoader().Load(policyPath))!;

        Assert.That(ex.Message, Does.Contain("non-empty string"));
    }

    [Test]
    public void Load_NullWhenOnLayerSelector_ThrowsInsteadOfTreatingAsAbsent()
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
                  when:
            contracts:
              strict: []
            """);

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() =>
            new ArchitecturePolicyDocumentLoader().Load(policyPath))!;

        Assert.That(ex.Message, Does.Contain("non-empty string"));
    }

    [Test]
    public void Load_MissingWhen_LoadsNormally()
    {
        // Absent (never-declared) `when` remains the ordinary, unaffected literal-only path.
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
              strict: []
            """);

        Assert.DoesNotThrow(() => new ArchitecturePolicyDocumentLoader().Load(policyPath));
    }

    [Test]
    public void Load_InvalidImportedForbiddenWhen_ErrorMessageAndProvenanceIdentifyExactIndex()
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
                namespace: App.Sales
            contracts:
              strict_context_dependencies:
                - name: domain-isolation
                  source:
                    role: DomainLayer
                  forbidden:
                    - role: DomainLayer
                    - role: DomainLayer
                    - role: DomainLayer
                      when: "subject.role =="
                  reason: Test.
            """);

        // Composed (imported) policies wrap the InvalidOperationException in
        // ArchitecturePolicyValidationException (a subclass, for fragment enrichment) - Assert.Catch
        // matches by assignability, unlike Assert.Throws's exact-type check.
        InvalidOperationException ex = Assert.Catch<InvalidOperationException>(() =>
            new ArchitecturePolicyDocumentLoader().Load(root))!;

        Assert.Multiple(() =>
        {
            Assert.That(ex.Message, Does.Contain("forbidden[2].when"));
            Assert.That(ex.Message, Does.Contain("fragment.yml"));
            Assert.That(ex.Message, Does.Contain("contracts.strict_context_dependencies[0].forbidden[2]"));
        });
    }

    // Regression coverage for the PR #347 second review round: (A) the raw walk's opaque-value
    // exclusion was name-only ("metadata"/"condition_sets"/etc regardless of location), so an
    // unsupported `when` could be smuggled under a same-named-but-unrelated bogus container (e.g.
    // `analysis.metadata.when` - ArchitectureAnalysisConfiguration has no real Metadata property,
    // so the whole container silently vanishes during deserialization, `when` included); (B) any
    // mapping key literally named "when" was unconditionally treated as the CEL marker, so a
    // layer/external-dependency-group/package-group literally named "when" (a previously valid,
    // arbitrary name) incorrectly failed to load. See
    // ArchitecturePolicyDocumentLoader.IsRecognizedOpaqueValueKey (sibling-key-based, not name-only)
    // and the childKeysAreArbitraryNames parameter threaded through WalkForWhenFields.

    [Test]
    public void Load_WhenNestedUnderBogusAnalysisMetadataContainer_ThrowsUnsupportedLocation()
    {
        string policyPath = WritePolicy($$"""
            version: 1
            name: Test
            analysis:
              target_assemblies: [{{AssemblyName}}]
              metadata:
                when: "true"
            contracts:
              strict: []
            """);

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() =>
            new ArchitecturePolicyDocumentLoader().Load(policyPath))!;

        Assert.That(ex.Message, Does.Contain("analysis.metadata.when"));
    }

    [Test]
    public void Load_WhenNestedUnderBogusConditionSetsContainer_ThrowsUnsupportedLocation()
    {
        // condition_sets is only a recognized opaque boundary as a sibling of target_assemblies/solution
        // (i.e. genuinely inside `analysis`) - here it is nested one level too deep, inside a bogus
        // `analysis.classification_placeholder.condition_sets`-shaped container, and must not shield `when`.
        string policyPath = WritePolicy($$"""
            version: 1
            name: Test
            analysis:
              target_assemblies: [{{AssemblyName}}]
            classification:
              condition_sets:
                when: "true"
            contracts:
              strict: []
            """);

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() =>
            new ArchitecturePolicyDocumentLoader().Load(policyPath))!;

        Assert.That(ex.Message, Does.Contain("classification.condition_sets.when"));
    }

    [Test]
    public void Load_LayerLiterallyNamedWhen_LoadsNormally()
    {
        string policyPath = WritePolicy($$"""
            version: 1
            name: Test
            analysis:
              target_assemblies: [{{AssemblyName}}]
            layers:
              when:
                namespace: App.Whenever
            contracts:
              strict: []
            """);

        Assert.DoesNotThrow(() => new ArchitecturePolicyDocumentLoader().Load(policyPath));
    }

    [Test]
    public void Load_LayerLiterallyNamedWhen_SelectorWhenStillCompiles()
    {
        // Confirms the arbitrary-name exemption is scoped to exactly the group-name level: one level
        // deeper, inside the "when"-named layer's own selector, `when` is still the real CEL field.
        string policyPath = WritePolicy($$"""
            version: 1
            name: Test
            analysis:
              target_assemblies: [{{AssemblyName}}]
            layers:
              when:
                selector:
                  role: DomainLayer
                  when: subject.role == "DomainLayer"
            contracts:
              strict: []
            """);

        ArchitectureContractDocument document = new ArchitecturePolicyDocumentLoader().Load(policyPath);

        Assert.That(document.Layers["when"].Selector!.CompiledWhen, Is.Not.Null);
    }

    [Test]
    public void Load_ExternalDependencyGroupLiterallyNamedWhen_LoadsNormally()
    {
        string policyPath = WritePolicy($$"""
            version: 1
            name: Test
            analysis:
              target_assemblies: [{{AssemblyName}}]
            external_dependencies:
              when:
                namespace_prefixes: [System.Whenever]
            contracts:
              strict: []
            """);

        Assert.DoesNotThrow(() => new ArchitecturePolicyDocumentLoader().Load(policyPath));
    }
}
