using System.Text.Json;
using System.Text.Json.Nodes;
using ArchLinterNet.Core.BuildState;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;
using ArchLinterNet.Testing;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class ArchitectureRemediationHintTests
{
    [Test]
    public void ProviderRegistry_CoversEverySealedDiagnosticSubtype()
    {
        List<Type> expectedTypes = typeof(ArchitectureDiagnostic).Assembly.GetTypes()
            .Where(type => type.IsSealed && !type.IsAbstract && typeof(ArchitectureDiagnostic).IsAssignableFrom(type))
            .OrderBy(type => type.Name, StringComparer.Ordinal)
            .ToList();
        List<Type> registeredTypes = ArchitectureRemediationHintProviderRegistry.All
            .Select(entry => entry.DiagnosticType)
            .OrderBy(type => type.Name, StringComparer.Ordinal)
            .ToList();

        Assert.That(registeredTypes, Is.EqualTo(expectedTypes));
    }

    [Test]
    public void FromDiagnostic_ExternalSubtype_RemainsCompatibleWithoutFabricatedGuidance()
    {
        var diagnostic = new UnregisteredDiagnostic("contract", "contract-id");

        ArchitectureFinding finding = ArchitectureFindingMapper.FromDiagnostic(diagnostic);

        Assert.Multiple(() =>
        {
            Assert.That(finding.Details, Is.SameAs(diagnostic));
            Assert.That(finding.RemediationHint, Is.Null);
        });
    }

    [Test]
    public void FromViolation_PortBoundaryWithExpectedPort_UsesDeclaredPortAndPreservesCanonicalIdentity()
    {
        ArchitectureViolation identityBoundViolation = PortViolation() with
        {
            Identity = Identity("port-id", "Shared.Type", sourceAssembly: "Assembly.One", targetAssembly: "Ports", targetType: "Ports.IOrdersPort"),
        };

        ArchitectureFinding finding = ArchitectureFindingMapper.FromViolation(identityBoundViolation, "strict");
        ArchitectureRemediationHint hint = finding.RemediationHint!;

        Assert.Multiple(() =>
        {
            Assert.That(hint.Category, Is.EqualTo(ArchitectureRemediationHintCategory.UseDeclaredPort));
            Assert.That(hint.ExpectedSeamOrDirection, Is.EqualTo("role:Port, name: Orders"));
            Assert.That(hint.FindingIdentity.SourceAssembly, Is.EqualTo("Assembly.One"));
            Assert.That(hint.FindingIdentity.TargetAssembly, Is.EqualTo("Ports"));
            Assert.That(hint.Evidence.Select(evidence => evidence.Kind), Is.EqualTo(new[] { "evidence_kind", "expected_seam" }));
        });
    }

    [Test]
    public void FromViolation_PortBoundaryAdapterMismatch_UsesAdapterGuidance()
    {
        ArchitectureViolation violation = PortViolation("adapter_port_mismatch");

        ArchitectureRemediationHint hint = ArchitectureFindingMapper.FromViolation(violation).RemediationHint!;

        Assert.That(hint.Category, Is.EqualTo(ArchitectureRemediationHintCategory.IntroduceAdapter));
        Assert.That(hint.Summary, Does.Contain("adapter"));
    }

    [Test]
    public void FromDiagnostic_WithoutKnownDependencySeam_RequiresContractReviewInsteadOfInventingOne()
    {
        var diagnostic = new DependencyDiagnostic(
            "no-direct-data-access", "dependency-id", "App.Feature.Service", "App.Data", ["App.Data.Repository"]);

        ArchitectureRemediationHint hint = ArchitectureFindingMapper.FromDiagnostic(diagnostic).RemediationHint!;

        Assert.Multiple(() =>
        {
            Assert.That(hint.Category, Is.EqualTo(ArchitectureRemediationHintCategory.ReviewContract));
            Assert.That(hint.RequiresReview, Is.True);
            Assert.That(hint.Caveat, Does.Contain("No approved alternative seam"));
            Assert.That(hint.Summary, Does.Not.Contain("allow-list").IgnoreCase);
            Assert.That(hint.Summary, Does.Not.Contain("audit").IgnoreCase);
        });
    }

    [Test]
    public void FromDiagnostic_PlacementAndClassificationEvidence_UsesBoundedRepairCategories()
    {
        var placement = new TypePlacementDiagnostic(
            "handler-location", "placement-id", "App.Feature.Orders.OrderHandler", "wrong location", ["App.Feature.Orders"])
        {
            ExpectedTypeLocation = "App.Application.Orders",
            ActualTypeLocation = "App.Feature.Orders",
        };
        var attribute = new AttributeUsageDiagnostic(
            "entity-marker", "attribute-id", "App.Feature.Orders.Order", "wrong attribute location", ["Entity"])
        {
            ExpectedAttributeLocation = "App.Domain",
            ActualAttributeLocation = "App.Feature.Orders",
        };

        Assert.Multiple(() =>
        {
            Assert.That(ArchitectureFindingMapper.FromDiagnostic(placement).RemediationHint!.Category,
                Is.EqualTo(ArchitectureRemediationHintCategory.MoveCode));
            Assert.That(ArchitectureFindingMapper.FromDiagnostic(attribute).RemediationHint!.Category,
                Is.EqualTo(ArchitectureRemediationHintCategory.FixClassification));
        });
    }

    [Test]
    public void FromDiagnostic_CoverageBuildAndPolicyInput_UseFixPolicyInput()
    {
        var coverage = new DependencyDiagnostic(
            "coverage", "coverage-id", "App.NewFeature", "uncovered namespace", ["App.NewFeature.Representative"]);
        var preflight = new BuildStatePreflightDiagnostic(
            "build-state", "build-id", BuildStatePreflightState.MissingArtifact,
            new BuildStatePreflightEvidence("src/App/App.csproj", "App"));
        var policyError = new ArchitecturePolicyErrorDiagnostic(
            "A policy field is missing.", ArchitecturePolicyDiagnosticKind.SemanticValidation, "schema", []);

        Assert.Multiple(() =>
        {
            Assert.That(ArchitectureFindingMapper.FromDiagnostic(coverage).RemediationHint!.Category,
                Is.EqualTo(ArchitectureRemediationHintCategory.FixPolicyInput));
            Assert.That(ArchitectureFindingMapper.FromDiagnostic(preflight).RemediationHint!.Category,
                Is.EqualTo(ArchitectureRemediationHintCategory.FixPolicyInput));
            Assert.That(ArchitectureFindingMapper.FromDiagnostic(policyError).RemediationHint!.Category,
                Is.EqualTo(ArchitectureRemediationHintCategory.FixPolicyInput));
        });
    }

    [Test]
    public void FromDiagnostic_ExternalPackageAndFrameworkBoundaries_RecommendBoundedReplacement()
    {
        var external = new ExternalDependencyDiagnostic(
            "external", "external-id", "App.Domain", "external", ["Vendor.Client"], "vendor-sdk");
        var package = new PackageDependencyDiagnostic(
            "package", "package-id", "App.Domain", "package", ["Vendor.Client@1.0"], "vendor-sdk");
        var framework = new FrameworkReferenceDiagnostic(
            "framework", "framework-id", "App.Domain", "framework", ["Microsoft.AspNetCore.App"],
            "aspnet", [new FrameworkReferenceEvidence("Microsoft.AspNetCore.App", "net10.0", true, "src/App.csproj", null)]);

        Assert.That(
            new[]
            {
                ArchitectureFindingMapper.FromDiagnostic(external).RemediationHint!,
                ArchitectureFindingMapper.FromDiagnostic(package).RemediationHint!,
                ArchitectureFindingMapper.FromDiagnostic(framework).RemediationHint!,
            }.Select(hint => hint.Category),
            Is.EqualTo(Enumerable.Repeat(ArchitectureRemediationHintCategory.RemoveOrReplaceDependency, 3)));
    }

    [Test]
    public void FromDiagnostic_StaleIgnoreAndPublicSurface_RequireBoundedReviewWithoutAutomaticMutation()
    {
        var staleIgnore = new UnmatchedIgnoreDiagnostic(
            "ignore", "ignore-id", 0, "App.Service", "Vendor.Client", "obsolete exception");
        var publicSurface = new PublicApiSurfaceDiagnostic(
            "public-api", "api-id", "App.Api.Service", "public API", ["App.Api.Service.NewMember"])
        {
            ApiAssemblyName = "App.Api",
            ApiDeltaKind = "added",
        };

        ArchitectureRemediationHint ignoreHint = ArchitectureFindingMapper.FromDiagnostic(staleIgnore).RemediationHint!;
        ArchitectureRemediationHint apiHint = ArchitectureFindingMapper.FromDiagnostic(publicSurface).RemediationHint!;

        Assert.Multiple(() =>
        {
            Assert.That(ignoreHint.Category, Is.EqualTo(ArchitectureRemediationHintCategory.NarrowException));
            Assert.That(ignoreHint.RequiresReview, Is.True);
            Assert.That(ignoreHint.Caveat, Does.Contain("never replace").IgnoreCase);
            Assert.That(apiHint.Category, Is.EqualTo(ArchitectureRemediationHintCategory.ReviewContract));
            Assert.That(apiHint.Summary, Does.Not.Contain("rewrite").IgnoreCase);
        });
    }

    [Test]
    public void HumanJsonSarifAndTesting_ExposeEquivalentNormalizedPortHintWithoutSarifFixes()
    {
        ArchitectureViolation violation = PortViolation();
        ArchitectureFinding finding = ArchitectureFindingMapper.FromViolation(violation, "strict");
        var formatter = new ArchitectureDiagnosticFormatter();
        string human = formatter.FormatViolationsForHumans([violation]);
        JsonNode? jsonFinding = JsonNode.Parse(JsonSerializer.Serialize(
            ArchitectureDiagnosticFormatter.FormatNormalizedFindingForJson(finding)));
        ArchitectureFindingReadEnvelope readEnvelope = ArchitectureFindingJsonReader.Read(
            jsonFinding!.ToJsonString(), strict: true);
        string sarif = new ArchitectureSarifFormatter().FormatResultAsSarif(
            "strict", [violation], Array.Empty<string>(), "1.0.0");
        using JsonDocument sarifDocument = JsonDocument.Parse(sarif);
        JsonElement sarifResult = sarifDocument.RootElement.GetProperty("runs")[0].GetProperty("results")[0];
        JsonNode? sarifFinding = JsonNode.Parse(sarifResult.GetProperty("properties").GetProperty("arch_linter_net").GetRawText());
        var testingResult = new ArchitectureValidationResult(new ArchitectureValidationResultParams(
            false, [violation], Array.Empty<string>())
        { Mode = "strict" });

        Assert.Multiple(() =>
        {
            Assert.That(human, Does.Contain("remediation: use_declared_port"));
            Assert.That(JsonNode.DeepEquals(jsonFinding, sarifFinding), Is.True);
            Assert.That(readEnvelope.RawRemediationHint!.Value.ValueKind, Is.EqualTo(JsonValueKind.Object),
                readEnvelope.RawRemediationHint.Value.GetRawText());
            Assert.That(readEnvelope.RawRemediationHint!.Value.GetProperty("category").GetString(),
                Is.EqualTo("use_declared_port"));
            Assert.That(sarifResult.TryGetProperty("fixes", out _), Is.False);
            Assert.That(testingResult.Findings.Single().RemediationHint!.Category,
                Is.EqualTo(ArchitectureRemediationHintCategory.UseDeclaredPort));
        });
    }

    private static ArchitectureViolation PortViolation(string evidenceKind = "direct_edge") => new(
        "orders-boundary", "port-id", "App.Orders.OrderService", "direct dependency", ["Infrastructure.SqlOrdersGateway"])
    {
        Payload = new PortBoundaryPayload(
            "Application", new Dictionary<string, object>(), "Infrastructure", new Dictionary<string, object>(),
            evidenceKind, "role:Port, name: Orders", "Use the approved port."),
    };

    private static ArchitectureViolationIdentity Identity(
        string contractId,
        string sourceType,
        string? sourceAssembly = null,
        string? targetAssembly = null,
        string? targetType = null) => new(
        ArchitectureViolationIdentity.CurrentVersion,
        "port_boundary",
        "reference",
        contractId,
        sourceAssembly,
        sourceType,
        null,
        targetAssembly,
        targetType,
        targetType,
        0);

    private sealed record UnregisteredDiagnostic(string ContractName, string? ContractId)
        : ArchitectureDiagnostic(ContractName, ContractId)
    {
        public override ArchitectureDiagnosticKind Kind => ArchitectureDiagnosticKind.Dependency;
    }
}
