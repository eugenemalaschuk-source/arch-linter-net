using System.Text;
using System.Text.Json;

namespace ArchLinterNet.Core.Tests;

internal static class BenchmarkEvidenceJson
{
    private const int MaximumCompactValueBytes = 8 * 1024;

    public static string Serialize(BenchmarkEvidenceDocument evidence)
    {
        evidence.Validate();
        using JsonDocument document = JsonDocument.Parse(BenchmarkJson.Serialize(evidence));
        using MemoryStream output = new();
        using (Utf8JsonWriter writer = new(output, new JsonWriterOptions { Indented = true }))
        {
            WriteEvidenceElement(writer, document.RootElement);
        }

        return Encoding.UTF8.GetString(output.ToArray());
    }

    private static void WriteEvidenceElement(
        Utf8JsonWriter writer,
        JsonElement element,
        bool compactArrayItems = false)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (JsonProperty property in element.EnumerateObject())
                {
                    writer.WritePropertyName(property.Name);
                    bool isSamples = string.Equals(property.Name, "samples", StringComparison.OrdinalIgnoreCase);
                    if (isSamples || !TryWriteCompactValue(writer, property.Value))
                    {
                        WriteEvidenceElement(
                            writer,
                            property.Value,
                            compactArrayItems: !isSamples && property.Value.ValueKind == JsonValueKind.Array);
                    }
                }

                writer.WriteEndObject();
                break;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (JsonElement item in element.EnumerateArray())
                {
                    if (!compactArrayItems || !TryWriteCompactValue(writer, item))
                    {
                        WriteEvidenceElement(writer, item);
                    }
                }

                writer.WriteEndArray();
                break;
            default:
                element.WriteTo(writer);
                break;
        }
    }

    private static bool TryWriteCompactValue(Utf8JsonWriter writer, JsonElement element)
    {
        if (element.ValueKind is not (JsonValueKind.Array or JsonValueKind.Object))
        {
            return false;
        }

        string compactJson = JsonSerializer.Serialize(element);
        if (Encoding.UTF8.GetByteCount(compactJson) > MaximumCompactValueBytes)
        {
            return false;
        }

        // Keep small records and repeated arrays on one line so evidence diffs stay under
        // GitHub's changed-line preview limit. Large values stay indented and readable.
        writer.WriteRawValue(compactJson, skipInputValidation: false);
        return true;
    }

    public static BenchmarkEvidenceDocument Deserialize(string json)
    {
        BenchmarkEvidenceDocument evidence = BenchmarkJson.Deserialize<BenchmarkEvidenceDocument>(json);
        evidence.Validate();
        return evidence;
    }
}
