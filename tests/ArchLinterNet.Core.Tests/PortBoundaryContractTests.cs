using System.Reflection;
using ArchLinterNet.Core.Contracts;
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
    }

    [Test]
    public void PortBoundaryViolation_EmitsStructuredSeamEvidence()
    {
        var violation = new ArchitectureViolation("ports", "ports", "Sales.Checkout", "direct edge", new[] { "Catalog.Order" })
        {
            Payload = new PortBoundaryPayload("ApplicationLayer", new Dictionary<string, object> { ["domain"] = "Sales" },
                "DomainLayer", new Dictionary<string, object> { ["domain"] = "Catalog" }, "direct_reference", "role:Port")
        };

        string json = new ArchitectureDiagnosticFormatter().FormatViolationsForCiArtifacts("ports", "ports", new[] { violation });

        Assert.That(json, Does.Contain("\"evidence_kind\":\"direct_reference\""));
        Assert.That(json, Does.Contain("\"expected_seam\":\"role:Port\""));
    }
}
