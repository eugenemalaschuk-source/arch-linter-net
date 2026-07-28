using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ArchLinterNet.Core.BuildState;
using ArchLinterNet.Core.Contracts;
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
                "analysis-build-state", "api-snapshot", "baseline", "policy-fragment", "policy-root",
            }));
            Assert.That(schemas.Single(static schema => schema.LogicalId == "baseline").DocumentVersion, Is.EqualTo("v2"));
            Assert.That(schemas.All(static schema => schema.SchemaId.Contains("/schema/0.5.1/", StringComparison.Ordinal)), Is.True);
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
    public void ApiSnapshotDescriptor_DeclaresTheActualTextFormat()
    {
        string snapshot = PublicApiSnapshotFormat.Serialize(new PublicApiSnapshotDocument(
            PublicApiSnapshotFormat.CurrentVersion,
            "public-api",
            [new PublicApiSnapshotEntry("Product", "method Product.Api Run()")]));

        PackagedSchemaRegistry registry = new();
        Assert.That(registry.TryRead("api-snapshot", out string descriptor), Is.True);
        using JsonDocument document = JsonDocument.Parse(descriptor);

        Assert.Multiple(() =>
        {
            Assert.That(document.RootElement.GetProperty("contentMediaType").GetString(), Is.EqualTo("text/plain"));
            Assert.That(snapshot, Does.Contain("@format arch-linter-net/public-api-snapshot"));
            Assert.That(snapshot, Does.Contain("@version 1"));
            Assert.That(snapshot, Does.Contain("@contract public-api"));
            Assert.That(snapshot, Does.Contain("@assembly Product"));
        });
    }
}
