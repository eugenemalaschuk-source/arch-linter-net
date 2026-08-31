using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using ArchLinterNet.Core.Resolution;
using Json.Schema;
using NUnit.Framework;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class ContractSurfaceExposureSchemaTests
{
    [Test]
    public void Schema_DeclaresStrictAndAuditGroupsAndClosedDefinitions()
    {
        using JsonDocument schema = LoadSchema();
        JsonElement root = schema.RootElement;
        JsonElement contractProperties = root.GetProperty("$defs").GetProperty("contracts").GetProperty("properties");
        JsonElement exposure = root.GetProperty("$defs").GetProperty("contractSurfaceExposureContract");
        JsonElement source = root.GetProperty("$defs").GetProperty("contractSurfaceExposureSource");
        JsonElement selector = root.GetProperty("$defs").GetProperty("contractSurfaceExposureSelector");
        JsonElement exposureShape = exposure.GetProperty("allOf")[1];

        Assert.Multiple(() =>
        {
            Assert.That(contractProperties.GetProperty("strict_contract_surface_exposure").GetProperty("items")
                .GetProperty("$ref").GetString(), Is.EqualTo("#/$defs/contractSurfaceExposureContract"));
            Assert.That(contractProperties.GetProperty("audit_contract_surface_exposure").GetProperty("items")
                .GetProperty("$ref").GetString(), Is.EqualTo("#/$defs/contractSurfaceExposureContract"));
            Assert.That(exposureShape.GetProperty("required").EnumerateArray().Select(v => v.GetString()),
                Is.EquivalentTo(["id", "name", "source", "forbidden"]));
            Assert.That(source.GetProperty("additionalProperties").GetBoolean(), Is.False);
            Assert.That(selector.GetProperty("additionalProperties").GetBoolean(), Is.False);
            Assert.That(selector.GetProperty("minProperties").GetInt32(), Is.EqualTo(1));
            Assert.That(selector.GetProperty("properties").EnumerateObject().Select(p => p.Name),
                Is.EquivalentTo(["name_suffix", "name_prefix", "namespace", "layer", "base_type",
                    "implements_interface", "has_attribute", "role"]));
        });
    }

    [TestCase("id: exposure\nname: Exposure\nsource:\n  assemblies: [Example.Api]\nforbidden:\n  - role: Entity\n", true)]
    [TestCase("id: exposure\nname: Exposure\nsource:\n  projects: [src/Example.Api.csproj]\nforbidden:\n  - namespace: Example.Domain\n", true)]
    [TestCase("id: exposure\nname: Exposure\nsource: {}\nforbidden:\n  - role: Entity\n", false)]
    [TestCase("id: exposure\nname: Exposure\nsource:\n  assemblies: []\nforbidden:\n  - role: Entity\n", false)]
    [TestCase("id: exposure\nname: Exposure\nsource:\n  types_matching: {}\nforbidden:\n  - role: Entity\n", false)]
    [TestCase("id: exposure\nname: Exposure\nsource:\n  types_matching:\n    regex: Entity\nforbidden:\n  - role: Entity\n", false)]
    [TestCase("id: exposure\nname: Exposure\nsource:\n  assemblies: [Example.Api]\nforbidden:\n  - {}\n", false)]
    [TestCase("id: exposure\nname: Exposure\nsource:\n  public_api_surface: ' '\nforbidden:\n  - role: Entity\n", false)]
    public void Schema_ContractSurfaceExposureShape_IsClosedAndBounded(string yaml, bool expectedValid)
    {
        Assert.That(Validate(yaml, "contractSurfaceExposureContract"), Is.EqualTo(expectedValid), yaml);
    }

    private static JsonSchema LoadSubSchema(string definition)
    {
        using JsonDocument document = LoadSchema();
        var wrapper = new JsonObject
        {
            ["$defs"] = JsonNode.Parse(document.RootElement.GetProperty("$defs").GetRawText()),
            ["$ref"] = $"#/$defs/{definition}"
        };
        return JsonSchema.FromText(wrapper.ToJsonString());
    }

    private static bool Validate(string yaml, string definition)
    {
        YamlStream stream = new();
        stream.Load(new StringReader(yaml));
        JsonNode? instance = ConvertNode(stream.Documents[0].RootNode);
        return LoadSubSchema(definition).Evaluate(instance).IsValid;
    }

    private static JsonNode? ConvertNode(YamlNode node) => node switch
    {
        YamlScalarNode scalar => ConvertScalar(scalar),
        YamlSequenceNode sequence => new JsonArray(sequence.Children.Select(ConvertNode).ToArray()),
        YamlMappingNode mapping => new JsonObject(mapping.Children.Select(pair =>
            new KeyValuePair<string, JsonNode?>(((YamlScalarNode)pair.Key).Value ?? string.Empty,
                ConvertNode(pair.Value)))),
        _ => throw new NotSupportedException(node.GetType().Name)
    };

    private static JsonNode? ConvertScalar(YamlScalarNode scalar)
    {
        if (scalar.Style != ScalarStyle.Plain || scalar.Value is null)
        {
            return scalar.Value;
        }

        return scalar.Value switch
        {
            "null" or "~" or "" => null,
            "true" => true,
            "false" => false,
            _ when int.TryParse(scalar.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int i) => i,
            _ when double.TryParse(scalar.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double d) => d,
            _ => scalar.Value
        };
    }

    private static JsonDocument LoadSchema()
    {
        string repositoryRoot = new ArchitectureRepositoryRootResolver().Resolve();
        return JsonDocument.Parse(File.ReadAllText(Path.Combine(repositoryRoot, "schema", "dependencies.arch.schema.json")));
    }
}
