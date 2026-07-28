using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using ArchLinterNet.Core.BuildState;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;
using ArchLinterNet.Core.Schema;
using Json.Schema;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class PackagedSchemaRegistryTests
{
    [Test]
    public void List_ReturnsEveryReleaseMatchedSchemaInOrdinalOrder()
    {
        PackagedSchemaRegistry registry = new();

        IReadOnlyList<PackagedSchemaDescriptor> schemas = registry.List();

        Assert.Multiple(() =>
        {
            Assert.That(schemas.Select(static schema => schema.LogicalId), Is.EqualTo(new[]
            {
                "analysis-build-state", "api-snapshot", "baseline", "normalized-finding", "policy-fragment", "policy-root",
            }));
            Assert.That(schemas.Single(static schema => schema.LogicalId == "baseline").DocumentVersion, Is.EqualTo("v2"));
            Assert.That(schemas.All(static schema => schema.SchemaId.Contains("/schema/0.5.1/", StringComparison.Ordinal)), Is.True);
            Assert.That(schemas.All(static schema => schema.ResourcePath.StartsWith("schema/0.5.1/", StringComparison.Ordinal)), Is.True);
            Assert.That(schemas.All(static schema => schema.Sha256.Length == 64), Is.True);
            Assert.That(schemas.All(static schema => schema.SupportsRead && schema.SupportsWrite), Is.True);
            Assert.That(schemas.All(static schema => !string.IsNullOrWhiteSpace(schema.MigrationNote)), Is.True);
            Assert.That(schemas.All(static schema => !string.IsNullOrWhiteSpace(schema.OwningCapability)), Is.True);
        });
    }

    [Test]
    public void EveryListedSchema_HasMatchingEmbeddedContentAndDigest()
    {
        PackagedSchemaRegistry registry = new();

        foreach (PackagedSchemaDescriptor descriptor in registry.List())
        {
            Assert.That(registry.TryRead(descriptor.LogicalId, out string schema), Is.True, descriptor.LogicalId);
            Assert.Multiple(() =>
            {
                Assert.That(Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(schema))), Is.EqualTo(descriptor.Sha256));
                using JsonDocument document = JsonDocument.Parse(schema);
                Assert.That(document.RootElement.GetProperty("$id").GetString(), Is.EqualTo(descriptor.SchemaId));
            });
        }
    }

    [Test]
    public void TryRead_UnknownLogicalId_ReturnsFalse()
    {
        Assert.That(new PackagedSchemaRegistry().TryRead("missing", out string schema), Is.False);
        Assert.That(schema, Is.Empty);
    }

    [Test]
    public void BuildStateSchema_ValidatesTheCurrentBuildReceiptV1Serialization()
    {
        BuildReceiptV1 receipt = new(
            "src/Product/Product.csproj", "Product", "Debug", "net10.0",
            new string('a', 64), new string('b', 64));
        string receiptJson = JsonSerializer.Serialize(receipt);

        PackagedSchemaRegistry registry = new();
        Assert.That(registry.TryRead("analysis-build-state", out string schemaText), Is.True);

        JsonSchema schema = JsonSchema.FromText(schemaText);
        using JsonDocument document = JsonDocument.Parse(receiptJson);
        Assert.That(schema.Evaluate(document.RootElement).IsValid, Is.True);
    }

    [Test]
    public void NormalizedFindingSchema_ValidatesCurrentPackageDiagnosticProjection()
    {
        var violation = new ArchitectureViolation(
            "domain-no-ef", "domain-no-ef", "Product.Domain", "forbidden package group",
            ["Example.Package@1.0.0"])
        {
            Payload = new PackageDependencyPayload("forbidden")
        };
        var formatter = new ArchitectureDiagnosticFormatter();
        using JsonDocument output = JsonDocument.Parse(
            formatter.FormatResultForCiArtifacts("strict", false, [violation], Array.Empty<string>()));
        JsonElement finding = output.RootElement.GetProperty("violations")[0];

        PackagedSchemaRegistry registry = new();
        Assert.That(registry.TryRead("normalized-finding", out string schemaText), Is.True);

        JsonSchema schema = JsonSchema.FromText(schemaText);
        Assert.That(schema.Evaluate(finding).IsValid, Is.True);

        JsonObject unknownKind = JsonNode.Parse(finding.GetRawText())!.AsObject();
        unknownKind["kind"] = "future_finding";
        unknownKind["schema_version"] = 2;
        Assert.That(schema.Evaluate(unknownKind).IsValid, Is.False);
    }

    [Test]
    public void ApiSnapshotContract_ValidatesSerializedTextAgainstThePackagedResource()
    {
        string snapshot = PublicApiSnapshotFormat.Serialize(new PublicApiSnapshotDocument(
            PublicApiSnapshotFormat.CurrentVersion,
            "public-api",
            [new PublicApiSnapshotEntry("Product", "method Product.Api Run()")]));

        PackagedSchemaRegistry registry = new();
        Assert.That(registry.TryRead("api-snapshot", out string schemaText), Is.True);
        JsonSchema schema = JsonSchema.FromText(schemaText);
        using JsonDocument arbitraryJson = JsonDocument.Parse("\"not a public API snapshot\"");

        Assert.Multiple(() =>
        {
            Assert.That(registry.TryValidateText("api-snapshot", snapshot, out string diagnostic), Is.True, diagnostic);
            Assert.That(registry.TryValidateText("api-snapshot", snapshot.Replace("@version 1", "@version 2", StringComparison.Ordinal), out diagnostic), Is.False);
            Assert.That(diagnostic, Does.Contain("unsupported snapshot version '2'"));
            Assert.That(schema.Evaluate(arbitraryJson.RootElement).IsValid, Is.False);
        });
    }

    [Test]
    public void TryValidateText_RejectsUnknownNonTextAndNonCanonicalDocuments()
    {
        string snapshot = PublicApiSnapshotFormat.Serialize(new PublicApiSnapshotDocument(
            PublicApiSnapshotFormat.CurrentVersion,
            "public-api",
            [new PublicApiSnapshotEntry("Product", "method Product.Api Run()")]));
        PackagedSchemaRegistry registry = new();

        Assert.Multiple(() =>
        {
            Assert.That(registry.TryValidateText("missing", snapshot, out string diagnostic), Is.False);
            Assert.That(diagnostic, Does.Contain("unknown"));
            Assert.That(registry.TryValidateText("baseline", snapshot, out diagnostic), Is.False);
            Assert.That(diagnostic, Does.Contain("does not define a line-oriented text format"));
            Assert.That(registry.TryValidateText("api-snapshot", snapshot.Replace("\n", "\r\n", StringComparison.Ordinal), out diagnostic), Is.False);
            Assert.That(diagnostic, Does.Contain("canonical writer form"));
            Assert.That(registry.TryValidateText("api-snapshot", snapshot.Replace("@contract public-api", "@contract ", StringComparison.Ordinal), out diagnostic), Is.False);
            Assert.That(diagnostic, Does.Contain("does not satisfy the packaged API snapshot contract"));
            Assert.That(registry.TryValidateText("api-snapshot", PublicApiSnapshotFormat.Serialize(new PublicApiSnapshotDocument(
                PublicApiSnapshotFormat.CurrentVersion, "empty", [])), out diagnostic), Is.True, diagnostic);
        });
    }
}
