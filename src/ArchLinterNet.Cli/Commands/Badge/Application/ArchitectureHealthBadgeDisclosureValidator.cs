using System.Buffers;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ArchLinterNet.Cli.Commands.Badge.Application;

/// <summary>
/// Validates a closed public badge profile from received UTF-8 bytes. It deliberately has no
/// access to Health receipts: relays verify representation and digest, not Health semantics.
/// </summary>
internal static partial class ArchitectureHealthBadgeDisclosureValidator
{
    internal const int MaximumPayloadBytes = 16 * 1024;

    internal static bool TryValidate(string profile, ReadOnlySpan<byte> utf8, out string sha256)
    {
        sha256 = string.Empty;
        if (utf8.Length == 0 || utf8.Length > MaximumPayloadBytes || !IsSupportedProfile(profile))
        {
            return false;
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(utf8.ToArray());
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !HasExactFields(root, profile))
            {
                return false;
            }

            int schemaVersion = RequiredInt(root, "schemaVersion");
            string label = RequiredString(root, "label");
            string message = RequiredString(root, "message");
            string color = RequiredString(root, "color");
            if (schemaVersion != 1 || label != "architecture" || !HasClosedHeadline(message, color))
            {
                return false;
            }

            string? verifiedAt = null;
            string? validUntil = null;
            if (profile == "headline-plus-freshness/v1")
            {
                verifiedAt = RequiredString(root, "verified_at");
                validUntil = RequiredString(root, "valid_until");
                DateTimeOffset verified = ParseUtcTimestamp(verifiedAt);
                DateTimeOffset valid = ParseUtcTimestamp(validUntil);
                if (valid <= verified || valid > verified.AddMinutes(60))
                {
                    return false;
                }
            }

            byte[] canonical = SerializeCanonical(message, color, verifiedAt, validUntil);
            if (!utf8.SequenceEqual(canonical))
            {
                return false;
            }

            sha256 = Convert.ToHexString(SHA256.HashData(utf8)).ToLowerInvariant();
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private static bool HasExactFields(JsonElement root, string profile)
    {
        string[] expected = profile == "headline-only/v1"
            ? ["schemaVersion", "label", "message", "color"]
            : ["schemaVersion", "label", "message", "color", "verified_at", "valid_until"];
        return root.EnumerateObject().Select(property => property.Name)
            .SequenceEqual(expected, StringComparer.Ordinal);
    }

    private static bool HasClosedHeadline(string message, string color)
    {
        Match match = HeadlinePattern().Match(message);
        if (!match.Success
            || !int.TryParse(match.Groups[3].Value, System.Globalization.NumberStyles.None,
                System.Globalization.CultureInfo.InvariantCulture, out _)
            || !int.TryParse(match.Groups[4].Value, System.Globalization.NumberStyles.None,
                System.Globalization.CultureInfo.InvariantCulture, out _))
        {
            return false;
        }

        return (match.Groups[2].Value, color) switch
        {
            ("HEALTHY", "brightgreen") => true,
            ("DEBT", "yellow") => true,
            ("DEGRADING", "orange") => true,
            ("FAILING", "red") => true,
            _ => false,
        };
    }

    private static byte[] SerializeCanonical(string message, string color, string? verifiedAt, string? validUntil)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteNumber("schemaVersion", 1);
            writer.WriteString("label", "architecture");
            writer.WriteString("message", message);
            writer.WriteString("color", color);
            if (verifiedAt is not null)
            {
                writer.WriteString("verified_at", verifiedAt);
                writer.WriteString("valid_until", validUntil);
            }

            writer.WriteEndObject();
        }

        return buffer.WrittenSpan.ToArray();
    }

    private static DateTimeOffset ParseUtcTimestamp(string value)
    {
        if (!DateTimeOffset.TryParseExact(
                value,
                "yyyy-MM-dd'T'HH:mm:ss'Z'",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal,
                out DateTimeOffset timestamp))
        {
            throw new InvalidOperationException("Freshness timestamps must use canonical UTC seconds.");
        }

        return timestamp;
    }

    private static int RequiredInt(JsonElement root, string name) =>
        root.TryGetProperty(name, out JsonElement value) && value.TryGetInt32(out int parsed)
            ? parsed
            : throw new InvalidOperationException($"Missing integer '{name}'.");

    private static string RequiredString(JsonElement root, string name) =>
        root.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String
            && value.GetString() is { Length: > 0 } parsed
            ? parsed
            : throw new InvalidOperationException($"Missing string '{name}'.");

    private static bool IsSupportedProfile(string profile) =>
        profile is "headline-only/v1" or "headline-plus-freshness/v1";

    [GeneratedRegex("^(PASS|FAIL) \\u00B7 (HEALTHY|DEBT|DEGRADING|FAILING) \\u00B7 ([0-9]+) ignores \\u00B7 ([0-9]+) rules$", RegexOptions.CultureInvariant)]
    private static partial Regex HeadlinePattern();
}
