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
            writer.WriteStartObject();
            foreach (JsonProperty property in document.RootElement.EnumerateObject())
            {
                writer.WritePropertyName(property.Name);
                if (property.NameEquals("samples"))
                {
                    writer.WriteStartArray();
                    foreach (JsonElement sample in property.Value.EnumerateArray())
                    {
                        // Keep each repeated measurement on one line. This leaves the surrounding
                        // evidence readable while keeping large result files within GitHub's diff
                        // preview limits so trusted review can inspect the complete patch.
                        writer.WriteRawValue(JsonSerializer.Serialize(sample), skipInputValidation: false);
                    }

                    writer.WriteEndArray();
                }
                else
                {
                    property.Value.WriteTo(writer);
                }
            }

            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(output.ToArray());
    }

    public static BenchmarkEvidenceDocument Deserialize(string json)
    {
        BenchmarkEvidenceDocument evidence = BenchmarkJson.Deserialize<BenchmarkEvidenceDocument>(json);
        evidence.Validate();
        return evidence;
    }
}
