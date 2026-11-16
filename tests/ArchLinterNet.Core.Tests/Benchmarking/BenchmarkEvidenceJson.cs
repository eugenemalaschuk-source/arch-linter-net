using System.Text;
using System.Text.Json;

namespace ArchLinterNet.Core.Tests;

internal static class BenchmarkEvidenceJson
{
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

    private static void WriteEvidenceElement(Utf8JsonWriter writer, JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (JsonProperty property in element.EnumerateObject())
                {
                    writer.WritePropertyName(property.Name);
                    WriteEvidenceElement(writer, property.Value);
                }

                writer.WriteEndObject();
                break;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (JsonElement item in element.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.Object)
                    {
                        // Keep each record in a large object array on one line. Surrounding
                        // metadata stays indented, while GitHub can return complete patches for
                        // evidence files and trusted review can inspect the entire diff.
                        writer.WriteRawValue(JsonSerializer.Serialize(item), skipInputValidation: false);
                    }
                    else
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

    public static BenchmarkEvidenceDocument Deserialize(string json)
    {
        BenchmarkEvidenceDocument evidence = BenchmarkJson.Deserialize<BenchmarkEvidenceDocument>(json);
        evidence.Validate();
        return evidence;
    }
}
