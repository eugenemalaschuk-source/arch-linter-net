using System.Text.Json;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class ArchitectureSarifImportedDiagnosticLocationProjectorTests
{
    [Test]
    public void Project_WithPhysicalPathAndRegion_EmitsArtifactAndRegion()
    {
        object[] locations = ArchitectureSarifImportedDiagnosticLocationProjector.Project(
            new SarifEvidenceSourceLocation(
                "src/App/External.cs",
                new SarifEvidenceSourceRegion(startLine: 12, startColumn: 3, endLine: 12, endColumn: 9)),
            "SEC100",
            "type");

        using JsonDocument document = JsonDocument.Parse(JsonSerializer.Serialize(locations));
        JsonElement physical = document.RootElement[0].GetProperty("physicalLocation");

        Assert.Multiple(() =>
        {
            Assert.That(physical.GetProperty("artifactLocation").GetProperty("uri").GetString(),
                Is.EqualTo("src/App/External.cs"));
            Assert.That(physical.GetProperty("region").GetProperty("startLine").GetInt32(), Is.EqualTo(12));
            Assert.That(physical.GetProperty("region").GetProperty("startColumn").GetInt32(), Is.EqualTo(3));
            Assert.That(physical.GetProperty("region").GetProperty("endLine").GetInt32(), Is.EqualTo(12));
            Assert.That(physical.GetProperty("region").GetProperty("endColumn").GetInt32(), Is.EqualTo(9));
        });
    }

    [Test]
    public void Project_WithPathlessAnchoredRegion_EmitsAnnotationAndLogicalLocation()
    {
        object[] locations = ArchitectureSarifImportedDiagnosticLocationProjector.Project(
            new SarifEvidenceSourceLocation(null, new SarifEvidenceSourceRegion(charOffset: 18, charLength: 4)),
            "SEC100",
            "type");

        using JsonDocument document = JsonDocument.Parse(JsonSerializer.Serialize(locations));
        JsonElement location = document.RootElement[0];

        Assert.Multiple(() =>
        {
            Assert.That(location.GetProperty("logicalLocations")[0].GetProperty("fullyQualifiedName").GetString(),
                Is.EqualTo("SEC100"));
            Assert.That(location.GetProperty("logicalLocations")[0].GetProperty("kind").GetString(),
                Is.EqualTo("type"));
            Assert.That(location.GetProperty("annotations")[0].GetProperty("charOffset").GetInt32(),
                Is.EqualTo(18));
            Assert.That(location.GetProperty("annotations")[0].GetProperty("charLength").GetInt32(),
                Is.EqualTo(4));
            Assert.That(location.TryGetProperty("physicalLocation", out _), Is.False);
        });
    }

    [Test]
    public void Project_WithInvalidOrEmptyAnchor_FallsBackToLogicalLocation()
    {
        object[] locations = ArchitectureSarifImportedDiagnosticLocationProjector.Project(
            new SarifEvidenceSourceLocation(string.Empty, new SarifEvidenceSourceRegion(startColumn: 2)),
            "SEC100",
            "type");

        using JsonDocument document = JsonDocument.Parse(JsonSerializer.Serialize(locations));
        JsonElement location = document.RootElement[0];

        Assert.Multiple(() =>
        {
            Assert.That(location.GetProperty("logicalLocations")[0].GetProperty("fullyQualifiedName").GetString(),
                Is.EqualTo("SEC100"));
            Assert.That(location.GetProperty("logicalLocations")[0].GetProperty("kind").GetString(),
                Is.EqualTo("type"));
            Assert.That(location.TryGetProperty("annotations", out _), Is.False);
            Assert.That(location.TryGetProperty("physicalLocation", out _), Is.False);
        });
    }
}
