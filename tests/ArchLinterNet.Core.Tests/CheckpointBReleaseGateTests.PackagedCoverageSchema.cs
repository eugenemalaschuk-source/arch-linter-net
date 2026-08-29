using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Nodes;
using Json.Schema;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

public sealed partial class CheckpointBReleaseGateTests
{
    private static void AssertPackedCoverageScopeSchema(ZipArchive core)
    {
        ZipArchiveEntry entry = core.GetEntry("contentFiles/any/any/schema/0.8.0/dependencies.arch.schema.json")
            ?? throw new AssertionException("Packed Core package is missing the policy-root schema.");
        using var reader = new StreamReader(entry.Open());
        using JsonDocument document = JsonDocument.Parse(reader.ReadToEnd());
        var wrapper = new JsonObject
        {
            ["$defs"] = JsonNode.Parse(document.RootElement.GetProperty("$defs").GetRawText()),
            ["$ref"] = "#/$defs/coverageContract",
        };
        JsonSchema schema = JsonSchema.FromText(wrapper.ToJsonString());

        foreach (string scope in new[] { "project", "assembly" })
        {
            var valid = new JsonObject
            {
                ["name"] = $"{scope}-coverage",
                ["scope"] = scope,
                ["reason"] = "Every discovered unit must be governed.",
            };
            var invalid = (JsonObject)valid.DeepClone();
            invalid["roots"] = new JsonArray(new JsonObject { ["namespace"] = "App" });

            Assert.Multiple(() =>
            {
                Assert.That(schema.Evaluate(valid).IsValid, Is.True, $"Packed schema: {scope} without roots");
                Assert.That(schema.Evaluate(invalid).IsValid, Is.False, $"Packed schema: {scope} with roots");
            });
        }
    }
}
