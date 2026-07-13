using System.Globalization;
using System.Reflection;
using System.Text.Json.Nodes;
using Json.Schema;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace ArchLinterNet.Core.Contracts.PolicyImports;

internal static class ArchitecturePolicyEffectiveSchemaValidator
{
    private const string SchemaResourceName = "ArchLinterNet.Core.Schema.dependencies.arch.schema.json";

    private static readonly Lazy<JsonSchema> _schema = new(LoadSchema);

    public static void Validate(string yaml)
    {
        var stream = new YamlStream();
        stream.Load(new StringReader(yaml));
        JsonNode? instance = ConvertNode(stream.Documents[0].RootNode);
        RemoveValidatedContractIds(instance);
        EvaluationResults results = _schema.Value.Evaluate(
            instance,
            new EvaluationOptions { OutputFormat = OutputFormat.List });
        if (results.IsValid)
        {
            return;
        }

        string details = string.Join(
            "; ",
            results.Details
                .Where(detail => !detail.IsValid)
                .SelectMany(detail => detail.Errors?.Select(error => $"{detail.InstanceLocation}: {error.Value}")
                    ?? Array.Empty<string>())
                .TakeLast(12));
        throw new ArchitecturePolicyImportException(
            ArchitecturePolicyImportErrorCategory.SourceShape,
            $"Composed policy does not satisfy the effective policy schema: {details}");
    }

    private static JsonSchema LoadSchema()
    {
        Assembly assembly = typeof(ArchitecturePolicyEffectiveSchemaValidator).Assembly;
        using Stream stream = assembly.GetManifestResourceStream(SchemaResourceName)
            ?? throw new InvalidOperationException($"Embedded policy schema '{SchemaResourceName}' was not found.");
        using var reader = new StreamReader(stream);
        return JsonSchema.FromText(reader.ReadToEnd());
    }

    private static void RemoveValidatedContractIds(JsonNode? instance)
    {
        if (instance is not JsonObject root
            || root["contracts"] is not JsonObject contracts)
        {
            return;
        }

        foreach ((_, JsonNode? group) in contracts)
        {
            if (group is not JsonArray entries)
            {
                continue;
            }

            foreach (JsonObject contract in entries.OfType<JsonObject>())
            {
                if (!contract.TryGetPropertyValue("id", out JsonNode? idNode))
                {
                    continue;
                }

                if (idNode is not JsonValue idValue
                    || !idValue.TryGetValue(out string? id)
                    || string.IsNullOrEmpty(id))
                {
                    throw new ArchitecturePolicyImportException(
                        ArchitecturePolicyImportErrorCategory.SourceShape,
                        "A composed contract id must be a non-empty string when declared.");
                }

                // The published schema exposes id through baseContractFields. Json Schema's
                // additionalProperties scope inside each allOf family branch cannot see that
                // sibling annotation, so validate the shared field here and evaluate the remaining
                // contract against the family schema. The effective YAML itself is unchanged.
                contract.Remove("id");
            }
        }
    }

    private static JsonNode? ConvertNode(YamlNode node)
    {
        return node switch
        {
            YamlScalarNode scalar => ConvertScalar(scalar),
            YamlSequenceNode sequence => new JsonArray(sequence.Children.Select(ConvertNode).ToArray()),
            YamlMappingNode mapping => new JsonObject(mapping.Children.Select(pair =>
                new KeyValuePair<string, JsonNode?>(
                    ((YamlScalarNode)pair.Key).Value ?? string.Empty,
                    ConvertNode(pair.Value)))),
            _ => throw new NotSupportedException($"Unsupported YAML node type: {node.GetType()}")
        };
    }

    private static JsonNode? ConvertScalar(YamlScalarNode scalar)
    {
        string? value = scalar.Value;
        if (scalar.Style != ScalarStyle.Plain || value is null)
        {
            return value;
        }

        return value switch
        {
            "null" or "~" or "" => null,
            "true" => true,
            "false" => false,
            _ when int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int integer) => integer,
            _ when double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double number) => number,
            _ => value
        };
    }
}
