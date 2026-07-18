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
// Split across two files (this one: layer selectors + stale-coverage; the
// .ContextualContracts.cs partial: contextual dependency/allow-only + remaining regressions) to
// stay under the repo's file-size threshold - see AGENTS.md.
[TestFixture]
public sealed partial class CelSelectorContextualIntegrationTests
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

        // The evaluation failure now surfaces as ArchitecturePolicyValidationException (a structured
        // diagnostic ValidateCommandHandler's typed catch already routes through the same JSON/SARIF
        // "architecture_policy_error" path a load-time policy error gets), not a bare
        // InvalidOperationException.
        ArchitecturePolicyValidationException ex = Assert.Throws<ArchitecturePolicyValidationException>(
            () => TypesMatchingLayer(runner.Session, document.Layers["sales_domain"]))!;

        Assert.Multiple(() =>
        {
            Assert.That(ex.Message, Does.Contain("layers.sales_domain.selector"), "must name the YAML location");
            Assert.That(ex.Message, Does.Contain("subject.metadataText[\"missing-key\"]"), "must include the exact expression");
            Assert.That(ex.Message, Does.Contain("DomainLayer"), "must include the selector role");
            Assert.That(ex.Diagnostic.Location, Is.Not.Null, "diagnostic must carry a real policy location");
            Assert.That(ex.Diagnostic.Location!.YamlPath, Is.EqualTo("layers.sales_domain.selector"));
            Assert.That(ex.Diagnostic.Kind, Is.EqualTo(ArchitecturePolicyDiagnosticKind.SemanticValidation));
        });
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

    [Test]
    public void SemanticCoverage_ContextualConsumerWhen_EvaluationFailure_CarriesRealPolicyLocation()
    {
        // Regression: the coverage-matching path rebuilds a synthetic ArchitectureContextSelector
        // from ArchitectureContextualConsumerReference (not the original, compiler-populated
        // selector object) — WhenLocation/WhenContractName must be threaded through that
        // reconstruction too, or an evaluation error triggered only via coverage matching (never via
        // CheckContextDependencyContract itself) loses YAML provenance. BuildCoverageSummary is
        // called here without ever calling CheckContextDependencyContract, to isolate this path.
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
                - name: sales-must-not-depend-on-other-domain
                  id: sales-must-not-depend-on-other-domain
                  source:
                    role: DomainLayer
                  forbidden:
                    - role: DomainLayer
                      when: target.metadataText["nonexistent-key"] == "Sales"
                  reason: Test.
              strict_coverage:
                - name: semantic-role-coverage
                  id: semantic-role-coverage
                  scope: semantic_role
                  roots:
                    - namespace: ContextualContractTestFixtures
            """);
        var runner = new ArchitectureContractRunner(CreateContext(), document);

        ArchitecturePolicyValidationException ex = Assert.Throws<ArchitecturePolicyValidationException>(
            () => runner.BuildCoverageSummary(document.Contracts.StrictCoverage[0]))!;

        Assert.Multiple(() =>
        {
            Assert.That(ex.Diagnostic.Location, Is.Not.Null, "coverage-path evaluation errors must still carry a policy location");
            Assert.That(ex.Diagnostic.Location!.YamlPath, Is.EqualTo("contracts.strict_context_dependencies[0].forbidden[0]"));
            Assert.That(ex.Message, Does.Contain("sales-must-not-depend-on-other-domain"));
        });
    }

    // --- helpers (shared with the .ContextualContracts.cs partial) ---

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
