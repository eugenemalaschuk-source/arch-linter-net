using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using ArchLinterNet.Core.BuildState;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;
using ArchLinterNet.Core.Resolution;
using ArchLinterNet.Core.Schema;
using Json.Schema;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class PackagedSchemaRegistryTests
{
    private static readonly string[] _value = {
                "analysis-build-state", "analysis-cache", "analysis-profile", "api-snapshot", "baseline", "normalized-finding", "policy-fragment", "policy-root",
            };
    private static readonly string[] _advancedSchemaIds =
        ["policy-root", "policy-fragment", "normalized-finding", "analysis-cache"];

    [Test]
    public void List_ReturnsEveryReleaseMatchedSchemaInOrdinalOrder()
    {
        PackagedSchemaRegistry registry = new();

        IReadOnlyList<PackagedSchemaDescriptor> schemas = registry.List();

        Assert.Multiple(() =>
        {
            Assert.That(schemas.Select(static schema => schema.LogicalId), Is.EqualTo(_value));
            Assert.That(schemas.Single(static schema => schema.LogicalId == "baseline").DocumentVersion, Is.EqualTo("v2"));
            Assert.That(schemas.Single(static schema => schema.LogicalId == "normalized-finding").DocumentVersion, Is.EqualTo("v2"));
            // Policy root/fragment and the applicability schema advances own independent 0.6.1
            // identities. Every previous 0.5.1 resource remains byte-for-byte frozen (see
            // openspec/specs/packaged-schema-registry and schema/0.5.1/compatibility-manifest.json).
            Assert.That(
                schemas.Where(schema => !_advancedSchemaIds.Contains(schema.LogicalId))
                    .All(static schema => schema.SchemaId.Contains("/schema/0.5.1/", StringComparison.Ordinal)),
                Is.True);
            Assert.That(
                schemas.Where(schema => _advancedSchemaIds.Contains(schema.LogicalId))
                    .All(static schema => schema.SchemaId.Contains("/schema/0.6.1/", StringComparison.Ordinal)),
                Is.True);
            Assert.That(
                schemas.Where(schema => !_advancedSchemaIds.Contains(schema.LogicalId))
                    .All(static schema => schema.ResourcePath.StartsWith("schema/0.5.1/", StringComparison.Ordinal)),
                Is.True);
            Assert.That(
                schemas.Where(schema => _advancedSchemaIds.Contains(schema.LogicalId))
                    .All(static schema => schema.ResourcePath.StartsWith("schema/0.6.1/", StringComparison.Ordinal)),
                Is.True);
            Assert.That(schemas.All(static schema => schema.Sha256.Length == 64), Is.True);
            Assert.That(
                schemas.Select(static schema => (schema.LogicalId, schema.SupportsRead, schema.SupportsWrite)),
                Is.EqualTo(new[]
                {
                    ("analysis-build-state", true, true),
                    ("analysis-cache", true, true),
                    ("analysis-profile", false, true),
                    ("api-snapshot", true, true),
                    ("baseline", true, true),
                    ("normalized-finding", true, true),
                    ("policy-fragment", true, true),
                    ("policy-root", true, true),
                }));
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
                Assert.That(schema, Does.Not.Contain("Not yet registered in compatibility-manifest.json"));
                using JsonDocument document = JsonDocument.Parse(schema);
                Assert.That(document.RootElement.GetProperty("$id").GetString(), Is.EqualTo(descriptor.SchemaId));
            });
        }
    }

    [Test]
    public void ApplicabilitySchemaAdvance_PreservesTheFrozenV1Bytes()
    {
        string repositoryRoot = new ArchitectureRepositoryRootResolver().Resolve();
        var expectedDigests = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["normalized-finding.schema.json"] = "f3b6fb5de05de315e6c59bfdeedf921423165bdc7da6d2da4681600fcc4947d3",
            ["analysis-cache.schema.json"] = "b0958295d23fc6bb4d575ddd81e837e8e458c355662cdae4844bf7a48dfcc9f2",
        };

        foreach ((string filename, string expectedDigest) in expectedDigests)
        {
            string path = Path.Combine(repositoryRoot, "schema", "0.5.1", filename);
            string actualDigest = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)));
            Assert.That(actualDigest, Is.EqualTo(expectedDigest), filename);
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
            new string('a', 64), new string('b', 64), new string('c', 64),
            CacheEligibility.CacheIneligible, ["package-reference-identity-unverified"]);
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
        Assert.That(finding.GetProperty("schema_version").GetInt32(), Is.EqualTo(ArchitectureFinding.CurrentSchemaVersion));

        JsonObject unknownKind = JsonNode.Parse(finding.GetRawText())!.AsObject();
        unknownKind["kind"] = "future_finding";
        Assert.That(schema.Evaluate(unknownKind).IsValid, Is.False);

        JsonObject unknownVersion = JsonNode.Parse(finding.GetRawText())!.AsObject();
        unknownVersion["schema_version"] = ArchitectureFinding.CurrentSchemaVersion + 1;
        Assert.That(schema.Evaluate(unknownVersion).IsValid, Is.False);
    }

    [Test]
    public void NormalizedFindingReader_UnknownV1Kind_IsOpaqueForNonStrictAndRejectedForStrict()
    {
        const string Json = """
            {
              "schema_version": 1,
              "kind": "future_finding",
              "contract": "future contract",
              "contract_id": "future-contract",
              "canonical_identity": "future:1",
              "mode": "strict",
              "severity": "error",
              "message_code": "future",
              "policy_origin": {
                "root_path": "/repo",
                "source_path": "architecture/future.yml",
                "role": "fragment",
                "yaml_path": "future[0]",
                "line": 7,
                "column": 3,
                "source_ordinal": 2,
                "import_chain": ["architecture.yml", "architecture/future.yml"]
              },
              "source_location": {
                "path": "src/Future.cs",
                "line": 11,
                "column": 5
              },
              "baseline_state": null,
              "details": { "future_evidence": true }
            }
            """;

        ArchitectureFindingReadEnvelope opaque = ArchitectureFindingJsonReader.Read(Json, strict: false);

        Assert.Multiple(() =>
        {
            Assert.That(opaque.IsOpaque, Is.True);
            Assert.That(opaque.SchemaVersion, Is.EqualTo(1));
            Assert.That(opaque.Kind, Is.EqualTo("future_finding"));
            Assert.That(opaque.Contract, Is.EqualTo("future contract"));
            Assert.That(opaque.ContractId, Is.EqualTo("future-contract"));
            Assert.That(
                opaque.RawPolicyOrigin?.GetProperty("yaml_path").GetString(),
                Is.EqualTo("future[0]"));
            Assert.That(
                opaque.RawSourceLocation?.GetProperty("path").GetString(),
                Is.EqualTo("src/Future.cs"));
            Assert.That(opaque.RawDetails.GetProperty("future_evidence").GetBoolean(), Is.True);
            using JsonDocument forwarded = JsonDocument.Parse(opaque.ToJson());
            Assert.That(
                forwarded.RootElement.GetProperty("policy_origin").GetProperty("source_path").GetString(),
                Is.EqualTo("architecture/future.yml"));
            Assert.That(
                forwarded.RootElement.GetProperty("details").GetProperty("future_evidence").GetBoolean(),
                Is.True);
            Assert.That(
                JsonNode.DeepEquals(JsonNode.Parse(Json), JsonNode.Parse(opaque.ToJson())),
                Is.True);
            Assert.That(
                () => ArchitectureFindingJsonReader.Read(Json, strict: true),
                Throws.TypeOf<ArchitectureFindingFormatException>()
                    .With.Message.Contains("Unsupported normalized finding kind 'future_finding'"));
        });
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
