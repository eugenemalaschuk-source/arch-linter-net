using System.Text.Json;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;
using NJsonSchema;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class SarifSchemaValidationTests
{
    [Test]
    public async Task FormatFindingsAsSarif_PathlessRegion_ValidatesAgainstTheOasisSarif21Schema()
    {
        ArchitectureFinding finding = ImportedFinding(
            path: null,
            new SarifEvidenceSourceRegion(startLine: 17, startColumn: 5, charOffset: 402));

        string sarif = ArchitectureSarifFormatter.FormatFindingsAsSarif([finding], "1.2.3");
        using JsonDocument document = JsonDocument.Parse(sarif);
        JsonElement result = document.RootElement.GetProperty("runs")[0].GetProperty("results")[0];
        JsonSchema schema = await JsonSchema.FromJsonAsync(File.ReadAllText(Path.Combine(
            TestContext.CurrentContext.TestDirectory,
            "SarifSchemas",
            "sarif-schema-2.1.0.json")));

        Assert.Multiple(() =>
        {
            Assert.That(schema.Validate(sarif), Is.Empty);
            Assert.That(result.GetProperty("locations")[0].GetProperty("annotations")[0]
                .GetProperty("startLine").GetInt32(), Is.EqualTo(17));
            Assert.That(result.GetProperty("locations")[0].GetProperty("annotations")[0]
                .GetProperty("charOffset").GetInt32(), Is.EqualTo(402));
            Assert.That(result.GetProperty("locations")[0].TryGetProperty("physicalLocation", out _), Is.False);
        });
    }

    [Test]
    public async Task FormatFindingsAsSarif_EmptySourceRegion_OmitsTheInvalidSarifRegion()
    {
        ArchitectureFinding finding = ImportedFinding(
            "src/App/WithoutCoordinates.cs",
            new SarifEvidenceSourceRegion());

        string sarif = ArchitectureSarifFormatter.FormatFindingsAsSarif([finding], "1.2.3");
        using JsonDocument document = JsonDocument.Parse(sarif);
        JsonElement physicalLocation = document.RootElement.GetProperty("runs")[0].GetProperty("results")[0]
            .GetProperty("locations")[0].GetProperty("physicalLocation");
        JsonSchema schema = await JsonSchema.FromJsonAsync(File.ReadAllText(Path.Combine(
            TestContext.CurrentContext.TestDirectory,
            "SarifSchemas",
            "sarif-schema-2.1.0.json")));

        Assert.Multiple(() =>
        {
            Assert.That(schema.Validate(sarif), Is.Empty);
            Assert.That(physicalLocation.GetProperty("artifactLocation").GetProperty("uri").GetString(),
                Is.EqualTo("src/App/WithoutCoordinates.cs"));
            Assert.That(physicalLocation.TryGetProperty("region", out _), Is.False);
        });
    }

    private static ArchitectureFinding ImportedFinding(
        string? path,
        SarifEvidenceSourceRegion? region) => ArchitectureImportedDiagnosticProjector.ToFinding(
        new SarifSelectedExternalDiagnostic(
            "external-diagnostic:v2:sarif-schema-fixture",
            new SarifEvidenceSourceDiagnostic(
                "A trusted source result used for SARIF schema validation.",
                "SEC100",
                SarifEvidenceSourceSeverity.Error,
                new SarifEvidenceSourceLocation(path, region)),
            SarifExternalDiagnosticGovernanceMode.Strict,
            new SarifExternalDiagnosticFingerprint(
                SarifExternalDiagnosticFingerprintOrigin.Source,
                "source-fingerprint",
                "primary"),
            [new SarifEvidenceProvenance(
                "external.scan",
                "artifacts/analysis.sarif",
                "artifact-hash",
                "Example Analyzer",
                "1.2.3",
                "run-1",
                1,
                new SarifEvidenceResolvedContext("external.scan", "repo", "revision", "scope"))]));
}
