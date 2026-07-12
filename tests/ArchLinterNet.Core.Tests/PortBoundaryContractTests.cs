using System.Reflection;
using System.Reflection.Emit;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Families;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;
using ContextualContractTestFixtures;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class PortBoundaryContractTests
{
    [Test]
    public void CheckPortBoundaryContract_AllowsApprovedPortAndRejectsDirectDomainReference()
    {
        Assembly assembly = typeof(SalesCheckout).Assembly;
        var document = new ArchitectureContractDocument
        {
            Version = 1,
            Name = "ports",
            Analysis = new ArchitectureAnalysisConfiguration { TargetAssemblies = new List<string> { assembly.GetName().Name! } },
            Classification = new ArchitectureClassificationConfiguration
            {
                Attributes =
                {
                    new ArchitectureAttributeClassificationMapping { Attribute = "ContextualContractTestFixtures.ContextDomainMarkerAttribute", Role = "DomainLayer", Metadata = new Dictionary<string, object> { ["domain"] = "constructor[0]" } },
                    new ArchitectureAttributeClassificationMapping { Attribute = "ContextualContractTestFixtures.ContextPortMarkerAttribute", Role = "Port", Metadata = new Dictionary<string, object> { ["domain"] = "constructor[0]" } },
                    new ArchitectureAttributeClassificationMapping { Attribute = "ContextualContractTestFixtures.ContextAdapterMarkerAttribute", Role = "Adapter", Metadata = new Dictionary<string, object> { ["domain"] = "constructor[0]" } },
                }
            }
        };
        var contract = new ArchitecturePortBoundaryContract
        {
            Name = "sales-to-inventory-through-port",
            Source = new ArchitectureContextSelector { Role = "DomainLayer", Metadata = new Dictionary<string, object> { ["domain"] = "Sales" } },
            TargetContext = new ArchitectureContextMetadataSelector { Metadata = new Dictionary<string, object> { ["domain"] = "Inventory" } },
            AllowedSeams = new List<ArchitectureContextSelector> { new() { Role = "Port", Metadata = new Dictionary<string, object> { ["domain"] = "Inventory" } } },
            Forbidden = new List<ArchitectureContextSelector> { new() { Role = "DomainLayer", Metadata = new Dictionary<string, object> { ["domain"] = "Inventory" } } },
            Reason = "Use the reviewed port."
        };
        document.Contracts.StrictPortBoundaries.Add(contract);
        var runner = new ArchitectureContractRunner(new ArchitectureAnalysisContext("/tmp", new[] { assembly }, Array.Empty<string>(), Array.Empty<string>()), document);

        List<ArchitectureViolation> violations = runner.Session.CheckPortBoundaryContract(contract);

        Assert.That(violations.Any(v => v.SourceType == typeof(SalesCheckout).FullName && v.ForbiddenReferences.Contains(typeof(InventoryStockItem).FullName)), Is.True);
        Assert.That(violations.Any(v => v.SourceType == typeof(SalesUsesInventoryPort).FullName), Is.False);
    }

    [Test]
    public void CheckPortBoundaryContract_TargetMatchingNeitherAllowedSeamNorForbidden_IsReported()
    {
        // Regression: a target in the selected target_context that matches neither `allowed_seams`
        // nor `forbidden` must still be reported - permitting it silently would turn the allow-list
        // rule into a deny-list (only `forbidden` targets flagged).
        Assembly assembly = typeof(SalesCheckout).Assembly;
        var document = new ArchitectureContractDocument
        {
            Version = 1,
            Name = "ports",
            Analysis = new ArchitectureAnalysisConfiguration { TargetAssemblies = new List<string> { assembly.GetName().Name! } },
            Classification = new ArchitectureClassificationConfiguration
            {
                Attributes =
                {
                    new ArchitectureAttributeClassificationMapping { Attribute = "ContextualContractTestFixtures.ContextDomainMarkerAttribute", Role = "DomainLayer", Metadata = new Dictionary<string, object> { ["domain"] = "constructor[0]" } },
                    new ArchitectureAttributeClassificationMapping { Attribute = "ContextualContractTestFixtures.ContextPortMarkerAttribute", Role = "Port", Metadata = new Dictionary<string, object> { ["domain"] = "constructor[0]" } },
                    new ArchitectureAttributeClassificationMapping { Attribute = "ContextualContractTestFixtures.ContextAdapterMarkerAttribute", Role = "Adapter", Metadata = new Dictionary<string, object> { ["domain"] = "constructor[0]" } },
                }
            }
        };
        var contract = new ArchitecturePortBoundaryContract
        {
            Name = "sales-to-inventory-through-port",
            Source = new ArchitectureContextSelector { Role = "DomainLayer", Metadata = new Dictionary<string, object> { ["domain"] = "Sales" } },
            TargetContext = new ArchitectureContextMetadataSelector { Metadata = new Dictionary<string, object> { ["domain"] = "Inventory" } },
            AllowedSeams = new List<ArchitectureContextSelector> { new() { Role = "Port", Metadata = new Dictionary<string, object> { ["domain"] = "Inventory" } } },
            Forbidden = new List<ArchitectureContextSelector> { new() { Role = "DomainLayer", Metadata = new Dictionary<string, object> { ["domain"] = "Inventory" } } },
            Reason = "Use the reviewed port."
        };
        document.Contracts.StrictPortBoundaries.Add(contract);
        var runner = new ArchitectureContractRunner(new ArchitectureAnalysisContext("/tmp", new[] { assembly }, Array.Empty<string>(), Array.Empty<string>()), document);

        List<ArchitectureViolation> violations = runner.Session.CheckPortBoundaryContract(contract);

        Assert.That(violations.Any(v =>
            v.SourceType == typeof(SalesReferencesInventoryAdapter).FullName
            && v.ForbiddenReferences.Contains(typeof(InventoryLegacyAdapter).FullName)), Is.True);
    }

    [Test]
    public void CheckPortBoundaryContract_AclScenario_AllowsApprovedAclAndRejectsDirectDatabaseAdapter()
    {
        // End-to-end anti-corruption-layer scenario per spec.md "Anti-corruption boundaries use
        // explicit selected seams": an approved ACL seam is permitted, a direct database/adapter
        // reference is forbidden.
        Assembly assembly = typeof(LegacyCrmUsesApprovedAcl).Assembly;
        var document = new ArchitectureContractDocument
        {
            Version = 1,
            Name = "acl",
            Analysis = new ArchitectureAnalysisConfiguration { TargetAssemblies = new List<string> { assembly.GetName().Name! } },
            Classification = new ArchitectureClassificationConfiguration
            {
                Attributes =
                {
                    new ArchitectureAttributeClassificationMapping { Attribute = "ContextualContractTestFixtures.ContextDomainMarkerAttribute", Role = "DomainLayer", Metadata = new Dictionary<string, object> { ["domain"] = "constructor[0]" } },
                    new ArchitectureAttributeClassificationMapping { Attribute = "ContextualContractTestFixtures.ContextAclMarkerAttribute", Role = "AntiCorruptionLayer", Metadata = new Dictionary<string, object> { ["domain"] = "constructor[0]" } },
                    new ArchitectureAttributeClassificationMapping { Attribute = "ContextualContractTestFixtures.ContextAdapterMarkerAttribute", Role = "Adapter", Metadata = new Dictionary<string, object> { ["domain"] = "constructor[0]" } },
                }
            }
        };
        var contract = new ArchitecturePortBoundaryContract
        {
            Name = "legacy-crm-through-acl",
            Source = new ArchitectureContextSelector { Role = "DomainLayer", Metadata = new Dictionary<string, object> { ["domain"] = "LegacySales" } },
            TargetContext = new ArchitectureContextMetadataSelector { Metadata = new Dictionary<string, object> { ["domain"] = "Legacy" } },
            AllowedSeams = new List<ArchitectureContextSelector> { new() { Role = "AntiCorruptionLayer", Metadata = new Dictionary<string, object> { ["domain"] = "Legacy" } } },
            Forbidden = new List<ArchitectureContextSelector> { new() { Role = "Adapter", Metadata = new Dictionary<string, object> { ["domain"] = "Legacy" } } },
            Reason = "Legacy CRM must go through the reviewed ACL, not the raw database adapter."
        };
        document.Contracts.StrictPortBoundaries.Add(contract);
        var runner = new ArchitectureContractRunner(new ArchitectureAnalysisContext("/tmp", new[] { assembly }, Array.Empty<string>(), Array.Empty<string>()), document);

        List<ArchitectureViolation> violations = runner.Session.CheckPortBoundaryContract(contract);

        Assert.That(violations.Any(v => v.SourceType == typeof(LegacyCrmUsesApprovedAcl).FullName), Is.False);
        Assert.That(violations.Any(v =>
            v.SourceType == typeof(LegacyCrmReferencesDatabaseAdapterDirectly).FullName
            && v.ForbiddenReferences.Contains(typeof(LegacyCrmDatabaseAdapter).FullName)), Is.True);
    }

    [Test]
    public void CheckPortBoundaryContract_UnloadableFieldReference_ReportsUnsupportedEvidenceAndFailsClosed()
    {
        // Regression (#306 review, Critical): a member whose type comes from a missing/unresolvable
        // dependency assembly must not be silently dropped from the direct-edge scan - the source's
        // real reference set may be incomplete, so a forbidden edge could vanish with no violation.
        // The checker must report this explicitly (fail closed) instead of passing on partial evidence.
        using UnloadableFieldFixture fixture = UnloadableFieldFixture.Create(typeBuilder =>
        {
            ConstructorInfo ctor = typeof(ContextAdapterMarkerAttribute).GetConstructor(new[] { typeof(string) })!;
            typeBuilder.SetCustomAttribute(new CustomAttributeBuilder(ctor, new object[] { "Payment" }));
        });

        var document = new ArchitectureContractDocument
        {
            Version = 1,
            Name = "ports",
            Analysis = new ArchitectureAnalysisConfiguration { TargetAssemblies = new List<string> { fixture.ConsumerAssembly.GetName().Name! } },
            Classification = new ArchitectureClassificationConfiguration
            {
                Attributes =
                {
                    new ArchitectureAttributeClassificationMapping { Attribute = "ContextualContractTestFixtures.ContextAdapterMarkerAttribute", Role = "Adapter", Metadata = new Dictionary<string, object> { ["domain"] = "constructor[0]" } },
                }
            }
        };
        var contract = new ArchitecturePortBoundaryContract
        {
            Name = "adapter-direct-edges",
            Source = new ArchitectureContextSelector { Role = "Adapter" },
            TargetContext = new ArchitectureContextMetadataSelector { Metadata = new Dictionary<string, object> { ["domain"] = "Payment" } },
            AllowedSeams = new List<ArchitectureContextSelector> { new() { Role = "Port" } },
            Forbidden = new List<ArchitectureContextSelector> { new() { Role = "DomainLayer" } },
            Reason = "Test."
        };
        document.Contracts.StrictPortBoundaries.Add(contract);
        var runner = new ArchitectureContractRunner(
            new ArchitectureAnalysisContext("/tmp", new[] { fixture.ConsumerAssembly }, Array.Empty<string>(), Array.Empty<string>()), document);

        List<ArchitectureViolation> violations = runner.Session.CheckPortBoundaryContract(contract);

        ArchitectureViolation violation = violations.Single(v => v.SourceType == fixture.SourceType.FullName);
        ArchitectureDiagnostic mapped = ArchitectureDiagnosticMapper.FromViolation(violation);
        var diagnostic = (PortBoundaryDiagnostic)mapped;
        Assert.That(diagnostic.EvidenceKind, Is.EqualTo("unsupported_evidence"));
    }

    [Test]
    public void CheckPortBoundaryContract_AdapterWithNoInterfaces_DoesNotThrowAndReportsMismatch()
    {
        // Regression: an adapter with zero implemented interfaces must not throw a
        // NullReferenceException when the mismatch violation payload is built.
        Assembly assembly = typeof(InterfacelessPaymentAdapter).Assembly;
        var document = new ArchitectureContractDocument
        {
            Version = 1,
            Name = "adapter",
            Analysis = new ArchitectureAnalysisConfiguration { TargetAssemblies = new List<string> { assembly.GetName().Name! } },
            Classification = new ArchitectureClassificationConfiguration
            {
                Attributes =
            {
                new ArchitectureAttributeClassificationMapping { Attribute = "ContextualContractTestFixtures.ContextPortMarkerAttribute", Role = "Port", Metadata = new Dictionary<string, object> { ["domain"] = "constructor[0]" } },
                new ArchitectureAttributeClassificationMapping { Attribute = "ContextualContractTestFixtures.ContextAdapterMarkerAttribute", Role = "Adapter", Metadata = new Dictionary<string, object> { ["domain"] = "constructor[0]" } },
            }
            }
        };
        var contract = new ArchitecturePortBoundaryContract
        {
            Name = "payment-adapter",
            Source = new ArchitectureContextSelector { Role = "Adapter" },
            TargetContext = new ArchitectureContextMetadataSelector { Metadata = new Dictionary<string, object> { ["domain"] = "Payment" } },
            AllowedSeams = new List<ArchitectureContextSelector> { new() { Role = "Port" } },
            Forbidden = new List<ArchitectureContextSelector> { new() { Role = "Adapter" } },
            Reason = "r",
            AdapterBindings = new List<ArchitectureAdapterPortBinding>
            {
                new() { Adapter = new ArchitectureContextSelector { Role = "Adapter", Metadata = new Dictionary<string, object> { ["domain"] = "Payment" } }, ExpectedPort = new ArchitectureContextSelector { Role = "Port", Metadata = new Dictionary<string, object> { ["domain"] = "Payment" } } }
            }
        };
        document.Contracts.StrictPortBoundaries.Add(contract);
        var runner = new ArchitectureContractRunner(new ArchitectureAnalysisContext("/tmp", new[] { assembly }, Array.Empty<string>(), Array.Empty<string>()), document);

        List<ArchitectureViolation> violations = null!;
        Assert.DoesNotThrow(() => violations = runner.Session.CheckPortBoundaryContract(contract));

        ArchitectureViolation violation = violations.Single(v => v.SourceType == typeof(InterfacelessPaymentAdapter).FullName);
        ArchitectureDiagnostic mapped = ArchitectureDiagnosticMapper.FromViolation(violation);
        var diagnostic = (PortBoundaryDiagnostic)mapped;
        Assert.That(diagnostic.TargetRole, Is.Null);
        Assert.That(diagnostic.ForbiddenReferences, Does.Contain("no implemented interface"));
    }

    [Test]
    public void CheckPortBoundaryContract_ReportsAdapterPortMismatch()
    {
        Assembly assembly = typeof(StripePaymentAdapter).Assembly;
        var document = new ArchitectureContractDocument
        {
            Version = 1,
            Name = "adapter",
            Analysis = new ArchitectureAnalysisConfiguration { TargetAssemblies = new List<string> { assembly.GetName().Name! } },
            Classification = new ArchitectureClassificationConfiguration
            {
                Attributes =
            {
                new ArchitectureAttributeClassificationMapping { Attribute = "ContextualContractTestFixtures.ContextPortMarkerAttribute", Role = "Port", Metadata = new Dictionary<string, object> { ["domain"] = "constructor[0]" } },
                new ArchitectureAttributeClassificationMapping { Attribute = "ContextualContractTestFixtures.ContextAdapterMarkerAttribute", Role = "Adapter", Metadata = new Dictionary<string, object> { ["domain"] = "constructor[0]" } },
            }
            }
        };
        var contract = new ArchitecturePortBoundaryContract
        {
            Name = "payment-adapter",
            Source = new ArchitectureContextSelector { Role = "Adapter" },
            TargetContext = new ArchitectureContextMetadataSelector { Metadata = new Dictionary<string, object> { ["domain"] = "Payment" } },
            AllowedSeams = new List<ArchitectureContextSelector> { new() { Role = "Port" } },
            Forbidden = new List<ArchitectureContextSelector> { new() { Role = "Adapter" } },
            Reason = "r",
            AdapterBindings = new List<ArchitectureAdapterPortBinding>
            {
                new() { Adapter = new ArchitectureContextSelector { Role = "Adapter", Metadata = new Dictionary<string, object> { ["domain"] = "Payment" } }, ExpectedPort = new ArchitectureContextSelector { Role = "Port", Metadata = new Dictionary<string, object> { ["domain"] = "Payment" } } }
            }
        };
        document.Contracts.StrictPortBoundaries.Add(contract);
        var runner = new ArchitectureContractRunner(new ArchitectureAnalysisContext("/tmp", new[] { assembly }, Array.Empty<string>(), Array.Empty<string>()), document);

        List<ArchitectureViolation> violations = runner.Session.CheckPortBoundaryContract(contract);

        Assert.That(violations.Any(v => v.SourceType == typeof(MismatchedPaymentAdapter).FullName), Is.True);
        Assert.That(violations.Any(v => v.SourceType == typeof(StripePaymentAdapter).FullName), Is.False);
        ArchitectureDiagnostic mapped = ArchitectureDiagnosticMapper.FromViolation(
            violations.Single(v => v.SourceType == typeof(MismatchedPaymentAdapter).FullName));
        Assert.That(mapped, Is.TypeOf<PortBoundaryDiagnostic>());
        var diagnostic = (PortBoundaryDiagnostic)mapped;
        Assert.That(diagnostic.TargetRole, Is.EqualTo("Port"));
        Assert.That(diagnostic.TargetMetadata!["domain"], Is.EqualTo("Catalog"));
        Assert.That(diagnostic.ForbiddenReferences, Does.Contain(typeof(ICatalogPort).FullName));
        Assert.That(diagnostic.RemediationHint, Does.Contain("Implement the expected port"));
    }

    [Test]
    public void AdapterBindingIgnore_KeyedByActualMismatchEvidence_SuppressesViolation()
    {
        // Regression: the ignore/baseline suppression key must be the actual reported mismatch
        // evidence (the implemented-or-missing interface), not a static description of the expected
        // port - otherwise a baseline generated from a real violation could never re-match it.
        Assembly assembly = typeof(StripePaymentAdapter).Assembly;
        ArchitectureContractDocument document = CreateAdapterBindingDocument(assembly, out ArchitecturePortBoundaryContract contract);
        contract.IgnoredViolations.Add(new ArchitectureIgnoredViolation
        {
            SourceType = typeof(MismatchedPaymentAdapter).FullName!,
            ForbiddenReference = typeof(ICatalogPort).FullName!,
            Reason = "baselined mismatch"
        });
        var runner = new ArchitectureContractRunner(new ArchitectureAnalysisContext("/tmp", new[] { assembly }, Array.Empty<string>(), Array.Empty<string>()), document);

        List<ArchitectureViolation> violations = runner.Session.CheckPortBoundaryContract(contract);

        Assert.That(violations.Any(v => v.SourceType == typeof(MismatchedPaymentAdapter).FullName), Is.False);
    }

    [Test]
    public void AdapterBindingIgnore_KeyedByExpectedPortDescription_DoesNotSuppressViolation()
    {
        // Regression: an ignore entry written against the old (buggy) suppression key - a static
        // description of the expected port selector, e.g. "role:Port (domain=Payment)" - must NOT
        // suppress the violation. That key never changes when the adapter's actual implemented
        // interface changes, so keying suppression on it would silently keep suppressing a *different*
        // real mismatch than the one that was reviewed and baselined.
        Assembly assembly = typeof(StripePaymentAdapter).Assembly;
        ArchitectureContractDocument document = CreateAdapterBindingDocument(assembly, out ArchitecturePortBoundaryContract contract);
        contract.IgnoredViolations.Add(new ArchitectureIgnoredViolation
        {
            SourceType = typeof(MismatchedPaymentAdapter).FullName!,
            ForbiddenReference = "role:Port (domain=Payment)",
            Reason = "stale ignore keyed by expected-port description"
        });
        var runner = new ArchitectureContractRunner(new ArchitectureAnalysisContext("/tmp", new[] { assembly }, Array.Empty<string>(), Array.Empty<string>()), document);

        List<ArchitectureViolation> violations = runner.Session.CheckPortBoundaryContract(contract);

        Assert.That(violations.Any(v => v.SourceType == typeof(MismatchedPaymentAdapter).FullName), Is.True);
    }

    [Test]
    public void AdapterBindingMismatch_PrefersClassifiedInterfaceOverUnclassifiedOne_AsEvidence()
    {
        // Regression: when an adapter doesn't implement the expected port, the reported mismatch
        // evidence should be a RoleIndex-classified interface (meaningful port evidence) rather than
        // whichever implemented interface happens to sort first alphabetically.
        Assembly assembly = typeof(AdapterWithUnclassifiedAndWrongPortInterfaces).Assembly;
        ArchitectureContractDocument document = CreateAdapterBindingDocument(assembly, out ArchitecturePortBoundaryContract contract);
        var runner = new ArchitectureContractRunner(new ArchitectureAnalysisContext("/tmp", new[] { assembly }, Array.Empty<string>(), Array.Empty<string>()), document);

        List<ArchitectureViolation> violations = runner.Session.CheckPortBoundaryContract(contract);

        ArchitectureViolation violation = violations.Single(v => v.SourceType == typeof(AdapterWithUnclassifiedAndWrongPortInterfaces).FullName);
        Assert.That(violation.ForbiddenReferences, Does.Contain(typeof(ICatalogPort).FullName));
        Assert.That(violation.ForbiddenReferences, Does.Not.Contain(typeof(IAardvarkUnclassifiedInterface).FullName));
    }

    private static ArchitectureContractDocument CreateAdapterBindingDocument(Assembly assembly, out ArchitecturePortBoundaryContract contract)
    {
        var document = new ArchitectureContractDocument
        {
            Version = 1,
            Name = "adapter",
            Analysis = new ArchitectureAnalysisConfiguration { TargetAssemblies = new List<string> { assembly.GetName().Name! } },
            Classification = new ArchitectureClassificationConfiguration
            {
                Attributes =
                {
                    new ArchitectureAttributeClassificationMapping { Attribute = "ContextualContractTestFixtures.ContextPortMarkerAttribute", Role = "Port", Metadata = new Dictionary<string, object> { ["domain"] = "constructor[0]" } },
                    new ArchitectureAttributeClassificationMapping { Attribute = "ContextualContractTestFixtures.ContextAdapterMarkerAttribute", Role = "Adapter", Metadata = new Dictionary<string, object> { ["domain"] = "constructor[0]" } },
                }
            }
        };
        contract = new ArchitecturePortBoundaryContract
        {
            Name = "payment-adapter",
            Source = new ArchitectureContextSelector { Role = "Adapter" },
            TargetContext = new ArchitectureContextMetadataSelector { Metadata = new Dictionary<string, object> { ["domain"] = "Payment" } },
            AllowedSeams = new List<ArchitectureContextSelector> { new() { Role = "Port" } },
            Forbidden = new List<ArchitectureContextSelector> { new() { Role = "Adapter" } },
            Reason = "r",
            AdapterBindings = new List<ArchitectureAdapterPortBinding>
            {
                new() { Adapter = new ArchitectureContextSelector { Role = "Adapter", Metadata = new Dictionary<string, object> { ["domain"] = "Payment" } }, ExpectedPort = new ArchitectureContextSelector { Role = "Port", Metadata = new Dictionary<string, object> { ["domain"] = "Payment" } } }
            }
        };
        document.Contracts.StrictPortBoundaries.Add(contract);
        return document;
    }

    private static readonly string[] _catalogOrderReference = { "Catalog.Order" };

    private static ArchitectureViolation CreateDirectEdgeSeamEvidenceViolation() =>
        new("ports", "ports", "Sales.Checkout", "direct edge", _catalogOrderReference)
        {
            Payload = new PortBoundaryPayload("ApplicationLayer", new Dictionary<string, object> { ["domain"] = "Sales" },
                "DomainLayer", new Dictionary<string, object> { ["domain"] = "Catalog" }, "direct_reference", "role:Port",
                "Depend on the approved port abstraction.")
        };

    [Test]
    public void PortBoundaryViolation_EmitsStructuredSeamEvidence()
    {
        ArchitectureViolation violation = CreateDirectEdgeSeamEvidenceViolation();

        string json = new ArchitectureDiagnosticFormatter().FormatViolationsForCiArtifacts("ports", "ports", new[] { violation });

        Assert.That(json, Does.Contain("\"evidence_kind\":\"direct_reference\""));
        Assert.That(json, Does.Contain("\"expected_seam\":\"role:Port\""));
        Assert.That(json, Does.Contain("\"remediation_hint\":\"Depend on the approved port abstraction.\""));
    }

    [Test]
    public void PortBoundaryViolation_HumanReadableFormat_IncludesSeamEvidence()
    {
        ArchitectureViolation violation = CreateDirectEdgeSeamEvidenceViolation();
        ArchitectureDiagnostic diagnostic = ArchitectureDiagnosticMapper.FromViolation(violation);
        Assert.That(diagnostic.Kind, Is.EqualTo(ArchitectureDiagnosticKind.PortBoundary));

        string text = new ArchitectureDiagnosticFormatter().FormatViolationsForHumans(new[] { violation });

        Assert.That(text, Does.Contain("kind: port_boundary"));
        Assert.That(text, Does.Contain("expected_seam: role:Port"));
        Assert.That(text, Does.Contain("remediation: Depend on the approved port abstraction."));
    }

    [Test]
    public void CheckPortBoundaryContract_ForbiddenSelectorWinsOverAllowedSeam()
    {
        ArchitecturePortBoundaryContract contract = CreateInventoryContract();
        contract.Forbidden = new List<ArchitectureContextSelector>
        {
            new() { Role = "Port", Metadata = new Dictionary<string, object> { ["domain"] = "Inventory" } }
        };
        var runner = CreateRunner(contract);

        List<ArchitectureViolation> violations = runner.Session.CheckPortBoundaryContract(contract);

        Assert.That(violations.Any(v => v.SourceType == typeof(SalesUsesInventoryPort).FullName), Is.True);
    }

    [Test]
    public void CheckPortBoundaryContract_TargetContextNotEqualOperator_UsesReferencedSourceKey()
    {
        ArchitecturePortBoundaryContract contract = CreateInventoryContract();
        contract.TargetContext = new ArchitectureContextMetadataSelector
        {
            Metadata = new Dictionary<string, object> { ["domain"] = "!{source.metadata.otherDomain}" }
        };
        var runner = CreateRunner(contract, new Dictionary<string, object> { ["domain"] = "constructor[0]", ["otherDomain"] = "Sales" });

        List<ArchitectureViolation> violations = runner.Session.CheckPortBoundaryContract(contract);

        Assert.That(violations.Any(v => v.SourceType == typeof(SalesCheckout).FullName), Is.True);
    }

    [Test]
    public void CheckPortBoundaryContract_TargetContextMetadataList_MatchesAnyListedValue()
    {
        ArchitecturePortBoundaryContract contract = CreateInventoryContract();
        contract.TargetContext = new ArchitectureContextMetadataSelector
        {
            Metadata = new Dictionary<string, object> { ["domain"] = new List<object> { "Inventory", "Warehouse" } }
        };
        var runner = CreateRunner(contract);

        List<ArchitectureViolation> violations = runner.Session.CheckPortBoundaryContract(contract);

        Assert.That(violations.Any(v => v.SourceType == typeof(SalesCheckout).FullName), Is.True);
    }

    [Test]
    public void CheckPortBoundaryContract_ExcludeSuppressesForbiddenTarget()
    {
        ArchitecturePortBoundaryContract contract = CreateInventoryContract();
        contract.Exclude = new List<ArchitectureContextSelector>
        {
            new() { Role = "DomainLayer", Metadata = new Dictionary<string, object> { ["domain"] = "Inventory" } }
        };
        var runner = CreateRunner(contract);

        List<ArchitectureViolation> violations = runner.Session.CheckPortBoundaryContract(contract);

        Assert.That(violations.Any(v => v.SourceType == typeof(SalesCheckout).FullName), Is.False);
    }

    [Test]
    public void CheckPortBoundaryContract_ClosedGenericDomainTargetIsForbidden()
    {
        // Regression: a direct reference to a closed constructed generic domain type (forbidden by
        // role) must still be recognized and reported, not silently pass because the closed type
        // never equals the open generic type definition RoleIndex actually classified.
        ArchitecturePortBoundaryContract contract = CreateInventoryContract();
        var runner = CreateRunner(contract);

        List<ArchitectureViolation> violations = runner.Session.CheckPortBoundaryContract(contract);

        Assert.That(violations.Any(v =>
            v.SourceType == typeof(SalesReferencesGenericInventoryItem).FullName
            && v.ForbiddenReferences.Contains(typeof(GenericInventoryItem<SalesOrder>).FullName)), Is.True);
    }

    [Test]
    public void CheckPortBoundaryContract_AdapterImplementingClosedGenericExpectedPort_NoMismatch()
    {
        // Regression: an adapter binding's ExpectedPort selector must match against a closed
        // constructed generic interface the same way any other selector match does.
        Assembly assembly = typeof(GenericPaymentAdapter).Assembly;
        ArchitectureContractDocument document = CreateAdapterBindingDocument(assembly, out ArchitecturePortBoundaryContract contract);
        var runner = new ArchitectureContractRunner(new ArchitectureAnalysisContext("/tmp", new[] { assembly }, Array.Empty<string>(), Array.Empty<string>()), document);

        List<ArchitectureViolation> violations = runner.Session.CheckPortBoundaryContract(contract);

        Assert.That(violations.Any(v => v.SourceType == typeof(GenericPaymentAdapter).FullName), Is.False);
    }

    private static ArchitecturePortBoundaryContract CreateInventoryContract() => new()
    {
        Name = "sales-to-inventory-through-port",
        Source = new ArchitectureContextSelector { Role = "DomainLayer", Metadata = new Dictionary<string, object> { ["domain"] = "Sales" } },
        TargetContext = new ArchitectureContextMetadataSelector { Metadata = new Dictionary<string, object> { ["domain"] = "Inventory" } },
        AllowedSeams = new List<ArchitectureContextSelector> { new() { Role = "Port", Metadata = new Dictionary<string, object> { ["domain"] = "Inventory" } } },
        Forbidden = new List<ArchitectureContextSelector> { new() { Role = "DomainLayer", Metadata = new Dictionary<string, object> { ["domain"] = "Inventory" } } },
        Reason = "Use the reviewed port."
    };

    private static ArchitectureContractRunner CreateRunner(ArchitecturePortBoundaryContract contract,
        Dictionary<string, object>? domainMetadata = null)
    {
        Assembly assembly = typeof(SalesCheckout).Assembly;
        var document = new ArchitectureContractDocument
        {
            Version = 1,
            Name = "ports",
            Analysis = new ArchitectureAnalysisConfiguration
            {
                TargetAssemblies = new List<string> { assembly.GetName().Name! }
            },
            Classification = new ArchitectureClassificationConfiguration
            {
                Attributes =
                {
                    new ArchitectureAttributeClassificationMapping
                    {
                        Attribute = "ContextualContractTestFixtures.ContextDomainMarkerAttribute",
                        Role = "DomainLayer",
                        Metadata = domainMetadata ?? new Dictionary<string, object> { ["domain"] = "constructor[0]" }
                    },
                    new ArchitectureAttributeClassificationMapping
                    {
                        Attribute = "ContextualContractTestFixtures.ContextPortMarkerAttribute",
                        Role = "Port",
                        Metadata = new Dictionary<string, object> { ["domain"] = "constructor[0]" }
                    },
                }
            }
        };
        document.Contracts.StrictPortBoundaries.Add(contract);
        return new ArchitectureContractRunner(new ArchitectureAnalysisContext("/tmp", new[] { assembly }, Array.Empty<string>(), Array.Empty<string>()), document);
    }
}
