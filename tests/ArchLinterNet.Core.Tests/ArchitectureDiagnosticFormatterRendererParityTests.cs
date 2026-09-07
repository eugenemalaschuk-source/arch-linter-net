using System.Text.Json;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class ArchitectureDiagnosticFormatterRendererParityTests
{
    [Test]
    public void ApplicabilityFacade_PreservesCanonicalHumanProjection()
    {
        var completion = new ArchitectureAssessmentCompletionEvidence(
            ArchitectureAssessmentCompletionState.Pass,
            Array.Empty<ArchitectureApplicabilityAssessment>(),
            Array.Empty<ArchitectureApplicabilityReason>());
        var projection = new ArchitectureApplicabilityProjection(
            completion,
            new ArchitectureApplicabilitySummary(0, 0, 0, 0, 0, 0, 0),
            Array.Empty<ArchitectureFinding>());

        Assert.That(
            ArchitectureDiagnosticFormatter.FormatApplicabilityProjectionForHumans(projection),
            Is.EqualTo(string.Join(
                Environment.NewLine,
                "Assessment completion: pass; reasons: none",
                "Assessment completeness transparency (not an architecture quality score): required=0, required_evaluable=0, required_unassessable=0, evaluable=0, unassessable=0, optional=0, not_applicable=0",
                "Applicability controls:") + Environment.NewLine));
    }

    [Test]
    public void WaiverFacade_PreservesHumanAndJsonShapes()
    {
        var waiver = new ArchitectureWaiverLifecycleRecord(
            "waiver-b",
            "active",
            "contract",
            "contract-id",
            "strict",
            "Source.Type",
            "Forbidden.Type",
            null,
            "documented reason",
            null,
            null,
            new DateOnly(2026, 1, 2),
            null,
            new DateOnly(2026, 2, 3),
            true);

        string human = new ArchitectureDiagnosticFormatter().FormatWaiversForHumans([waiver]);
        string json = ArchitectureDiagnosticFormatter.AddWaiversToCiArtifacts("{\"passed\":true}", [waiver]);
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement jsonWaiver = document.RootElement.GetProperty("waivers")[0];

        Assert.Multiple(() =>
        {
            Assert.That(human, Is.EqualTo(
                "Architecture waivers:" + Environment.NewLine
                + "  [active] waiver-b: contract (Source.Type -> Forbidden.Type); target: ?; reason: documented reason; owner: ?; issue: ?; introduced: 2026-01-02; expires: "));
            Assert.That(jsonWaiver.GetProperty("id").GetString(), Is.EqualTo("waiver-b"));
            Assert.That(jsonWaiver.GetProperty("evaluation_date").GetString(), Is.EqualTo("2026-02-03"));
            Assert.That(jsonWaiver.GetProperty("matches_governed_finding").GetBoolean(), Is.True);
        });
    }

    [Test]
    public void PolicyInventoryFacade_PreservesHumanAndJsonShapes()
    {
        var inventory = new ArchitecturePolicyInventory(
            ArchitecturePolicyInventory.CurrentSchemaId,
            4,
            new ArchitecturePolicyInventoryRules(2, 1, 1),
            new ArchitecturePolicyInventoryIgnoreDebt(3, 1, 1, 1, 0, 0),
            Array.Empty<ArchitectureWaiverLifecycleRecord>());

        string human = ArchitectureDiagnosticFormatter.FormatPolicyInventoryForHumans(inventory);
        string json = ArchitectureDiagnosticFormatter.AddPolicyInventoryToCiArtifacts("{\"passed\":true}", inventory);
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement jsonInventory = document.RootElement.GetProperty("policy_inventory");

        Assert.Multiple(() =>
        {
            Assert.That(human, Is.EqualTo(
                "Policy rules       4  (strict 2, audit 1, coverage 1)" + Environment.NewLine
                + "Waiver debt       3  (1 active, 1 stale, 1 expired)"));
            Assert.That(jsonInventory.GetProperty("schema").GetString(), Is.EqualTo(ArchitecturePolicyInventory.CurrentSchemaId));
            Assert.That(jsonInventory.GetProperty("effective_rule_count").GetInt32(), Is.EqualTo(4));
            Assert.That(jsonInventory.GetProperty("ignore_debt").GetProperty("expired").GetInt32(), Is.EqualTo(1));
            Assert.That(jsonInventory.GetProperty("waivers").GetArrayLength(), Is.Zero);
        });
    }

    [Test]
    public void ImportedDiagnosticFacade_PreservesEscapingProvenanceAndGovernanceJson()
    {
        var source = new SarifEvidenceSourceDiagnostic(
            "message\nfrom producer",
            "RULE-1",
            SarifEvidenceSourceSeverity.Error,
            new SarifEvidenceSourceLocation("src/Findings.cs", new SarifEvidenceSourceRegion(10, 4)),
            "Product",
            ["security"]);
        var provenance = new SarifEvidenceProvenance(
            "external.scan",
            "artifact.sarif",
            "sha256",
            "Tool\tName",
            "1.0",
            "run-1",
            1,
            new SarifEvidenceResolvedContext("external.scan", "repo", "revision", "scope"));
        var selected = new SarifSelectedExternalDiagnostic(
            "external-diagnostic:v2:one",
            source,
            SarifExternalDiagnosticGovernanceMode.Strict,
            new SarifExternalDiagnosticFingerprint(
                SarifExternalDiagnosticFingerprintOrigin.Source,
                "fingerprint",
                "primary"),
            [provenance]);
        ArchitectureFinding finding = ArchitectureImportedDiagnosticProjector.ToFinding(selected);

        string human = ArchitectureDiagnosticFormatter.FormatFindingsForHumans([finding]);
        string json = JsonSerializer.Serialize(ArchitectureDiagnosticFormatter.FormatNormalizedFindingForJson(finding));
        using JsonDocument document = JsonDocument.Parse(json);

        Assert.Multiple(() =>
        {
            Assert.That(human, Does.Contain("Tool\\tName/RULE-1 at src/Findings.cs:10:4: message\\nfrom producer"));
            Assert.That(human, Does.Contain("governance_mode=strict"));
            Assert.That(human, Does.Contain("fingerprint=source:primary:fingerprint"));
            Assert.That(document.RootElement.GetProperty("governance_mode").GetString(), Is.EqualTo("strict"));
            Assert.That(document.RootElement.GetProperty("source_diagnostic").GetProperty("severity").GetString(), Is.EqualTo("error"));
            Assert.That(document.RootElement.GetProperty("source_diagnostic").GetProperty("fingerprint").GetProperty("name").GetString(), Is.EqualTo("primary"));
            Assert.That(document.RootElement.GetProperty("evidence_provenance")[0].GetProperty("artifact_sha256").GetString(), Is.EqualTo("sha256"));
        });
    }

    [Test]
    public void ContractSurfaceExposureFacade_PreservesPathRichHumanContext()
    {
        var violation = new ArchitectureViolation(
            "no-internal-contract-types",
            "surface-id",
            "Product.Api.OrdersContract",
            "Product.Internal",
            ["Product.Internal.OrderEntity"])
        {
            Payload = new ContractSurfaceExposurePayload(
                "Product.Api",
                "Product.Api.OrdersContract",
                "method:Get.return",
                "10:method3:Get6:return",
                "Product.Internal",
                "Product.Internal.OrderEntity",
                "exported",
                "method:OrdersController.Get",
                "reviewed-api"),
        };

        string human = new ArchitectureDiagnosticFormatter().FormatViolationsForHumans([violation]);

        Assert.Multiple(() =>
        {
            Assert.That(human, Does.Contain("source_assembly: Product.Api"));
            Assert.That(human, Does.Contain("exposure_path: method:Get.return"));
            Assert.That(human, Does.Contain("canonical_exposure_path: 10:method3:Get6:return"));
            Assert.That(human, Does.Contain("site: method:OrdersController.Get"));
            Assert.That(human, Does.Contain("reviewed_public_api_surface: reviewed-api"));
        });
    }
}
