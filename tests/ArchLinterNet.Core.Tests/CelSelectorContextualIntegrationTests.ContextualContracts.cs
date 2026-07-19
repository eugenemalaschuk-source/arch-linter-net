using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Families;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

// Continuation of CelSelectorContextualIntegrationTests (see that file for the shared
// setup/helpers and layer-selector/stale-coverage tests): contextual dependency/allow-only
// `When` matching, the identity-collision and near-miss-evidence regressions, the
// selector-without-`when` regression safety net, the Unity/client namespace-convention example,
// JSON determinism, and the literal-only-overload structural guard.
public sealed partial class CelSelectorContextualIntegrationTests
{
    // --- Contextual dependency `When` ---

    private ArchitectureContractDocument LoadContextDependencyDocument(string forbiddenWhen, string groupKey = "strict_context_dependencies") =>
        Load($$"""
            version: 1
            name: Test
            analysis:
              target_assemblies: [{{AssemblyName}}]
            classification:
              attributes:
                - attribute: ContextualContractTestFixtures.ContextDomainMarkerAttribute
                  role: DomainLayer
                  metadata:
                    domain: constructor[0]
            contracts:
              {{groupKey}}:
                - name: sales-must-not-depend-on-other-domain
                  id: sales-cross-domain-When
                  source:
                    role: DomainLayer
                  forbidden:
                    - role: DomainLayer
                      when: {{forbiddenWhen}}
                  reason: Bounded contexts must not depend on each other's domain types.
            """);

    [Test]
    public void ContextDependency_SourceWhen_RestrictsWhichTypesQualifyAsSource()
    {
        ArchitectureContractDocument document = Load($$"""
            version: 1
            name: Test
            analysis:
              target_assemblies: [{{AssemblyName}}]
            classification:
              attributes:
                - attribute: ContextualContractTestFixtures.ContextDomainMarkerAttribute
                  role: DomainLayer
                  metadata:
                    domain: constructor[0]
            contracts:
              strict_context_dependencies:
                - name: sales-only-no-inventory
                  id: sales-only-no-inventory
                  source:
                    role: DomainLayer
                    when: source.metadataText["domain"] == "Sales"
                  forbidden:
                    - role: DomainLayer
                      metadata:
                        domain: Inventory
                  reason: Only Sales-domain sources are in scope for this contract.
            """);
        var runner = new ArchitectureContractRunner(CreateContext(), document);
        ArchitectureContextDependencyContract contract = document.Contracts.StrictContextDependencies[0];

        List<ArchitectureViolation> violations = runner.Session.CheckContextDependencyContract(contract);

        Assert.Multiple(() =>
        {
            // SalesCheckout (domain=Sales) -> InventoryStockItem: source.when matches, violation fires.
            Assert.That(violations.Any(v => v.SourceType == typeof(ContextualContractTestFixtures.SalesCheckout).FullName), Is.True);
            // InventoryWarehouse (domain=Inventory) -> InventoryStockItem: source.when excludes it as a
            // source entirely, so no violation is ever considered for it under this contract.
            Assert.That(violations.Any(v => v.SourceType == typeof(ContextualContractTestFixtures.InventoryWarehouse).FullName), Is.False);
        });
    }

    [Test]
    public void ContextDependency_SourceWhen_EvaluationFailure_FailsClosed()
    {
        ArchitectureContractDocument document = Load($$"""
            version: 1
            name: Test
            analysis:
              target_assemblies: [{{AssemblyName}}]
            classification:
              attributes:
                - attribute: ContextualContractTestFixtures.ContextDomainMarkerAttribute
                  role: DomainLayer
                  metadata:
                    domain: constructor[0]
            contracts:
              strict_context_dependencies:
                - name: sales-only-no-inventory
                  id: sales-only-no-inventory
                  source:
                    role: DomainLayer
                    when: source.metadataText["nonexistent-key"] == "Sales"
                  forbidden:
                    - role: DomainLayer
                      metadata:
                        domain: Inventory
                  reason: Test.
            """);
        var runner = new ArchitectureContractRunner(CreateContext(), document);
        ArchitectureContextDependencyContract contract = document.Contracts.StrictContextDependencies[0];

        ArchitecturePolicyValidationException ex = Assert.Throws<ArchitecturePolicyValidationException>(
            () => runner.Session.CheckContextDependencyContract(contract))!;
        Assert.That(ex.Diagnostic.Location?.YamlPath, Is.EqualTo("contracts.strict_context_dependencies[0].source"));
    }

    [Test]
    public void SemanticCoverage_TwoStaleConsumersWithDifferentWhen_ReportDistinguishableEvidence()
    {
        // Regression: even after the identity-collision fix keeps both records, their Description
        // text (the evidence string shown in stale-selector diagnostics) must also differ - it must
        // not be built from role/metadata alone, or two distinct stale predicates would render as
        // two identical-looking lines with no way to tell which `when` actually went stale.
        ArchitectureContractDocument document = Load($$"""
            version: 1
            name: Test
            analysis:
              target_assemblies: [{{AssemblyName}}]
            classification:
              attributes:
                - attribute: ContextualContractTestFixtures.ContextDomainMarkerAttribute
                  role: DomainLayer
                  metadata:
                    domain: constructor[0]
            contracts:
              strict_context_dependencies:
                - name: dependency-contract-a
                  id: dependency-contract-a
                  source:
                    role: DomainLayer
                  forbidden:
                    - role: DomainLayer
                      when: target.metadataText["domain"] == "NonExistentDomainA"
                  reason: Test A.
                - name: dependency-contract-b
                  id: dependency-contract-b
                  source:
                    role: DomainLayer
                  forbidden:
                    - role: DomainLayer
                      when: target.metadataText["domain"] == "NonExistentDomainB"
                  reason: Test B.
              strict_coverage:
                - name: semantic-role-coverage
                  id: semantic-role-coverage
                  scope: semantic_role
                  roots:
                    - namespace: ContextualContractTestFixtures
            """);
        var runner = new ArchitectureContractRunner(CreateContext(), document);
        ArchitectureCoverageSummary summary = runner.BuildCoverageSummary(document.Contracts.StrictCoverage[0])!;

        var forbiddenStaleItems = summary.StaleItems
            .Where(item => item.Item.Contains("NonExistentDomainA") || item.Item.Contains("NonExistentDomainB"))
            .ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(forbiddenStaleItems, Has.Length.EqualTo(2));
            Assert.That(forbiddenStaleItems.Select(item => item.Item).Distinct().Count(), Is.EqualTo(2));
            Assert.That(forbiddenStaleItems, Has.Some.Matches<ArchitectureCoverageSummaryEvidenceItem>(
                item => item.Item.Contains("NonExistentDomainA")));
            Assert.That(forbiddenStaleItems, Has.Some.Matches<ArchitectureCoverageSummaryEvidenceItem>(
                item => item.Item.Contains("NonExistentDomainB")));
        });
    }

    [Test]
    public void RegisteredContextualConsumers_SameRoleDifferentWhen_BothRetainedNotCollapsed()
    {
        // Regression for a TryAdd identity collision: two forbidden selectors sharing role DomainLayer
        // but declaring different `when` expressions must be distinct consumption records - collapsing
        // them would silently drop one selector's `when` from stale-selector coverage detection.
        ArchitectureContractDocument document = Load($$"""
            version: 1
            name: Test
            analysis:
              target_assemblies: [{{AssemblyName}}]
            classification:
              attributes:
                - attribute: ContextualContractTestFixtures.ContextDomainMarkerAttribute
                  role: DomainLayer
                  metadata:
                    domain: constructor[0]
            contracts:
              strict_context_dependencies:
                - name: dependency-contract-a
                  id: dependency-contract-a
                  source:
                    role: DomainLayer
                  forbidden:
                    - role: DomainLayer
                      when: target.metadataText["domain"] == "Inventory"
                  reason: Test A.
                - name: dependency-contract-b
                  id: dependency-contract-b
                  source:
                    role: DomainLayer
                  forbidden:
                    - role: DomainLayer
                      when: target.metadataText["domain"] == "SharedKernel"
                  reason: Test B.
            """);
        var runner = new ArchitectureContractRunner(CreateContext(), document);

        IReadOnlyCollection<ArchitectureContextualConsumerReference> consumers = runner.Session.RegisteredContextualConsumers;

        Assert.That(
            consumers.Count(c => c.Role == "DomainLayer" && c.SourceRole == "DomainLayer"),
            Is.EqualTo(2),
            "Two forbidden selectors with the same role but different `when` must remain two distinct consumer records.");
    }

    [Test]
    public void ContextDependency_ForbiddenWhen_CrossDomainComparison_MatchesCrossDomainReference()
    {
        const string When = "target.metadataText[\"domain\"] != source.metadataText[\"domain\"]";
        ArchitectureContractDocument document = LoadContextDependencyDocument(When);
        var runner = new ArchitectureContractRunner(CreateContext(), document);
        ArchitectureContextDependencyContract contract = document.Contracts.StrictContextDependencies[0];

        List<ArchitectureViolation> violations = runner.Session.CheckContextDependencyContract(contract);

        Assert.That(violations.Any(v =>
            v.SourceType == typeof(ContextualContractTestFixtures.SalesCheckout).FullName
            && v.ForbiddenReferences.Contains(typeof(ContextualContractTestFixtures.InventoryStockItem).FullName)), Is.True);
    }

    [Test]
    public void ContextDependency_ForbiddenWhen_CrossDomainComparison_DoesNotMatchSameDomainReference()
    {
        const string When = "target.metadataText[\"domain\"] != source.metadataText[\"domain\"]";
        ArchitectureContractDocument document = LoadContextDependencyDocument(When);
        var runner = new ArchitectureContractRunner(CreateContext(), document);
        ArchitectureContextDependencyContract contract = document.Contracts.StrictContextDependencies[0];

        List<ArchitectureViolation> violations = runner.Session.CheckContextDependencyContract(contract);

        Assert.That(violations.Any(v => v.SourceType == typeof(ContextualContractTestFixtures.SalesOrder).FullName), Is.False);
    }

    [Test]
    public void ContextDependency_ForbiddenWhen_DiagnosticEvidenceIncludesExpressionSourceText()
    {
        const string When = "target.metadataText[\"domain\"] != source.metadataText[\"domain\"]";
        ArchitectureContractDocument document = LoadContextDependencyDocument(When);
        var runner = new ArchitectureContractRunner(CreateContext(), document);
        ArchitectureContextDependencyContract contract = document.Contracts.StrictContextDependencies[0];

        List<ArchitectureViolation> violations = runner.Session.CheckContextDependencyContract(contract);
        ArchitectureViolation violation = violations.First(v =>
            v.SourceType == typeof(ContextualContractTestFixtures.SalesCheckout).FullName);

        Assert.That(violation.ForbiddenNamespace, Does.Contain("when:"));
        Assert.That(violation.ForbiddenNamespace, Does.Contain("metadataText"));
    }

    [Test]
    public void ContextDependency_ForbiddenWhen_EvaluationFailure_StrictFailsClosed()
    {
        const string When = "target.metadataText[\"nonexistent-key\"] == source.metadataText[\"domain\"]";
        ArchitectureContractDocument document = LoadContextDependencyDocument(When, "strict_context_dependencies");
        var runner = new ArchitectureContractRunner(CreateContext(), document);
        ArchitectureContextDependencyContract contract = document.Contracts.StrictContextDependencies[0];

        ArchitecturePolicyValidationException ex = Assert.Throws<ArchitecturePolicyValidationException>(
            () => runner.Session.CheckContextDependencyContract(contract))!;

        Assert.Multiple(() =>
        {
            Assert.That(ex.Message, Does.Contain("sales-must-not-depend-on-other-domain"), "must name the contract");
            Assert.That(ex.Message, Does.Contain("contracts.strict_context_dependencies[0].forbidden[0]"), "must name the YAML location");
            Assert.That(ex.Message, Does.Contain(When), "must include the exact expression");
            // Which specific ContextualContractTestFixtures type triggers the failure first depends
            // on RoleIndex classification order, not something this test pins down - assert the
            // shape (both a source and a target identity are named) rather than a specific pair.
            Assert.That(ex.Message, Does.Contain("for source 'ContextualContractTestFixtures."), "must name a source type");
            Assert.That(ex.Message, Does.Contain("-> target 'ContextualContractTestFixtures."), "must name a target type");
            Assert.That(ex.Diagnostic.Location!.YamlPath, Is.EqualTo("contracts.strict_context_dependencies[0].forbidden[0]"));
            Assert.That(ex.Diagnostic.Kind, Is.EqualTo(ArchitecturePolicyDiagnosticKind.SemanticValidation));
        });
    }

    [Test]
    public void ContextDependency_ForbiddenWhen_EvaluationFailure_AuditAlsoFailsClosed()
    {
        // Per cel-policy-model spec's "Audit contract expression failure still blocks the run"
        // scenario: audit's normally-non-blocking behavior must not downgrade an evaluation error.
        const string When = "target.metadataText[\"nonexistent-key\"] == source.metadataText[\"domain\"]";
        ArchitectureContractDocument document = LoadContextDependencyDocument(When, "audit_context_dependencies");
        var runner = new ArchitectureContractRunner(CreateContext(), document);
        ArchitectureContextDependencyContract contract = document.Contracts.AuditContextDependencies[0];

        ArchitecturePolicyValidationException ex = Assert.Throws<ArchitecturePolicyValidationException>(
            () => runner.Session.CheckContextDependencyContract(contract))!;
        Assert.That(ex.Diagnostic.Location?.YamlPath, Is.EqualTo("contracts.audit_context_dependencies[0].forbidden[0]"));
    }

    // --- Contextual allow-only `When` ---

    [Test]
    public void ContextAllowOnly_AllowedWhen_SameDomainAllowed_CrossDomainViolates()
    {
        ArchitectureContractDocument document = Load($$"""
            version: 1
            name: Test
            analysis:
              target_assemblies: [{{AssemblyName}}]
            classification:
              attributes:
                - attribute: ContextualContractTestFixtures.ContextDomainMarkerAttribute
                  role: DomainLayer
                  metadata:
                    domain: constructor[0]
            contracts:
              strict_context_allow_only:
                - name: sales-same-domain-only
                  id: sales-same-domain-only
                  source:
                    role: DomainLayer
                  allowed:
                    - role: DomainLayer
                      when: target.metadataText["domain"] == source.metadataText["domain"]
                  reason: Sales may only depend on its own domain.
            """);
        var runner = new ArchitectureContractRunner(CreateContext(), document);
        ArchitectureContextAllowOnlyContract contract = document.Contracts.StrictContextAllowOnly[0];

        List<ArchitectureViolation> violations = runner.Session.CheckContextAllowOnlyContract(contract);

        Assert.Multiple(() =>
        {
            Assert.That(violations.Any(v => v.SourceType == typeof(ContextualContractTestFixtures.SalesOrder).FullName), Is.False);
            Assert.That(violations.Any(v =>
                v.SourceType == typeof(ContextualContractTestFixtures.SalesCheckout).FullName
                && v.ForbiddenReferences.Contains(typeof(ContextualContractTestFixtures.InventoryStockItem).FullName)), Is.True);
        });
    }

    [Test]
    public void ContextAllowOnly_NearMissWhen_SurfacedAsAdditionalEvidence()
    {
        ArchitectureContractDocument document = Load($$"""
            version: 1
            name: Test
            analysis:
              target_assemblies: [{{AssemblyName}}]
            classification:
              attributes:
                - attribute: ContextualContractTestFixtures.ContextDomainMarkerAttribute
                  role: DomainLayer
                  metadata:
                    domain: constructor[0]
            contracts:
              strict_context_allow_only:
                - name: sales-same-domain-only
                  id: sales-same-domain-only
                  source:
                    role: DomainLayer
                  allowed:
                    - role: DomainLayer
                      when: target.metadataText["domain"] == source.metadataText["domain"]
                  reason: Sales may only depend on its own domain.
            """);
        var runner = new ArchitectureContractRunner(CreateContext(), document);
        ArchitectureContextAllowOnlyContract contract = document.Contracts.StrictContextAllowOnly[0];

        List<ArchitectureViolation> violations = runner.Session.CheckContextAllowOnlyContract(contract);
        ArchitectureViolation violation = violations.First(v =>
            v.SourceType == typeof(ContextualContractTestFixtures.SalesCheckout).FullName);

        Assert.That(violation.ForbiddenReferences.Any(reference => reference.Contains("when:")), Is.True);
    }

    // --- Structured expression participation on violation payloads (publish-cel-diagnostics-docs) ---

    [Test]
    public void ContextDependency_ForbiddenWhen_PayloadCarriesStructuredExpressionParticipation()
    {
        const string When = "target.metadataText[\"domain\"] != source.metadataText[\"domain\"]";
        ArchitectureContractDocument document = LoadContextDependencyDocument(When);
        var runner = new ArchitectureContractRunner(CreateContext(), document);
        ArchitectureContextDependencyContract contract = document.Contracts.StrictContextDependencies[0];

        List<ArchitectureViolation> violations = runner.Session.CheckContextDependencyContract(contract);
        ArchitectureViolation violation = violations.First(v =>
            v.SourceType == typeof(ContextualContractTestFixtures.SalesCheckout).FullName);
        var payload = (ContextDependencyPayload)violation.Payload!;

        Assert.Multiple(() =>
        {
            Assert.That(payload.WhenExpression, Is.Not.Null);
            Assert.That(payload.WhenExpression!.Source, Is.EqualTo(When));
            Assert.That(payload.WhenExpression!.Result, Is.EqualTo(ExpressionParticipationResult.Matched));
            Assert.That(payload.WhenExpression!.YamlPath, Is.EqualTo("contracts.strict_context_dependencies[0].forbidden[0]"));
        });
    }

    [Test]
    public void ContextDependency_ForbiddenWithoutWhen_PayloadOmitsExpressionParticipation()
    {
        ArchitectureContractDocument document = LoadContextDependencyDocumentLiteralOnly();
        var runner = new ArchitectureContractRunner(CreateContext(), document);
        ArchitectureContextDependencyContract contract = document.Contracts.StrictContextDependencies[0];

        List<ArchitectureViolation> violations = runner.Session.CheckContextDependencyContract(contract);
        ArchitectureViolation violation = violations.First(v =>
            v.SourceType == typeof(ContextualContractTestFixtures.SalesCheckout).FullName);
        var payload = (ContextDependencyPayload)violation.Payload!;

        Assert.That(payload.WhenExpression, Is.Null);
    }

    [Test]
    public void ContextAllowOnly_NearMissWhen_PayloadCarriesNotMatchedExpressionParticipation()
    {
        ArchitectureContractDocument document = Load($$"""
            version: 1
            name: Test
            analysis:
              target_assemblies: [{{AssemblyName}}]
            classification:
              attributes:
                - attribute: ContextualContractTestFixtures.ContextDomainMarkerAttribute
                  role: DomainLayer
                  metadata:
                    domain: constructor[0]
            contracts:
              strict_context_allow_only:
                - name: sales-same-domain-only
                  id: sales-same-domain-only
                  source:
                    role: DomainLayer
                  allowed:
                    - role: DomainLayer
                      when: target.metadataText["domain"] == source.metadataText["domain"]
                  reason: Sales may only depend on its own domain.
            """);
        var runner = new ArchitectureContractRunner(CreateContext(), document);
        ArchitectureContextAllowOnlyContract contract = document.Contracts.StrictContextAllowOnly[0];

        List<ArchitectureViolation> violations = runner.Session.CheckContextAllowOnlyContract(contract);
        ArchitectureViolation violation = violations.First(v =>
            v.SourceType == typeof(ContextualContractTestFixtures.SalesCheckout).FullName);
        var payload = (ContextAllowOnlyPayload)violation.Payload!;

        Assert.Multiple(() =>
        {
            Assert.That(payload.WhenExpression, Is.Not.Null);
            Assert.That(payload.WhenExpression!.Result, Is.EqualTo(ExpressionParticipationResult.NotMatched));
            Assert.That(payload.WhenExpression!.Source, Is.EqualTo("target.metadataText[\"domain\"] == source.metadataText[\"domain\"]"));
        });
    }

    // --- Selectors without `When` remain unaffected (regression safety net) ---

    [Test]
    public void ContextDependency_SelectorWithoutWhen_BehavesIdenticallyToPreExistingLiteralMatching()
    {
        ArchitectureContractDocument document = LoadContextDependencyDocumentLiteralOnly();
        var runner = new ArchitectureContractRunner(CreateContext(), document);
        ArchitectureContextDependencyContract contract = document.Contracts.StrictContextDependencies[0];

        List<ArchitectureViolation> violations = runner.Session.CheckContextDependencyContract(contract);

        Assert.That(violations.Any(v =>
            v.SourceType == typeof(ContextualContractTestFixtures.SalesCheckout).FullName
            && v.ForbiddenReferences.Contains(typeof(ContextualContractTestFixtures.InventoryStockItem).FullName)), Is.True);
    }

    private ArchitectureContractDocument LoadContextDependencyDocumentLiteralOnly() =>
        Load($$"""
            version: 1
            name: Test
            analysis:
              target_assemblies: [{{AssemblyName}}]
            classification:
              attributes:
                - attribute: ContextualContractTestFixtures.ContextDomainMarkerAttribute
                  role: DomainLayer
                  metadata:
                    domain: constructor[0]
            contracts:
              strict_context_dependencies:
                - name: sales-no-inventory
                  id: sales-no-inventory
                  source:
                    role: DomainLayer
                    metadata:
                      domain: Sales
                  forbidden:
                    - role: DomainLayer
                      metadata:
                        domain: Inventory
                  reason: Test.
            """);

    // --- Namespace-convention role refined by `When` (Unity/client-style: a broad namespace
    // convention assigns an initial role, and `When` narrows it using a fact `role`/`metadata`
    // alone cannot express — here, whether the type actually carries a specific marker attribute,
    // analogous to refining "everything under Assets/Scripts/Systems" down to real ECS systems). ---

    [Test]
    public void LayerSelector_NamespaceDerivedRole_RefinedByWhen_MatchesOnlyAttributeCarryingTypes()
    {
        ArchitectureContractDocument document = Load($$"""
            version: 1
            name: Test
            analysis:
              target_assemblies: [{{AssemblyName}}]
            classification:
              namespace:
                - namespace: ContextualContractTestFixtures
                  role: ConventionCandidate
            layers:
              real_domain_types:
                selector:
                  role: ConventionCandidate
                  when: >
                    "ContextualContractTestFixtures.ContextDomainMarkerAttribute"
                    in subject.attributeTypeNames
            contracts:
              strict: []
            """);
        var runner = new ArchitectureContractRunner(CreateContext(), document);

        Type[] matches = TypesMatchingLayer(runner.Session, document.Layers["real_domain_types"]);

        Assert.Multiple(() =>
        {
            // SalesOrder carries [ContextDomainMarker("Sales")] - a real domain marker.
            Assert.That(matches, Does.Contain(typeof(ContextualContractTestFixtures.SalesOrder)));
            // PlainUnclassifiedType is in the same namespace (so it gets the convention role) but
            // carries no attribute at all - `When` excludes it even though role matched.
            Assert.That(matches, Does.Not.Contain(typeof(ContextualContractTestFixtures.PlainUnclassifiedType)));
        });
    }

    // --- JSON determinism for `When`-bearing diagnostics ---

    [Test]
    public void ContextDependency_ForbiddenWhen_JsonOutput_IsDeterministicAcrossRepeatedRuns()
    {
        const string When = "target.metadataText[\"domain\"] != source.metadataText[\"domain\"]";
        var formatter = new ArchLinterNet.Core.Reporting.ArchitectureDiagnosticFormatter();

        string RunOnce()
        {
            ArchitectureContractDocument document = LoadContextDependencyDocument(When);
            var runner = new ArchitectureContractRunner(CreateContext(), document);
            ArchitectureContextDependencyContract contract = document.Contracts.StrictContextDependencies[0];
            List<ArchitectureViolation> violations = runner.Session.CheckContextDependencyContract(contract);
            return formatter.FormatResultForCiArtifacts(
                "strict", passed: false,
                violations: violations,
                cycles: Array.Empty<string>(),
                classificationRoles: Array.Empty<ArchitectureClassificationRoleFact>(),
                coverageFindings: Array.Empty<ArchitectureViolation>(),
                unmatched: Array.Empty<ArchitectureUnmatchedIgnoredViolation>(),
                policyConsistencyFindings: Array.Empty<PolicyConsistencyDiagnostic>(),
                coverageSummaries: Array.Empty<ArchitectureCoverageSummary>(),
                classificationConflicts: Array.Empty<ArchitectureClassificationConflict>(),
                classificationMetadataFailures: Array.Empty<ArchitectureClassificationMetadataFailure>());
        }

        string first = RunOnce();
        string second = RunOnce();

        Assert.That(first, Is.EqualTo(second));
    }

    // --- Structural invariant guard: `when` must never reach a location that doesn't support it ---

    [Test]
    public void ContextSelectorMatcher_LiteralOnlyOverload_ThrowsIfCompiledWhenIsSomehowPresent()
    {
        // Port-boundary/adapter-binding selectors reuse ArchitectureContextSelector's shape but
        // have `when` rejected at policy-load time, so the literal-only 4-arg Matches overload
        // those call sites use should never see a compiled predicate. This proves the guard fires
        // instead of silently ignoring one, should that structural invariant ever be violated.
        var compiled = ArchLinterNet.Core.Contracts.Expressions.ArchitectureExpressionSchemas.SelectorEnvironment
            .CompilePredicate("subject.role == \"DomainLayer\"");
        var selector = new ArchitectureContextSelector { Role = "DomainLayer", CompiledWhen = compiled.Program };
        var roleIndex = new ArchitectureRoleIndex(
            new ArchitectureClassificationConfiguration(),
            new ArchitectureTypeIndex(new[] { typeof(CelSelectorContextualIntegrationTests).Assembly }));

        Assert.Throws<InvalidOperationException>(() =>
            ArchLinterNet.Core.Execution.ArchitectureContextSelectorMatcher.Matches(
                selector, typeof(ContextualContractTestFixtures.SalesOrder), roleIndex, sourceDescriptor: null));
    }
}
