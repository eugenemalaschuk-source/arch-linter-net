using System.Reflection;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;
using ContextualContractTestFixtures;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

// End-to-end tests for CheckContextDependencyContract (context_dependencies family), per tasks.md
// 7.2/7.3: missing source/target metadata, same-domain (no violation), cross-domain (violation),
// exclusions, strict vs. audit mode, JSON diagnostic evidence, and human-readable distinguishability
// from a namespace/layer DependencyDiagnostic. See
// openspec/changes/add-contextual-dependency-contracts/specs/contextual-dependency-contracts/spec.md.
[TestFixture]
public sealed class ContextDependencyContractTests
{
    private static readonly Assembly _fixturesAssembly = typeof(SalesOrder).Assembly;
    private readonly ArchitectureDiagnosticFormatter _formatter = new();

    private static ArchitectureClassificationConfiguration Classification()
    {
        return new ArchitectureClassificationConfiguration
        {
            Attributes =
            {
                new ArchitectureAttributeClassificationMapping
                {
                    Attribute = "ContextualContractTestFixtures.ContextDomainMarkerAttribute",
                    Role = "DomainLayer",
                    Metadata = new Dictionary<string, object> { ["domain"] = "constructor[0]" }
                },
                new ArchitectureAttributeClassificationMapping
                {
                    Attribute = "ContextualContractTestFixtures.ContextDomainlessMarkerAttribute",
                    Role = "DomainLayer"
                },
                new ArchitectureAttributeClassificationMapping
                {
                    Attribute = "ContextualContractTestFixtures.ContextSharedKernelMarkerAttribute",
                    Role = "SharedKernel"
                }
            }
        };
    }

    private static ArchitectureContractDocument CreateDocument(ArchitectureContextDependencyContract contract, string groupKey = "strict_context_dependencies")
    {
        var groups = new ArchitectureContractGroups();
        if (groupKey == "strict_context_dependencies")
        {
            groups.StrictContextDependencies.Add(contract);
        }
        else
        {
            groups.AuditContextDependencies.Add(contract);
        }

        return new ArchitectureContractDocument
        {
            Version = 1,
            Name = "Test",
            Classification = Classification(),
            Layers = new Dictionary<string, ArchitectureLayer>(),
            Analysis = new ArchitectureAnalysisConfiguration { TargetAssemblies = new List<string> { _fixturesAssembly.GetName().Name! } },
            Contracts = groups
        };
    }

    private static ArchitectureAnalysisContext CreateContext()
    {
        return new ArchitectureAnalysisContext("/tmp", new[] { _fixturesAssembly }, Array.Empty<string>(), Array.Empty<string>());
    }

    private static ArchitectureContextDependencyContract CrossDomainContract(string id = "sales-no-inventory")
    {
        return new ArchitectureContextDependencyContract
        {
            Id = id,
            Name = "sales-must-not-depend-on-inventory",
            Source = new ArchitectureContextSelector
            {
                Role = "DomainLayer",
                Metadata = new Dictionary<string, object> { ["domain"] = "Sales" }
            },
            Forbidden = new List<ArchitectureContextSelector>
            {
                new()
                {
                    Role = "DomainLayer",
                    Metadata = new Dictionary<string, object> { ["domain"] = "!{source.metadata.domain}" }
                }
            },
            Reason = "Bounded contexts must not depend on each other's domain types."
        };
    }

    [Test]
    public void CheckContextDependencyContract_SameDomainReference_NoViolation()
    {
        var contract = new ArchitectureContextDependencyContract
        {
            Name = "no-cross-domain",
            Source = new ArchitectureContextSelector { Role = "DomainLayer", Metadata = new Dictionary<string, object> { ["domain"] = "Sales" } },
            Forbidden = new List<ArchitectureContextSelector>
            {
                new() { Role = "DomainLayer", Metadata = new Dictionary<string, object> { ["domain"] = "Inventory" } }
            }
        };
        var runner = new ArchitectureContractRunner(CreateContext(), CreateDocument(contract));

        List<ArchitectureViolation> violations = runner.Session.CheckContextDependencyContract(contract);

        // SalesOrder -> SalesOrderLine is same-domain; SalesCheckout -> InventoryStockItem is
        // cross-domain and is expected here (asserted by the next test) rather than this one.
        Assert.That(violations.Any(v => v.SourceType == typeof(SalesOrder).FullName), Is.False);
    }

    [Test]
    public void CheckContextDependencyContract_CrossDomainReference_ReturnsViolation()
    {
        ArchitectureContextDependencyContract contract = CrossDomainContract();
        var runner = new ArchitectureContractRunner(CreateContext(), CreateDocument(contract));

        List<ArchitectureViolation> violations = runner.Session.CheckContextDependencyContract(contract);

        Assert.That(violations.Any(v =>
            v.SourceType == typeof(SalesCheckout).FullName
            && v.ForbiddenReferences.Contains(typeof(InventoryStockItem).FullName)), Is.True);
    }

    [Test]
    public void CheckContextDependencyContract_TargetReferencedViaMultipleMembers_ReturnsExactlyOneViolation()
    {
        // SalesMultiMemberReferenceToInventory references InventoryStockItem through two distinct
        // properties. ArchitectureReferenceScanner does not deduplicate its own output, so this
        // proves the checker collapses repeated occurrences of the same target into one finding.
        ArchitectureContextDependencyContract contract = CrossDomainContract();
        var runner = new ArchitectureContractRunner(CreateContext(), CreateDocument(contract));

        List<ArchitectureViolation> violations = runner.Session.CheckContextDependencyContract(contract);

        Assert.That(
            violations.Count(v =>
                v.SourceType == typeof(SalesMultiMemberReferenceToInventory).FullName
                && v.ForbiddenReferences.Contains(typeof(InventoryStockItem).FullName)),
            Is.EqualTo(1));
    }

    [Test]
    public void CheckContextDependencyContract_TargetMatchesMultipleForbiddenSelectors_ReturnsExactlyOneViolation()
    {
        // Two forbidden selectors both match InventoryStockItem (role DomainLayer): one via the
        // cross-domain not-equal-to-source constraint, one via a broader same-role selector with no
        // metadata constraint. A single source/target pair must still produce exactly one violation.
        var contract = new ArchitectureContextDependencyContract
        {
            Id = "sales-multi-forbidden-selector",
            Name = "sales-must-not-depend-on-inventory-overlapping-selectors",
            Source = new ArchitectureContextSelector { Role = "DomainLayer", Metadata = new Dictionary<string, object> { ["domain"] = "Sales" } },
            Forbidden = new List<ArchitectureContextSelector>
            {
                new() { Role = "DomainLayer", Metadata = new Dictionary<string, object> { ["domain"] = "!{source.metadata.domain}" } },
                new() { Role = "DomainLayer" }
            }
        };
        var runner = new ArchitectureContractRunner(CreateContext(), CreateDocument(contract));

        List<ArchitectureViolation> violations = runner.Session.CheckContextDependencyContract(contract);

        Assert.That(
            violations.Count(v =>
                v.SourceType == typeof(SalesCheckout).FullName
                && v.ForbiddenReferences.Contains(typeof(InventoryStockItem).FullName)),
            Is.EqualTo(1));
    }

    [Test]
    public void CheckContextDependencyContract_SharedKernelExcluded_NoViolationForExcludedTarget()
    {
        var contract = new ArchitectureContextDependencyContract
        {
            Name = "sales-no-inventory-excluding-shared-kernel",
            Source = new ArchitectureContextSelector { Role = "DomainLayer", Metadata = new Dictionary<string, object> { ["domain"] = "Sales" } },
            Forbidden = new List<ArchitectureContextSelector>
            {
                new() { Role = "DomainLayer", Metadata = new Dictionary<string, object> { ["domain"] = "!{source.metadata.domain}" } },
                new() { Role = "SharedKernel" }
            },
            Exclude = new List<ArchitectureContextSelector> { new() { Role = "SharedKernel" } }
        };
        var runner = new ArchitectureContractRunner(CreateContext(), CreateDocument(contract));

        List<ArchitectureViolation> violations = runner.Session.CheckContextDependencyContract(contract);

        Assert.That(violations.Any(v =>
            v.SourceType == typeof(SalesInvoice).FullName
            && v.ForbiddenReferences.Contains(typeof(Money).FullName)), Is.False);
    }

    [Test]
    public void CheckContextDependencyContract_ExcludeAppliesOnlyToMatchingCandidate_OtherForbiddenTargetsStillViolate()
    {
        var contract = new ArchitectureContextDependencyContract
        {
            Name = "sales-no-inventory-excluding-shared-kernel",
            Source = new ArchitectureContextSelector { Role = "DomainLayer", Metadata = new Dictionary<string, object> { ["domain"] = "Sales" } },
            Forbidden = new List<ArchitectureContextSelector>
            {
                new() { Role = "DomainLayer", Metadata = new Dictionary<string, object> { ["domain"] = "Inventory" } }
            },
            Exclude = new List<ArchitectureContextSelector> { new() { Role = "SharedKernel" } }
        };
        var runner = new ArchitectureContractRunner(CreateContext(), CreateDocument(contract));

        List<ArchitectureViolation> violations = runner.Session.CheckContextDependencyContract(contract);

        Assert.That(violations.Any(v =>
            v.SourceType == typeof(SalesInvoiceReferencingInventoryAndSharedKernel).FullName
            && v.ForbiddenReferences.Contains(typeof(InventoryStockItem).FullName)), Is.True);
        Assert.That(violations.Any(v =>
            v.SourceType == typeof(SalesInvoiceReferencingInventoryAndSharedKernel).FullName
            && v.ForbiddenReferences.Contains(typeof(Money).FullName)), Is.False);
    }

    [Test]
    public void CheckContextDependencyContract_MissingSourceMetadata_NotEqualToSourceNeverMatches()
    {
        var contract = new ArchitectureContextDependencyContract
        {
            Name = "domainless-source-no-cross-domain",
            Source = new ArchitectureContextSelector { Role = "DomainLayer" },
            Forbidden = new List<ArchitectureContextSelector>
            {
                new() { Role = "DomainLayer", Metadata = new Dictionary<string, object> { ["domain"] = "!{source.metadata.domain}" } }
            }
        };
        var runner = new ArchitectureContractRunner(CreateContext(), CreateDocument(contract));

        List<ArchitectureViolation> violations = runner.Session.CheckContextDependencyContract(contract);

        // DomainlessSourceType has role DomainLayer (so it's a source-selector match, since Source
        // declares no metadata constraint) but no "domain" metadata key, so not-equal-to-source can
        // never match any candidate target for it.
        Assert.That(violations.Any(v => v.SourceType == typeof(DomainlessSourceType).FullName), Is.False);
    }

    [Test]
    public void CheckContextDependencyContract_MissingTargetMetadata_ExactConstraintDoesNotMatch()
    {
        var contract = new ArchitectureContextDependencyContract
        {
            Name = "sales-no-inventory-domain",
            Source = new ArchitectureContextSelector { Role = "DomainLayer", Metadata = new Dictionary<string, object> { ["domain"] = "Sales" } },
            Forbidden = new List<ArchitectureContextSelector>
            {
                new() { Role = "DomainLayer", Metadata = new Dictionary<string, object> { ["domain"] = "Inventory" } }
            }
        };
        var runner = new ArchitectureContractRunner(CreateContext(), CreateDocument(contract));

        List<ArchitectureViolation> violations = runner.Session.CheckContextDependencyContract(contract);

        // SourceReferencingDomainlessTarget -> DomainlessTargetType: target has role DomainLayer but
        // no "domain" metadata key, so the forbidden selector's exact "domain: Inventory" constraint
        // cannot match it.
        Assert.That(violations.Any(v => v.SourceType == typeof(SourceReferencingDomainlessTarget).FullName), Is.False);
    }

    [Test]
    public void CheckContextDependencyContract_IgnoredViolation_SuppressesMatchingFinding()
    {
        ArchitectureContextDependencyContract contract = CrossDomainContract();
        contract.IgnoredViolations = new List<ArchitectureIgnoredViolation>
        {
            new()
            {
                SourceType = typeof(SalesCheckout).FullName!,
                ForbiddenReference = typeof(InventoryStockItem).FullName!,
                Reason = "test"
            }
        };
        var runner = new ArchitectureContractRunner(CreateContext(), CreateDocument(contract));

        List<ArchitectureViolation> violations = runner.Session.CheckContextDependencyContract(contract);

        Assert.That(violations.Any(v => v.SourceType == typeof(SalesCheckout).FullName), Is.False);
    }

    [Test]
    public void CheckContextDependencyContract_ContractNotSelected_ReturnsNoViolations()
    {
        ArchitectureContextDependencyContract contract = CrossDomainContract("sales-no-inventory-2");
        var runner = new ArchitectureContractRunner(
            CreateContext(), CreateDocument(contract), selectedContractIds: new HashSet<string> { "some-other-contract" });

        List<ArchitectureViolation> violations = runner.Session.CheckContextDependencyContract(contract);

        Assert.That(violations, Is.Empty);
    }

    [Test]
    public void ArchitectureContractFamilyRegistry_StrictContextDependencies_FailsBuildOnViolation()
    {
        ArchitectureContextDependencyContract contract = CrossDomainContract();
        var document = CreateDocument(contract, "strict_context_dependencies");
        var runner = new ArchitectureContractRunner(CreateContext(), document);

        Assert.That(document.Contracts.StrictContextDependencies, Has.Count.EqualTo(1),
            "Contract must be registered under strict_context_dependencies, the build-failing group.");

        List<ArchitectureViolation> violations = runner.Session.CheckContextDependencyContract(
            document.Contracts.StrictContextDependencies[0]);

        Assert.That(violations, Is.Not.Empty, "strict_context_dependencies violations must be reportable as build-failing.");
    }

    [Test]
    public void CheckContextDependencyContract_AuditGroup_ReportsWithoutAffectingStrictSemantics()
    {
        // Audit vs. strict is a policy-loader/CLI-mode concern (which group a contract is declared
        // under), not a difference in CheckContextDependencyContract's own behavior - both groups
        // share the same checker. This asserts the same violation condition is detected regardless
        // of which group the contract is declared under.
        ArchitectureContextDependencyContract strictContract = CrossDomainContract("strict-variant");
        ArchitectureContextDependencyContract auditContract = CrossDomainContract("audit-variant");

        var strictRunner = new ArchitectureContractRunner(CreateContext(), CreateDocument(strictContract, "strict_context_dependencies"));
        var auditRunner = new ArchitectureContractRunner(CreateContext(), CreateDocument(auditContract, "audit_context_dependencies"));

        List<ArchitectureViolation> strictViolations = strictRunner.Session.CheckContextDependencyContract(strictContract);
        List<ArchitectureViolation> auditViolations = auditRunner.Session.CheckContextDependencyContract(auditContract);

        Assert.That(strictViolations, Is.Not.Empty);
        Assert.That(auditViolations, Is.Not.Empty);
    }

    [Test]
    public void ContextDependencyDiagnostic_CarriesSourceAndTargetRoleMetadataAndMatchedSelector()
    {
        ArchitectureContextDependencyContract contract = CrossDomainContract();
        var runner = new ArchitectureContractRunner(CreateContext(), CreateDocument(contract));

        List<ArchitectureViolation> violations = runner.Session.CheckContextDependencyContract(contract);
        ArchitectureViolation violation = violations.First(v => v.SourceType == typeof(SalesCheckout).FullName);

        ContextDependencyDiagnostic diagnostic = (ContextDependencyDiagnostic)violation.Payload!.ToDiagnostic(violation);

        Assert.Multiple(() =>
        {
            Assert.That(diagnostic.Kind, Is.EqualTo(ArchitectureDiagnosticKind.ContextDependency));
            Assert.That(diagnostic.SourceRole, Is.EqualTo("DomainLayer"));
            Assert.That(diagnostic.SourceMetadata!["domain"], Is.EqualTo("Sales"));
            Assert.That(diagnostic.TargetRole, Is.EqualTo("DomainLayer"));
            Assert.That(diagnostic.TargetMetadata!["domain"], Is.EqualTo("Inventory"));
            Assert.That(diagnostic.MatchedSelector, Is.EqualTo("forbidden"));
        });
    }

    [Test]
    public void JsonOutput_IncludesRoleAndMetadataEvidence()
    {
        ArchitectureContextDependencyContract contract = CrossDomainContract();
        var runner = new ArchitectureContractRunner(CreateContext(), CreateDocument(contract));

        ArchitectureViolation violation = runner.Session.CheckContextDependencyContract(contract)
            .First(v => v.SourceType == typeof(SalesCheckout).FullName);

        string json = _formatter.FormatResultForCiArtifacts(
            "strict", passed: false,
            violations: new[] { violation },
            cycles: Array.Empty<string>(),
            classificationRoles: Array.Empty<ArchLinterNet.Core.Model.ArchitectureClassificationRoleFact>(),
            coverageFindings: Array.Empty<ArchitectureViolation>(),
            unmatched: Array.Empty<ArchitectureUnmatchedIgnoredViolation>(),
            policyConsistencyFindings: Array.Empty<PolicyConsistencyDiagnostic>(),
            coverageSummaries: Array.Empty<ArchitectureCoverageSummary>(),
            classificationConflicts: Array.Empty<ArchitectureClassificationConflict>(),
            classificationMetadataFailures: Array.Empty<ArchitectureClassificationMetadataFailure>());

        Assert.Multiple(() =>
        {
            Assert.That(json, Does.Contain("\"source_role\""));
            Assert.That(json, Does.Contain("\"target_role\""));
            Assert.That(json, Does.Contain("\"source_metadata\""));
            Assert.That(json, Does.Contain("\"target_metadata\""));
            Assert.That(json, Does.Contain("\"matched_selector\""));
            Assert.That(json, Does.Contain("DomainLayer"));
        });
    }

    [Test]
    public void HumanOutput_DistinguishableFromNamespaceLayerDependencyViolation()
    {
        ArchitectureContextDependencyContract contextContract = CrossDomainContract();
        var contextRunner = new ArchitectureContractRunner(CreateContext(), CreateDocument(contextContract));
        ArchitectureViolation contextViolation = contextRunner.Session.CheckContextDependencyContract(contextContract)
            .First(v => v.SourceType == typeof(SalesCheckout).FullName);

        var namespaceViolation = new ArchitectureViolation(
            "namespace-rule", "namespace-rule-id", "MyApp.Application.Service",
            "MyApp.Infrastructure", new[] { "MyApp.Infrastructure.Gateway" })
        {
            Payload = new DependencyPayload()
        };

        string humanOutput = _formatter.FormatViolationsForHumans(new[] { contextViolation, namespaceViolation });

        Assert.Multiple(() =>
        {
            Assert.That(humanOutput, Does.Contain("kind: context_dependency"),
                "A contextual violation must be visibly tagged so it is distinguishable from a namespace/layer violation.");
            Assert.That(humanOutput, Does.Contain("source_role: DomainLayer"));

            // The namespace/layer violation line must not carry contextual-only evidence tokens.
            int namespaceLineStart = humanOutput.IndexOf("MyApp.Application.Service", StringComparison.Ordinal);
            Assert.That(namespaceLineStart, Is.GreaterThanOrEqualTo(0));
            string namespaceLine = humanOutput[namespaceLineStart..];
            Assert.That(namespaceLine, Does.Not.Contain("kind: context_dependency"));
        });
    }
}
