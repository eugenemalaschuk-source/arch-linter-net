using System.Text.Json;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

// The same normalized delta record must be visible in human, JSON, and SARIF output — a reviewer
// triaging a changed signature from CI must not have to switch formats to see what it used to be.
[TestFixture]
public sealed class PublicApiDeltaReportingParityTests
{
    private const string Signature = "method Acme.Module.Thing.Do(System.Int32): System.Boolean";
    private const string PreviousSignature = "method Acme.Module.Thing.Do(System.Int32): System.Void";

    private static ArchitectureViolation ChangedViolation()
    {
        return new ArchitectureViolation("surface", "surface", "Acme.Module.Thing", "public API surface", new[] { Signature })
        {
            Payload = new PublicApiSurfacePayload(
                UndeclaredApiSignature: Signature,
                ApiAssemblyName: "Acme.Module",
                ApiVisibility: "public",
                ApiDeltaKind: "changed",
                PreviousApiSignature: PreviousSignature),
        };
    }

    [Test]
    public void HumanOutput_CarriesDeltaKindAndPreviousSignature()
    {
        string output = new ArchitectureDiagnosticFormatter()
            .FormatViolationsForHumans(new[] { ChangedViolation() });

        Assert.Multiple(() =>
        {
            Assert.That(output, Does.Contain("reason: changed_api_signature"));
            Assert.That(output, Does.Contain("delta: changed"));
            Assert.That(output, Does.Contain($"previous_signature: {PreviousSignature}"));
        });
    }

    [Test]
    public void HumanOutput_RemovedMemberUsesRemovedReason()
    {
        ArchitectureViolation violation = new("surface", "surface", "Acme.Module.Thing", "public API surface", new[] { Signature })
        {
            Payload = new PublicApiSurfacePayload(
                UndeclaredApiSignature: Signature, ApiDeltaKind: "removed", PreviousApiSignature: Signature),
        };

        string output = new ArchitectureDiagnosticFormatter().FormatViolationsForHumans(new[] { violation });

        Assert.That(output, Does.Contain("reason: removed_api_member"));
    }

    [Test]
    public void JsonOutput_CarriesDeltaKindAndPreviousSignature()
    {
        string json = ArchitectureDiagnosticFormatter.FormatResultForCiArtifacts(
            "strict",
            passed: false,
            new[] { ChangedViolation() },
            Array.Empty<string>(),
            Array.Empty<ArchitectureCycleFinding>(),
            Array.Empty<ArchitectureClassificationRoleFact>(),
            null,
            Array.Empty<BuildStatePreflightDiagnostic>(),
            Array.Empty<ArchitectureViolation>(),
            Array.Empty<ArchitectureUnmatchedIgnoredViolation>(),
            Array.Empty<PolicyConsistencyDiagnostic>(),
            Array.Empty<ArchitectureCoverageSummary>(),
            Array.Empty<ArchitectureClassificationConflict>(),
            Array.Empty<ArchitectureClassificationMetadataFailure>());

        Assert.Multiple(() =>
        {
            Assert.That(json, Does.Contain("\"api_delta_kind\":\"changed\""));
            Assert.That(json, Does.Contain("\"previous_api_signature\""));
        });
    }

    [Test]
    public void SarifOutput_CarriesDeltaKindAndPreviousSignature()
    {
        string sarif = new ArchitectureSarifFormatter().FormatResultAsSarif(
            "strict",
            new[] { ChangedViolation() },
            Array.Empty<string>(),
            Array.Empty<BuildStatePreflightDiagnostic>(),
            "1.0.0");

        using JsonDocument document = JsonDocument.Parse(sarif);
        JsonElement properties = document.RootElement
            .GetProperty("runs")[0]
            .GetProperty("results")[0]
            .GetProperty("properties");

        Assert.Multiple(() =>
        {
            Assert.That(properties.GetProperty("api_delta_kind").GetString(), Is.EqualTo("changed"));
            Assert.That(properties.GetProperty("previous_api_signature").GetString(), Is.EqualTo(PreviousSignature));
            Assert.That(properties.GetProperty("api_signature").GetString(), Is.EqualTo(Signature));
        });
    }
}
