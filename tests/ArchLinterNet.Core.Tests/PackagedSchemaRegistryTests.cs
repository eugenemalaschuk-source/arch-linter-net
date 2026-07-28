using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ArchLinterNet.Core.Schema;
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
                "analysis-build-state", "analysis-cache", "analysis-profile", "api-snapshot",
                "baseline", "finding", "policy-fragment", "policy-root",
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
}
