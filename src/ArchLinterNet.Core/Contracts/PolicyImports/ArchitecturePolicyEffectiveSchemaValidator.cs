using System.Reflection;
using System.Text.Json.Nodes;
using ArchLinterNet.Core.Model;
using Json.Schema;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace ArchLinterNet.Core.Contracts.PolicyImports;

internal static class ArchitecturePolicyEffectiveSchemaValidator
{
    private const string SchemaResourceName = "ArchLinterNet.Core.Schema.dependencies.arch.schema.json";

    private static readonly Lazy<JsonSchema> _schema = new(LoadSchema);

    public static void Validate(string yaml, ArchitecturePolicyProvenanceIndex provenance)
    {
        var stream = new YamlStream();
        stream.Load(new StringReader(yaml));
        JsonNode? instance = ConvertNode(stream.Documents[0].RootNode);
        RemoveValidatedContractIds(instance, provenance);
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
        ArchitecturePolicySourceLocation? location = results.Details
            .Where(detail => !detail.IsValid && detail.Errors is not null)
            .OrderByDescending(detail => InstanceDepth(detail.InstanceLocation.ToString()))
            .Select(detail => FindLocation(provenance, detail.InstanceLocation.ToString()))
            .FirstOrDefault(candidate => candidate is not null);
        throw ArchitecturePolicyDiagnosticFactory.Exception(
            ArchitecturePolicyImportErrorCategory.SourceShape,
            $"Composed policy does not satisfy the effective policy schema: {details}",
            location);
    }

    private static JsonSchema LoadSchema()
    {
        Assembly assembly = typeof(ArchitecturePolicyEffectiveSchemaValidator).Assembly;
        using Stream stream = assembly.GetManifestResourceStream(SchemaResourceName)
            ?? throw new InvalidOperationException($"Embedded policy schema '{SchemaResourceName}' was not found.");
        using var reader = new StreamReader(stream);
        return JsonSchema.FromText(reader.ReadToEnd());
    }

    private static void RemoveValidatedContractIds(
        JsonNode? instance,
        ArchitecturePolicyProvenanceIndex provenance)
    {
        if (instance is not JsonObject root
            || root["contracts"] is not JsonObject contracts)
        {
            return;
        }

        foreach ((string groupName, JsonNode? group) in contracts)
        {
            if (group is not JsonArray entries)
            {
                continue;
            }

            for (int index = 0; index < entries.Count; index++)
            {
                if (entries[index] is not JsonObject contract)
                {
                    continue;
                }

                if (!contract.TryGetPropertyValue("id", out JsonNode? idNode))
                {
                    continue;
                }

                if (idNode is not JsonValue idValue
                    || !idValue.TryGetValue(out string? id)
                    || string.IsNullOrEmpty(id))
                {
                    provenance.TryGetLocation(
                        ArchitecturePolicyProvenancePath.AppendProperty(
                            ArchitecturePolicyProvenancePath.AppendIndex(
                                ArchitecturePolicyProvenancePath.AppendProperty(
                                    ArchitecturePolicyProvenancePath.Property("contracts"), groupName),
                                index),
                            "id"),
                        out ArchitecturePolicySourceLocation? location);
                    throw ArchitecturePolicyDiagnosticFactory.Exception(
                        ArchitecturePolicyImportErrorCategory.SourceShape,
                        "A composed contract id must be a non-empty string when declared.",
                        location);
                }

                // The published schema exposes id through baseContractFields. Json Schema's
                // additionalProperties scope inside each allOf family branch cannot see that
                // sibling annotation, so validate the shared field here and evaluate the remaining
                // contract against the family schema. The effective YAML itself is unchanged.
                contract.Remove("id");
            }
        }
    }

    private static ArchitecturePolicySourceLocation? FindLocation(
        ArchitecturePolicyProvenanceIndex provenance,
        string instanceLocation)
    {
        string path = ArchitecturePolicyProvenancePath.Normalize(instanceLocation);
        while (true)
        {
            if (provenance.TryGetLocation(path, out ArchitecturePolicySourceLocation? location))
            {
                return location;
            }

            if (path == ArchitecturePolicyProvenancePath.Root)
            {
                return null;
            }

            path = ArchitecturePolicyProvenancePath.Parent(path);
        }
    }

    private static int InstanceDepth(string instanceLocation)
    {
        return instanceLocation.Count(character => character == '/');
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
        if (value is null)
        {
            return value;
        }

        bool explicitlyTyped = !scalar.Tag.IsEmpty
            && !scalar.Tag.IsNonSpecific
            && scalar.Tag.Value.StartsWith("tag:yaml.org,2002:", StringComparison.Ordinal);
        if (scalar.Style != ScalarStyle.Plain && !explicitlyTyped)
        {
            return value;
        }

        if (string.Equals(value, "null", StringComparison.OrdinalIgnoreCase) || value == "~")
        {
            return null;
        }

        if (bool.TryParse(value, out bool boolean))
        {
            return boolean;
        }

        string normalized = value.Replace("_", string.Empty, StringComparison.Ordinal);
        if (TryParseInteger(normalized, out long integer))
        {
            return integer is >= int.MinValue and <= int.MaxValue ? (int)integer : integer;
        }

        if (double.TryParse(normalized, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out double number))
        {
            return number;
        }

        return value;
    }

    private static bool TryParseInteger(string value, out long result)
    {
        const System.Globalization.NumberStyles Decimal = System.Globalization.NumberStyles.Integer;
        if (long.TryParse(value, Decimal, System.Globalization.CultureInfo.InvariantCulture, out result))
        {
            return true;
        }

        bool negative = value.StartsWith("-", StringComparison.Ordinal);
        string unsignedValue = negative ? value[1..] : value;
        int radix = unsignedValue.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? 16
            : unsignedValue.StartsWith("0o", StringComparison.OrdinalIgnoreCase) ? 8
            : unsignedValue.StartsWith("0b", StringComparison.OrdinalIgnoreCase) ? 2
            : 0;
        if (radix == 0)
        {
            result = default;
            return false;
        }

        string digits = unsignedValue[2..];
        if (digits.Length == 0)
        {
            result = default;
            return false;
        }

        try
        {
            result = Convert.ToInt64(digits, radix);
            if (negative)
            {
                result = -result;
            }

            return true;
        }
        catch (FormatException)
        {
            result = default;
            return false;
        }
        catch (OverflowException)
        {
            result = default;
            return false;
        }
    }
}
