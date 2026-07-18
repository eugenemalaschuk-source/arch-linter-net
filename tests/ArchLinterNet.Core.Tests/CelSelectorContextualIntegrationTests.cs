using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Families;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

// End-to-end coverage for issue #164 (openspec/changes/cel-selector-contextual-integration):
// wiring compiled `When` predicates into layer-selector and contextual dependency/allow-only
// matching. Uses the real YAML-load pipeline (ArchitecturePolicyDocumentLoader) so `When` is
// compiled exactly as production policies compile it, then exercises matching through
// ArchitectureAnalysisSession/ArchitectureContractRunner - not the matcher in isolation.
[TestFixture]
public sealed class CelSelectorContextualIntegrationTests
{
    private string _tempDir = null!;

    [SetUp]
    public void SetUp()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"arch-linter-cel-selector-contextual-test-{Guid.NewGuid():N}");
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

    private static string AssemblyName => typeof(CelSelectorContextualIntegrationTests).Assembly.GetName().Name!;

    private static ArchitectureAnalysisContext CreateContext()
    {
        return new ArchitectureAnalysisContext(
            "/tmp", new[] { typeof(CelSelectorContextualIntegrationTests).Assembly }, Array.Empty<string>(), Array.Empty<string>());
    }

    private ArchitectureContractDocument Load(string yaml) =>
        new ArchitecturePolicyDocumentLoader().Load(WritePolicy(yaml));

    // --- Layer selector `When` ---

    [Test]
    public void LayerSelector_When_RefinesRoleMatch_MatchingCase()
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
            layers:
              sales_domain:
                selector:
                  role: DomainLayer
                  when: subject.metadataText["domain"] == "Sales"
            contracts:
              strict: []
            """);
        var runner = new ArchitectureContractRunner(CreateContext(), document);

        Type[] matches = TypesMatchingLayer(runner.Session, document.Layers["sales_domain"]);

        Assert.That(matches, Does.Contain(typeof(ContextualContractTestFixtures.SalesOrder)));
        Assert.That(matches, Does.Not.Contain(typeof(ContextualContractTestFixtures.InventoryStockItem)));
    }

    [Test]
    public void LayerSelector_When_EvaluatesFalse_ExcludesOtherwiseRoleMatchingType()
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
            layers:
              sales_domain:
                selector:
                  role: DomainLayer
                  when: subject.metadataText["domain"] == "Sales"
            contracts:
              strict: []
            """);
        var runner = new ArchitectureContractRunner(CreateContext(), document);

        Type[] matches = TypesMatchingLayer(runner.Session, document.Layers["sales_domain"]);

        Assert.That(matches, Does.Not.Contain(typeof(ContextualContractTestFixtures.InventoryStockItem)));
    }

    [Test]
    public void LayerSelector_WithoutWhen_BehavesIdenticallyToPreExistingLiteralMatching()
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
            layers:
              all_domain:
                selector:
                  role: DomainLayer
            contracts:
              strict: []
            """);
        var runner = new ArchitectureContractRunner(CreateContext(), document);

        Type[] matches = TypesMatchingLayer(runner.Session, document.Layers["all_domain"]);

        Assert.Multiple(() =>
        {
            Assert.That(matches, Does.Contain(typeof(ContextualContractTestFixtures.SalesOrder)));
            Assert.That(matches, Does.Contain(typeof(ContextualContractTestFixtures.InventoryStockItem)));
        });
    }

    [Test]
    public void LayerSelector_When_EvaluationFailure_ThrowsAndIdentifiesExpression()
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
            layers:
              sales_domain:
                selector:
                  role: DomainLayer
                  when: subject.metadataText["missing-key"] == "Sales"
            contracts:
              strict: []
            """);
        var runner = new ArchitectureContractRunner(CreateContext(), document);

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => TypesMatchingLayer(runner.Session, document.Layers["sales_domain"]))!;

        Assert.That(ex.Message, Does.Contain("When").IgnoreCase);
    }

    // --- Stale-selector coverage awareness of `When` ---

    [Test]
    public void SemanticCoverage_LayerSelectorWhen_MatchingSelector_IsNotReportedStale()
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
            layers:
              sales_only:
                selector:
                  role: DomainLayer
                  when: subject.metadataText["domain"] == "Sales"
            contracts:
              strict_coverage:
                - name: semantic-role-coverage
                  id: semantic-role-coverage
                  scope: semantic_role
                  roots:
                    - namespace: ContextualContractTestFixtures
            """);
        var runner = new ArchitectureContractRunner(CreateContext(), document);
        ArchitectureCoverageSummary summary = runner.BuildCoverageSummary(document.Contracts.StrictCoverage[0])!;

        Assert.That(summary.StaleItems, Is.Empty);
    }

    [Test]
    public void SemanticCoverage_LayerSelectorWhen_NoMatchViaWhen_IsReportedStaleEvenThoughRoleMatchesOtherTypes()
    {
        // Before #164, this selector's literal role (DomainLayer, matched by many fixture types)
        // alone would have kept it off the stale list. With `When` now participating in matching,
        // a `When` that never evaluates true for any classified type correctly surfaces as stale -
        // the "broad predicates remain visible through coverage" requirement from the issue.
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
            layers:
              nonexistent_domain:
                selector:
                  role: DomainLayer
                  when: subject.metadataText["domain"] == "NonExistentDomain"
            contracts:
              strict_coverage:
                - name: semantic-role-coverage
                  id: semantic-role-coverage
                  scope: semantic_role
                  roots:
                    - namespace: ContextualContractTestFixtures
            """);
        var runner = new ArchitectureContractRunner(CreateContext(), document);
        ArchitectureCoverageSummary summary = runner.BuildCoverageSummary(document.Contracts.StrictCoverage[0])!;

        Assert.That(summary.StaleItems, Has.Some.Matches<ArchitectureCoverageSummaryEvidenceItem>(
            item => item.Item.Contains("NonExistentDomain")));
    }

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

        Assert.Throws<InvalidOperationException>(() => runner.Session.CheckContextDependencyContract(contract));
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

        Assert.Throws<InvalidOperationException>(() => runner.Session.CheckContextDependencyContract(contract));
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

    // --- helpers ---

    private static Type[] TypesMatchingLayer(ArchitectureAnalysisSession session, ArchitectureLayer layer)
    {
        return typeof(CelSelectorContextualIntegrationTests).Assembly.GetTypes()
            .Where(t => ArchitectureLayerMatchesForTest(session, layer, t))
            .ToArray();
    }

    private static bool ArchitectureLayerMatchesForTest(ArchitectureAnalysisSession session, ArchitectureLayer layer, Type type)
    {
        return ArchLinterNet.Core.Execution.ArchitectureLayerTypeMatcher.Matches(
            layer, type, session.RoleIndex, session.ExpressionFacts);
    }
}
