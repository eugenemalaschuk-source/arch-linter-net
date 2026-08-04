using System.Text.Json;
using System.Text.Json.Serialization;

namespace ArchLinterNet.Core.Caching;

// A closed-set converter for ArchitectureClassificationRoleFact.Metadata's `object` value type —
// same defensive posture as AnalysisCacheDiagnosticPayloadConverter (never construct/deserialize an
// arbitrary CLR type from untrusted on-disk bytes). Per ArchitectureClassificationRoleFact's own
// remarks, Metadata values are always one of string/bool/decimal (attribute-role-extraction
// canonicalization never produces anything else), so this converter only ever reads/writes those
// three JSON shapes and fails closed (throws) on anything else — a tampered entry claiming an
// object/array value for a metadata entry is rejected as malformed, not silently coerced.
internal sealed class AnalysisCacheClassificationMetadataValueConverter : JsonConverter<object>
{
    public override object Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.TokenType switch
        {
            JsonTokenType.String => reader.GetString() ?? string.Empty,
            JsonTokenType.True => true,
            JsonTokenType.False => false,
            JsonTokenType.Number => reader.GetDecimal(),
            _ => throw new JsonException(
                $"Unsupported classification metadata value token '{reader.TokenType}' — only string/bool/decimal are valid."),
        };
    }

    public override void Write(Utf8JsonWriter writer, object value, JsonSerializerOptions options)
    {
        switch (value)
        {
            case string stringValue:
                writer.WriteStringValue(stringValue);
                break;
            case bool boolValue:
                writer.WriteBooleanValue(boolValue);
                break;
            case decimal decimalValue:
                writer.WriteNumberValue(decimalValue);
                break;
            default:
                throw new JsonException(
                    $"Unsupported classification metadata value type '{value.GetType()}' — only string/bool/decimal are valid.");
        }
    }
}
