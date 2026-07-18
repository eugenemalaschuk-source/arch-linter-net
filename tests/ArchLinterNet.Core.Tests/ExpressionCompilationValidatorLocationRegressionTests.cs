using ArchLinterNet.Core.Contracts;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

public sealed partial class ExpressionCompilationValidatorTests
{
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

    // Regression coverage for the PR #347 third review round: a sibling-key heuristic ("opaque if a
    // 'role' key sits next to it, or if it is the node's only key") is bypassable anywhere in the
    // tree by wrapping the payload in a fabricated container that happens to satisfy the heuristic.
    // Opaque-ness is now determined by exact structural path (mirroring _allowedWhenLocations), not
    // by the shape of the immediate parent node. See IsRecognizedOpaqueValueKey /
    // _recognizedOpaqueMetadataLocations.

    [Test]
    public void Load_WhenUnderBogusExtensionsSoleMetadataChild_ThrowsUnsupportedLocation()
    {
        // The exact reviewer repro: a single-key `extensions.metadata` container is not a legitimate
        // ArchitectureContextMetadataSelector (target_context) location, so it must not be opaque.
        string policyPath = WritePolicy($$"""
            version: 1
            name: Test
            analysis:
              target_assemblies: [{{AssemblyName}}]
            extensions:
              metadata:
                when: "true"
            contracts:
              strict: []
            """);

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() =>
            new ArchitecturePolicyDocumentLoader().Load(policyPath))!;

        Assert.That(ex.Message, Does.Contain("extensions.metadata.when"));
    }

    [Test]
    public void Load_WhenUnderBogusRoleSiblingContainer_ThrowsUnsupportedLocation()
    {
        // A fabricated node with a "role" sibling next to "metadata" is not one of the real
        // selector-shaped locations this schema declares "role"+"metadata" together at.
        string policyPath = WritePolicy($$"""
            version: 1
            name: Test
            analysis:
              target_assemblies: [{{AssemblyName}}]
            classification:
              extensions:
                role: whatever
                metadata:
                  when: "true"
            contracts:
              strict: []
            """);

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() =>
            new ArchitecturePolicyDocumentLoader().Load(policyPath))!;

        Assert.That(ex.Message, Does.Contain("classification.extensions.metadata.when"));
    }

    [Test]
    public void Load_PortBoundaryTargetContextMetadata_IsRecognizedOpaque()
    {
        // The one genuine "sole-key metadata" location (ArchitectureContextMetadataSelector) must
        // still load fine now that opaqueness is path-exact rather than shape-based.
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
                  reason: Test.
            """);

        Assert.DoesNotThrow(() => new ArchitecturePolicyDocumentLoader().Load(policyPath));
    }

    [Test]
    public void Load_ClassificationAttributeMetadata_IsRecognizedOpaque()
    {
        string policyPath = WritePolicy($$"""
            version: 1
            name: Test
            analysis:
              target_assemblies: [{{AssemblyName}}]
            classification:
              attributes:
                - attribute: Acme.DomainMarkerAttribute
                  role: DomainLayer
                  metadata:
                    when: onboarding
            contracts:
              strict: []
            """);

        Assert.DoesNotThrow(() => new ArchitecturePolicyDocumentLoader().Load(policyPath));
    }

    [Test]
    public void Load_ProjectMetadataRequiredProperties_IsRecognizedOpaque()
    {
        string policyPath = WritePolicy($$"""
            version: 1
            name: Test
            analysis:
              target_assemblies: [{{AssemblyName}}]
            contracts:
              strict_project_metadata:
                - name: project-metadata
                  projects: ["*.csproj"]
                  required_properties:
                    when: onboarding
            """);

        Assert.DoesNotThrow(() => new ArchitecturePolicyDocumentLoader().Load(policyPath));
    }

    [Test]
    public void Load_AnalysisConditionSets_IsRecognizedOpaque()
    {
        string policyPath = WritePolicy($$"""
            version: 1
            name: Test
            analysis:
              target_assemblies: [{{AssemblyName}}]
              condition_sets:
                when: [SOME_SYMBOL]
            contracts:
              strict: []
            """);

        Assert.DoesNotThrow(() => new ArchitecturePolicyDocumentLoader().Load(policyPath));
    }

    // Regression coverage for the PR #347 fourth review round: (A) the layers/external_dependencies/
    // packages arbitrary-name exemption matched by key name alone anywhere in the tree, so a bogus
    // nested "layers" container at an unrelated location suppressed when-checking for its whole
    // subtree; (B) the sequence walk only recursed one level (sequence-of-mappings), missing a `when`
    // hidden inside a doubly-nested sequence; (C) an unquoted boolean/numeric `when` scalar
    // (`when: true`, `when: 1`) was accepted for monolithic policies even though the composed-policy
    // JSON schema types `when` as a plain string. See ArchitecturePolicyDocumentLoader.WhenFields.cs.

    [Test]
    public void Load_ArbitraryNameExemptionInsideUnknownContainer_ThrowsUnsupportedLocation()
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
                  layers:
                    when: bogus wrapped inside a bogus "layers" name-group key
                  reason: Test.
            """);

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() =>
            new ArchitecturePolicyDocumentLoader().Load(policyPath))!;

        Assert.That(ex.Message, Does.Contain("contracts.strict_context_dependencies.0.layers.when"));
    }

    [Test]
    public void Load_WhenInsideNestedSequence_ThrowsUnsupportedLocation()
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
                  bogus_nested:
                    - - role: DomainLayer
                        when: "true"
                  reason: Test.
            """);

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() =>
            new ArchitecturePolicyDocumentLoader().Load(policyPath))!;

        Assert.That(ex.Message, Does.Contain("bogus_nested.0.0.when"));
    }

    [Test]
    public void Load_WhenBooleanLiteralOnLayerSelector_ThrowsMustBeQuotedString()
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
                  when: true
            contracts:
              strict: []
            """);

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() =>
            new ArchitecturePolicyDocumentLoader().Load(policyPath))!;

        Assert.That(ex.Message, Does.Contain("must be written as a quoted string"));
    }

    [Test]
    public void Load_WhenIntegerLiteralOnLayerSelector_ThrowsMustBeQuotedString()
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
                  when: 1
            contracts:
              strict: []
            """);

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() =>
            new ArchitecturePolicyDocumentLoader().Load(policyPath))!;

        Assert.That(ex.Message, Does.Contain("must be written as a quoted string"));
    }

    [Test]
    public void Load_WhenQuotedBooleanLikeStringOnLayerSelector_CompilesAsLiteralTrue()
    {
        // `when: "true"` (quoted) is functionally the same CEL source text as `when: true` would
        // produce, and is a legitimate (if discouraged - see cel-policy-model's "Broad policy
        // weakening" negative example) boolean-literal predicate; only the unquoted YAML scalar
        // style is rejected, not the resulting text content.
        string policyPath = WritePolicy($$"""
            version: 1
            name: Test
            analysis:
              target_assemblies: [{{AssemblyName}}]
            layers:
              sales:
                selector:
                  role: DomainLayer
                  when: "true"
            contracts:
              strict: []
            """);

        ArchitectureContractDocument document = new ArchitecturePolicyDocumentLoader().Load(policyPath);

        Assert.That(document.Layers["sales"].Selector!.CompiledWhen, Is.Not.Null);
    }
}
