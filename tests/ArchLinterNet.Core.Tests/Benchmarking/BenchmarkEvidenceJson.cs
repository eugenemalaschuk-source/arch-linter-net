using System.IO.Compression;
using System.Security.Cryptography;
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
                    if (string.Equals(property.Name, "raw_analysis_profile", StringComparison.OrdinalIgnoreCase))
                    {
                        string rawProfileJson = property.Value.GetRawText();
                        WriteRawProfileSummary(writer, property.Value);
                        writer.WritePropertyName("raw_analysis_profile_gzip_base64");
                        writer.WriteStringValue(Compress(rawProfileJson));
                        writer.WritePropertyName("raw_analysis_profile_sha256");
                        writer.WriteStringValue(Sha256(rawProfileJson));
                        continue;
                    }

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

    private static void WriteRawProfileSummary(Utf8JsonWriter writer, JsonElement profile)
    {
        writer.WriteStartObject();
        CopyProfileProperty(writer, profile, "SchemaId");
        CopyProfileProperty(writer, profile, "CompletionStatus");
        CopyProfileProperty(writer, profile, "CancellationObserved");

        if (profile.TryGetProperty("Counters", out JsonElement counters))
        {
            writer.WritePropertyName("Counters");
            writer.WriteStartObject();
            CopyProfileProperty(writer, counters, "ContractFamilyCounts");
            CopyProfileProperty(writer, counters, "ContractFamilyResultCounts");
            writer.WriteEndObject();
        }

        writer.WriteEndObject();
    }

    private static void CopyProfileProperty(Utf8JsonWriter writer, JsonElement source, string name)
    {
        if (source.TryGetProperty(name, out JsonElement property))
        {
            writer.WritePropertyName(name);
            if (property.ValueKind is JsonValueKind.Array or JsonValueKind.Object)
            {
                writer.WriteRawValue(JsonSerializer.Serialize(property), skipInputValidation: false);
            }
            else
            {
                property.WriteTo(writer);
            }
        }
    }

    private static string Compress(string json)
    {
        using MemoryStream compressed = new();
        using (GZipStream gzip = new(compressed, CompressionLevel.SmallestSize, leaveOpen: true))
        {
            gzip.Write(Encoding.UTF8.GetBytes(json));
        }

        return Convert.ToBase64String(compressed.ToArray());
    }

    private static string Decompress(string base64)
    {
        byte[] compressed = Convert.FromBase64String(base64);
        using MemoryStream input = new(compressed);
        using GZipStream gzip = new(input, CompressionMode.Decompress);
        using StreamReader reader = new(gzip, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    private static string Sha256(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    public static BenchmarkEvidenceDocument Deserialize(string json)
    {
        using JsonDocument serializedEvidence = JsonDocument.Parse(json);
        BenchmarkEvidenceDocument evidence = BenchmarkJson.Deserialize<BenchmarkEvidenceDocument>(json);
        JsonElement[] serializedSamples = serializedEvidence.RootElement.GetProperty("samples")
            .EnumerateArray()
            .ToArray();
        BenchmarkProfileSample[] samples = evidence.Samples
            .Select((sample, index) => RestoreRawProfile(sample, serializedSamples[index]))
            .ToArray();
        evidence = evidence with { Samples = samples };
        evidence.Validate();
        return evidence;
    }

    private static BenchmarkProfileSample RestoreRawProfile(
        BenchmarkProfileSample sample,
        JsonElement serializedSample)
    {
        bool hasCompressedProfile = serializedSample.TryGetProperty(
            "raw_analysis_profile_gzip_base64",
            out JsonElement compressedProfile);
        bool hasProfileSha256 = serializedSample.TryGetProperty(
            "raw_analysis_profile_sha256",
            out JsonElement profileSha256);
        if (!hasCompressedProfile && !hasProfileSha256)
        {
            return sample;
        }

        if (!hasCompressedProfile || !hasProfileSha256 || profileSha256.ValueKind != JsonValueKind.String)
        {
            throw new InvalidOperationException("Compressed analysis-profile evidence requires a SHA-256 digest.");
        }

        string rawProfileJson;
        try
        {
            rawProfileJson = Decompress(compressedProfile.GetString()!);
        }
        catch (Exception error) when (error is FormatException or InvalidDataException)
        {
            throw new InvalidOperationException("Compressed analysis-profile evidence is malformed.", error);
        }

        if (!string.Equals(Sha256(rawProfileJson), profileSha256.GetString(), StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Compressed analysis-profile evidence failed its SHA-256 check.");
        }

        using JsonDocument rawProfile = JsonDocument.Parse(rawProfileJson);
        return sample with { RawAnalysisProfile = rawProfile.RootElement.Clone() };
    }
}
