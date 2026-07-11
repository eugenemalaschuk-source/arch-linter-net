using System.Reflection;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;
using ContextualContractTestFixtures;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

// End-to-end tests for CheckContextAllowOnlyContract (context_allow_only family), per tasks.md
// 7.2/7.3, mirroring ContextDependencyContractTests. See
// openspec/changes/add-contextual-dependency-contracts/specs/contextual-allow-only-contracts/spec.md.
[TestFixture]
public sealed class ContextAllowOnlyContractTests
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

    private static ArchitectureContractDocument CreateDocument(ArchitectureContextAllowOnlyContract contract, string groupKey = "strict_context_allow_only")
    {
        var groups = new ArchitectureContractGroups();
        if (groupKey == "strict_context_allow_only")
        {
            groups.StrictContextAllowOnly.Add(contract);
        }
        else
        {
            groups.AuditContextAllowOnly.Add(contract);
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

    // Sales may reference only Sales (same-domain, via not-equal-to-source inverted as an allowed
    // same-context selector) or SharedKernel.
    private static ArchitectureContextAllowOnlyContract SalesAllowOnlyContract(string id = "sales-allow-only")
    {
        return new ArchitectureContextAllowOnlyContract
        {
            Id = id,
            Name = "sales-may-depend-only-on-own-context-or-shared-kernel",
            Source = new ArchitectureContextSelector
            {
                Role = "DomainLayer",
                Metadata = new Dictionary<string, object> { ["domain"] = "Sales" }
            },
            Allowed = new List<ArchitectureContextSelector>
            {
                new() { Role = "DomainLayer", Metadata = new Dictionary<string, object> { ["domain"] = "Sales" } },
                new() { Role = "SharedKernel" }
            },
            Reason = "Sales must depend only on its own context or the shared kernel."
        };
    }

    [Test]
    public void CheckContextAllowOnlyContract_ReferenceToAllowedTarget_NoViolation()
    {
        ArchitectureContextAllowOnlyContract contract = SalesAllowOnlyContract();
        var runner = new ArchitectureContractRunner(CreateContext(), CreateDocument(contract));

        List<ArchitectureViolation> violations = runner.Session.CheckContextAllowOnlyContract(contract);

        // SalesOrder -> SalesOrderLine (same domain) and SalesInvoice -> Money (SharedKernel) are
        // both allowed.
        Assert.That(violations.Any(v => v.SourceType == typeof(SalesOrder).FullName), Is.False);
        Assert.That(violations.Any(v => v.SourceType == typeof(SalesInvoice).FullName), Is.False);
    }

    [Test]
    public void CheckContextAllowOnlyContract_ReferenceOutsideAllowedSelectors_ReturnsViolation()
    {
        ArchitectureContextAllowOnlyContract contract = SalesAllowOnlyContract();
        var runner = new ArchitectureContractRunner(CreateContext(), CreateDocument(contract));

        List<ArchitectureViolation> violations = runner.Session.CheckContextAllowOnlyContract(contract);

        // SalesCheckout -> InventoryStockItem is outside the allowed selectors.
        Assert.That(violations.Any(v =>
            v.SourceType == typeof(SalesCheckout).FullName
            && v.ForbiddenReferences.Contains(typeof(InventoryStockItem).FullName)), Is.True);
    }

    [Test]
    public void CheckContextAllowOnlyContract_SharedKernelExcluded_UnaffectedByAllowedSharedKernelSelector()
    {
        var contract = new ArchitectureContextAllowOnlyContract
        {
            Name = "sales-allow-only-same-domain-excluding-shared-kernel",
            Source = new ArchitectureContextSelector { Role = "DomainLayer", Metadata = new Dictionary<string, object> { ["domain"] = "Sales" } },
            Allowed = new List<ArchitectureContextSelector>
            {
                new() { Role = "DomainLayer", Metadata = new Dictionary<string, object> { ["domain"] = "Sales" } }
            },
            Exclude = new List<ArchitectureContextSelector> { new() { Role = "SharedKernel" } }
        };
        var runner = new ArchitectureContractRunner(CreateContext(), CreateDocument(contract));

        List<ArchitectureViolation> violations = runner.Session.CheckContextAllowOnlyContract(contract);

        // SalesInvoice -> Money is excluded before allowed-list evaluation, so it is not reported
        // even though Money would not otherwise satisfy the (same-domain-only) allowed list.
        Assert.That(violations.Any(v =>
            v.SourceType == typeof(SalesInvoice).FullName
            && v.ForbiddenReferences.Contains(typeof(Money).FullName)), Is.False);
    }

    [Test]
    public void CheckContextAllowOnlyContract_ExcludedButUnallowedOtherTarget_StillViolates()
    {
        var contract = new ArchitectureContextAllowOnlyContract
        {
            Name = "sales-allow-only-same-domain-excluding-shared-kernel",
            Source = new ArchitectureContextSelector { Role = "DomainLayer", Metadata = new Dictionary<string, object> { ["domain"] = "Sales" } },
            Allowed = new List<ArchitectureContextSelector>
            {
                new() { Role = "DomainLayer", Metadata = new Dictionary<string, object> { ["domain"] = "Sales" } }
            },
            Exclude = new List<ArchitectureContextSelector> { new() { Role = "SharedKernel" } }
        };
        var runner = new ArchitectureContractRunner(CreateContext(), CreateDocument(contract));

        List<ArchitectureViolation> violations = runner.Session.CheckContextAllowOnlyContract(contract);

        Assert.That(violations.Any(v =>
            v.SourceType == typeof(SalesInvoiceReferencingInventoryAndSharedKernel).FullName
            && v.ForbiddenReferences.Contains(typeof(InventoryStockItem).FullName)), Is.True);
    }

    [Test]
    public void CheckContextAllowOnlyContract_UnclassifiedReferencedType_IsNotAViolationCandidate()
    {
        // Mirrors CheckAllowOnlyContract's existing behavior of only considering already-classified
        // reference targets - an unclassified type cannot match any selector, so reporting it would
        // be unrelated noise rather than a real contextual finding.
        var contract = new ArchitectureContextAllowOnlyContract
        {
            Name = "domainless-source-allow-only",
            Source = new ArchitectureContextSelector { Role = "DomainLayer" },
            Allowed = new List<ArchitectureContextSelector>
            {
                new() { Role = "DomainLayer", Metadata = new Dictionary<string, object> { ["domain"] = "Sales" } }
            }
        };
        var runner = new ArchitectureContractRunner(CreateContext(), CreateDocument(contract));

        List<ArchitectureViolation> violations = runner.Session.CheckContextAllowOnlyContract(contract);

        Assert.That(violations.Any(v =>
            v.SourceType == typeof(DomainlessSourceType).FullName
            && v.ForbiddenReferences.Contains(typeof(PlainUnclassifiedType).FullName)), Is.False);
    }

    [Test]
    public void CheckContextAllowOnlyContract_MissingTargetMetadata_ExactAllowedConstraintDoesNotMatch_TargetStillReported()
    {
        var contract = new ArchitectureContextAllowOnlyContract
        {
            Name = "sales-allow-only-inventory",
            Source = new ArchitectureContextSelector { Role = "DomainLayer", Metadata = new Dictionary<string, object> { ["domain"] = "Sales" } },
            Allowed = new List<ArchitectureContextSelector>
            {
                new() { Role = "DomainLayer", Metadata = new Dictionary<string, object> { ["domain"] = "Inventory" } }
            }
        };
        var runner = new ArchitectureContractRunner(CreateContext(), CreateDocument(contract));

        List<ArchitectureViolation> violations = runner.Session.CheckContextAllowOnlyContract(contract);

        // SourceReferencingDomainlessTarget -> DomainlessTargetType: target has role DomainLayer but
        // no "domain" metadata key, so it cannot match the allowed selector's exact constraint and is
        // reported as a violation (it is role-classified, so it is a meaningful candidate).
        Assert.That(violations.Any(v =>
            v.SourceType == typeof(SourceReferencingDomainlessTarget).FullName
            && v.ForbiddenReferences.Contains(typeof(DomainlessTargetType).FullName)), Is.True);
    }

    [Test]
    public void CheckContextAllowOnlyContract_IgnoredViolation_SuppressesMatchingFinding()
    {
        ArchitectureContextAllowOnlyContract contract = SalesAllowOnlyContract();
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

        List<ArchitectureViolation> violations = runner.Session.CheckContextAllowOnlyContract(contract);

        Assert.That(violations.Any(v => v.SourceType == typeof(SalesCheckout).FullName), Is.False);
    }

    [Test]
    public void CheckContextAllowOnlyContract_ContractNotSelected_ReturnsNoViolations()
    {
        ArchitectureContextAllowOnlyContract contract = SalesAllowOnlyContract("sales-allow-only-2");
        var runner = new ArchitectureContractRunner(
            CreateContext(), CreateDocument(contract), selectedContractIds: new HashSet<string> { "some-other-contract" });

        List<ArchitectureViolation> violations = runner.Session.CheckContextAllowOnlyContract(contract);

        Assert.That(violations, Is.Empty);
    }

    [Test]
    public void ArchitectureContractFamilyRegistry_StrictContextAllowOnly_FailsBuildOnViolation()
    {
        ArchitectureContextAllowOnlyContract contract = SalesAllowOnlyContract();
        var document = CreateDocument(contract, "strict_context_allow_only");
        var runner = new ArchitectureContractRunner(CreateContext(), document);

        Assert.That(document.Contracts.StrictContextAllowOnly, Has.Count.EqualTo(1),
            "Contract must be registered under strict_context_allow_only, the build-failing group.");

        List<ArchitectureViolation> violations = runner.Session.CheckContextAllowOnlyContract(
            document.Contracts.StrictContextAllowOnly[0]);

        Assert.That(violations, Is.Not.Empty, "strict_context_allow_only violations must be reportable as build-failing.");
    }

    [Test]
    public void CheckContextAllowOnlyContract_AuditGroup_ReportsSameViolationCondition()
    {
        ArchitectureContextAllowOnlyContract strictContract = SalesAllowOnlyContract("strict-variant");
        ArchitectureContextAllowOnlyContract auditContract = SalesAllowOnlyContract("audit-variant");

        var strictRunner = new ArchitectureContractRunner(CreateContext(), CreateDocument(strictContract, "strict_context_allow_only"));
        var auditRunner = new ArchitectureContractRunner(CreateContext(), CreateDocument(auditContract, "audit_context_allow_only"));

        List<ArchitectureViolation> strictViolations = strictRunner.Session.CheckContextAllowOnlyContract(strictContract);
        List<ArchitectureViolation> auditViolations = auditRunner.Session.CheckContextAllowOnlyContract(auditContract);

        Assert.That(strictViolations, Is.Not.Empty);
        Assert.That(auditViolations, Is.Not.Empty);
    }

    [Test]
    public void ContextAllowOnlyDiagnostic_CarriesSourceAndTargetRoleAndMetadata()
    {
        ArchitectureContextAllowOnlyContract contract = SalesAllowOnlyContract();
        var runner = new ArchitectureContractRunner(CreateContext(), CreateDocument(contract));

        List<ArchitectureViolation> violations = runner.Session.CheckContextAllowOnlyContract(contract);
        ArchitectureViolation violation = violations.First(v => v.SourceType == typeof(SalesCheckout).FullName);

        ContextAllowOnlyDiagnostic diagnostic = (ContextAllowOnlyDiagnostic)violation.Payload!.ToDiagnostic(violation);

        Assert.Multiple(() =>
        {
            Assert.That(diagnostic.Kind, Is.EqualTo(ArchitectureDiagnosticKind.ContextAllowOnly));
            Assert.That(diagnostic.SourceRole, Is.EqualTo("DomainLayer"));
            Assert.That(diagnostic.SourceMetadata!["domain"], Is.EqualTo("Sales"));
            Assert.That(diagnostic.TargetRole, Is.EqualTo("DomainLayer"));
            Assert.That(diagnostic.TargetMetadata!["domain"], Is.EqualTo("Inventory"));
        });
    }

    [Test]
    public void JsonOutput_IncludesRoleAndMetadataEvidence()
    {
        ArchitectureContextAllowOnlyContract contract = SalesAllowOnlyContract();
        var runner = new ArchitectureContractRunner(CreateContext(), CreateDocument(contract));

        ArchitectureViolation violation = runner.Session.CheckContextAllowOnlyContract(contract)
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
            Assert.That(json, Does.Contain("DomainLayer"));
        });
    }

    [Test]
    public void HumanOutput_DistinguishableFromNamespaceLayerAllowOnlyViolation()
    {
        ArchitectureContextAllowOnlyContract contextContract = SalesAllowOnlyContract();
        var contextRunner = new ArchitectureContractRunner(CreateContext(), CreateDocument(contextContract));
        ArchitectureViolation contextViolation = contextRunner.Session.CheckContextAllowOnlyContract(contextContract)
            .First(v => v.SourceType == typeof(SalesCheckout).FullName);

        var namespaceViolation = new ArchitectureViolation(
            "namespace-allow-only-rule", "namespace-allow-only-id", "MyApp.Domain.Order",
            "outside allowed layers", new[] { "MyApp.Infrastructure.Gateway" });

        string humanOutput = _formatter.FormatViolationsForHumans(new[] { contextViolation, namespaceViolation });

        Assert.Multiple(() =>
        {
            Assert.That(humanOutput, Does.Contain("kind: context_allow_only"),
                "A contextual violation must be visibly tagged so it is distinguishable from a namespace/layer violation.");
            Assert.That(humanOutput, Does.Contain("source_role: DomainLayer"));

            int namespaceLineStart = humanOutput.IndexOf("MyApp.Domain.Order", StringComparison.Ordinal);
            Assert.That(namespaceLineStart, Is.GreaterThanOrEqualTo(0));
            string namespaceLine = humanOutput[namespaceLineStart..];
            Assert.That(namespaceLine, Does.Not.Contain("kind: context_allow_only"));
        });
    }
}
