using System.Text.Json;
using System.Text.Json.Nodes;
using ArchLinterNet.Core.Resolution;
using Json.Schema;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class ArchitecturePolicyImportSchemaTests
{
    [Test]
    public void RootSchema_AllowsExplicitImportsWithoutFilenameRules()
    {
        using JsonDocument schema = Load("dependencies.arch.schema.json");

        JsonElement imports = schema.RootElement.GetProperty("properties").GetProperty("imports");
        JsonElement definition = schema.RootElement.GetProperty("$defs").GetProperty("imports");

        Assert.Multiple(() =>
        {
            Assert.That(imports.GetProperty("$ref").GetString(), Is.EqualTo("#/$defs/imports"));
            Assert.That(definition.GetProperty("minItems").GetInt32(), Is.EqualTo(1));
            Assert.That(schema.RootElement.GetRawText(), Does.Not.Contain("arch.yml\"").IgnoreCase);
        });
    }

    [Test]
    public void FragmentSchema_ExcludesRootIdentityAndAllowsEveryMergeableSection()
    {
        using JsonDocument schema = Load("dependencies.arch.fragment.schema.json");
        JsonElement properties = schema.RootElement.GetProperty("properties");
        string[] expected =
        {
            "imports", "layers", "external_dependencies", "packages", "legacy_runtime_layers",
            "analysis", "contracts", "classification"
        };

        Assert.Multiple(() =>
        {
            Assert.That(properties.EnumerateObject().Select(property => property.Name), Is.EquivalentTo(expected));
            Assert.That(properties.TryGetProperty("version", out _), Is.False);
            Assert.That(properties.TryGetProperty("name", out _), Is.False);
            Assert.That(schema.RootElement.GetProperty("additionalProperties").GetBoolean(), Is.False);
        });
    }

    [Test]
    public void FragmentSchema_ValidatesArbitraryFragmentShapeAndRejectsRootIdentity()
    {
        JsonSchema schema = LoadSelfContainedFragmentSchema();
        var valid = new JsonObject
        {
            ["imports"] = new JsonArray("nested/domain.yaml"),
            ["layers"] = new JsonObject
            {
                ["domain"] = new JsonObject { ["namespace"] = "App.Domain" }
            }
        };
        var rootShaped = new JsonObject
        {
            ["version"] = 1,
            ["name"] = "Not a fragment",
            ["layers"] = new JsonObject
            {
                ["domain"] = new JsonObject { ["namespace"] = "App.Domain" }
            }
        };

        Assert.Multiple(() =>
        {
            Assert.That(schema.Evaluate(valid).IsValid, Is.True);
            Assert.That(schema.Evaluate(rootShaped).IsValid, Is.False);
            Assert.That(schema.Evaluate(new JsonObject()).IsValid, Is.False);
        });
    }

    private static JsonSchema LoadSelfContainedFragmentSchema()
    {
        using JsonDocument rootDocument = Load("dependencies.arch.schema.json");
        using JsonDocument fragmentDocument = Load("dependencies.arch.fragment.schema.json");
        JsonObject fragment = (JsonObject)JsonNode.Parse(fragmentDocument.RootElement.GetRawText())!;
        fragment["$defs"] = JsonNode.Parse(rootDocument.RootElement.GetProperty("$defs").GetRawText());
        RewriteRootReferences(fragment);
        return JsonSchema.FromText(fragment.ToJsonString());
    }

    private static void RewriteRootReferences(JsonNode? node)
    {
        const string RootPrefix = "https://archlinternet.dev/schema/dependencies.arch.schema.json#/$defs/";
        if (node is JsonObject mapping)
        {
            foreach (string key in mapping.Select(pair => pair.Key).ToArray())
            {
                JsonNode? child = mapping[key];
                if (key == "$ref"
                    && child is JsonValue value
                    && value.TryGetValue(out string? reference)
                    && reference?.StartsWith(RootPrefix, StringComparison.Ordinal) == true)
                {
                    mapping[key] = $"#/$defs/{reference[RootPrefix.Length..]}";
                }
                else
                {
                    RewriteRootReferences(child);
                }
            }
        }
        else if (node is JsonArray sequence)
        {
            foreach (JsonNode? child in sequence)
            {
                RewriteRootReferences(child);
            }
        }
    }

    private static JsonDocument Load(string fileName)
    {
        string repositoryRoot = new ArchitectureRepositoryRootResolver().Resolve();
        return JsonDocument.Parse(File.ReadAllText(Path.Combine(repositoryRoot, "schema", fileName)));
    }
}
