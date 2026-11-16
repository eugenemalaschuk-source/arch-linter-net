using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ArchLinterNet.Core.Tests.Benchmarking;

internal static class BenchmarkEvidenceJson
{
    private const string ArchiveSchemaId = "benchmark-evidence-archive/v1";
    private const int MaximumCompactValueBytes = 8 * 1024;
    private const int Base64ChunkLength = 4 * 1024;

    public static string Serialize(BenchmarkEvidenceDocument evidence)
    {
        evidence.Validate();
        using var document = JsonDocument.Parse(BenchmarkJson.Serialize(evidence));
        using MemoryStream output = new();
        using (Utf8JsonWriter writer = new(output, new JsonWriterOptions { Indented = true }))
        {
            WriteEvidenceElement(writer, document.RootElement);
        }

        return Encoding.UTF8.GetString(output.ToArray());
    }

    public static string SerializeArchive(string evidenceJson)
    {
        _ = Deserialize(evidenceJson);
        byte[] payload = Encoding.UTF8.GetBytes(evidenceJson);
        string encodedPayload = Convert.ToBase64String(Compress(payload));

        using MemoryStream output = new();
        using (Utf8JsonWriter writer = new(output, new JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject();
            writer.WriteString("archive_schema_id", ArchiveSchemaId);
            writer.WriteString("encoding", "gzip+base64");
            writer.WriteString("payload_sha256", Sha256(payload));
            writer.WriteStartArray("payload_base64_chunks");
            for (int offset = 0; offset < encodedPayload.Length; offset += Base64ChunkLength)
                writer.WriteStringValue(encodedPayload.Substring(
                    offset,
                    Math.Min(Base64ChunkLength, encodedPayload.Length - offset)));

            writer.WriteEndArray();
            writer.WriteEndObject();
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
                        WriteEvidenceElement(
                            writer,
                            property.Value,
                            !isSamples && property.Value.ValueKind == JsonValueKind.Array);
                }

                writer.WriteEndObject();
                break;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (JsonElement item in element.EnumerateArray())
                    if (!compactArrayItems || !TryWriteCompactValue(writer, item))
                        WriteEvidenceElement(writer, item);

                writer.WriteEndArray();
                break;
            default:
                element.WriteTo(writer);
                break;
        }
    }

    private static bool TryWriteCompactValue(Utf8JsonWriter writer, JsonElement element)
    {
        if (element.ValueKind is not (JsonValueKind.Array or JsonValueKind.Object)) return false;

        string compactJson = JsonSerializer.Serialize(element);
        if (Encoding.UTF8.GetByteCount(compactJson) > MaximumCompactValueBytes) return false;

        writer.WriteRawValue(compactJson);
        return true;
    }

    public static BenchmarkEvidenceDocument Deserialize(string json)
    {
        using var document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;
        if (root.ValueKind == JsonValueKind.Object
            && root.TryGetProperty("archive_schema_id", out _))
            return DeserializeArchive(root);

        return DeserializeEvidencePayload(json);
    }

    private static BenchmarkEvidenceDocument DeserializeArchive(JsonElement archive)
    {
        if (!string.Equals(
                archive.GetProperty("archive_schema_id").GetString(),
                ArchiveSchemaId,
                StringComparison.Ordinal)
            || !string.Equals(archive.GetProperty("encoding").GetString(), "gzip+base64", StringComparison.Ordinal))
            throw new InvalidOperationException("Unsupported benchmark evidence archive format.");

        StringBuilder encodedPayload = new();
        foreach (JsonElement chunk in archive.GetProperty("payload_base64_chunks").EnumerateArray())
        {
            if (chunk.ValueKind != JsonValueKind.String)
                throw new InvalidOperationException("Benchmark evidence archive payload chunks must be strings.");

            encodedPayload.Append(chunk.GetString());
        }

        byte[] payload;
        try
        {
            payload = Decompress(Convert.FromBase64String(encodedPayload.ToString()));
        }
        catch (Exception error) when (error is FormatException or IOException)
        {
            throw new InvalidOperationException("Benchmark evidence archive payload is malformed.", error);
        }

        if (!string.Equals(
                Sha256(payload),
                archive.GetProperty("payload_sha256").GetString(),
                StringComparison.Ordinal))
            throw new InvalidOperationException("Benchmark evidence archive payload failed its SHA-256 check.");

        return DeserializeEvidencePayload(Encoding.UTF8.GetString(payload));
    }

    private static BenchmarkEvidenceDocument DeserializeEvidencePayload(string json)
    {
        BenchmarkEvidenceDocument evidence = BenchmarkJson.Deserialize<BenchmarkEvidenceDocument>(json);
        evidence.Validate();
        return evidence;
    }

    private static byte[] Compress(byte[] payload)
    {
        using MemoryStream compressed = new();
        using (GZipStream gzip = new(compressed, CompressionLevel.SmallestSize, true))
        {
            gzip.Write(payload);
        }

        return compressed.ToArray();
    }

    private static byte[] Decompress(byte[] compressed)
    {
        using MemoryStream input = new(compressed);
        using GZipStream gzip = new(input, CompressionMode.Decompress);
        using MemoryStream payload = new();
        gzip.CopyTo(payload);
        return payload.ToArray();
    }

    private static string Sha256(byte[] payload)
    {
        return Convert.ToHexString(SHA256.HashData(payload)).ToLowerInvariant();
    }
}
