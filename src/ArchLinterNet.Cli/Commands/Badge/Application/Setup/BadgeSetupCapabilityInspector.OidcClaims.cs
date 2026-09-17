using System.Globalization;
using System.Text.Json;

namespace ArchLinterNet.Cli.Commands.Badge.Application.Setup;

internal static class BadgeSetupOidcClaims
{
    internal static bool HasExpectedOidcClaims(
        string response,
        BadgeSetupConfiguration configuration,
        string oidcIssuer,
        out string? observedSubject)
    {
        observedSubject = null;
        string? token = ExtractOidcToken(response);

        if (string.IsNullOrWhiteSpace(token) || token.Length > 16 * 1024)
        {
            return false;
        }

        string[] segments = token.Split('.');
        if (segments.Length != 3 || segments.Any(string.IsNullOrEmpty))
        {
            return false;
        }

        try
        {
            if (!TryDecodeBase64Url(segments[1], out byte[] payload))
            {
                return false;
            }

            using JsonDocument document = JsonDocument.Parse(payload);
            JsonElement claims = document.RootElement;
            string expectedWorkflowRef = $"{configuration.Pins?.WorkflowRef}@{configuration.Pins?.WorkflowSha}";
            string defaultSubject = $"repo:{configuration.Repository.Owner}/{configuration.Repository.Name}:ref:refs/heads/{configuration.BaseRef}";
            string? actualSubject = StringClaim(claims, "sub");
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            bool bootstrap = string.Equals(
                Environment.GetEnvironmentVariable("ARCHLINTERNET_BOOTSTRAP"),
                "1",
                StringComparison.Ordinal);
            string expectedEvent = bootstrap ? "workflow_dispatch" : "push";
            bool valid = StringClaim(claims, "iss") == oidcIssuer
                && AudienceClaimMatches(claims, configuration.Destination.Audience)
                && PositiveClaim(claims, "repository_id") == configuration.Repository.RepositoryId
                && PositiveClaim(claims, "repository_owner_id") == configuration.Repository.RepositoryOwnerId
                && StringClaim(claims, "repository") == $"{configuration.Repository.Owner}/{configuration.Repository.Name}"
                && StringClaim(claims, "repository_visibility") == configuration.Repository.Visibility
                && StringClaim(claims, "event_name") == expectedEvent
                && StringClaim(claims, "ref") == $"refs/heads/{configuration.BaseRef}"
                && StringClaim(claims, "job_workflow_ref") == expectedWorkflowRef
                && StringClaim(claims, "job_workflow_sha") == configuration.Pins?.WorkflowSha
                && BadgeSetupValidationHelpers.IsSafeOidcSubject(actualSubject)
                && HasValidTimeClaims(claims, now);
            if (!valid)
            {
                return false;
            }

            if (configuration.Destination.Subject is not null)
            {
                return string.Equals(actualSubject, configuration.Destination.Subject, StringComparison.Ordinal);
            }

            if (string.Equals(actualSubject, defaultSubject, StringComparison.Ordinal))
            {
                return true;
            }

            // GitHub permits organizations to configure an OIDC subject template. Record
            // only the fully verified bootstrap subject, then require this exact value for
            // every subsequent publisher invocation.
            observedSubject = actualSubject;
            return true;
        }
        catch (Exception exception) when (exception is FormatException or JsonException or InvalidOperationException)
        {
            return false;
        }
    }

    private static bool HasValidTimeClaims(JsonElement claims, long now)
    {
        long issued = IntegerClaim(claims, "iat");
        long expires = IntegerClaim(claims, "exp");
        long notBefore = IntegerClaim(claims, "nbf");
        return issued <= now + 300
            && notBefore <= now + 300
            && expires >= now - 300
            && expires > issued
            && expires - issued <= 600;
    }

    private static bool AudienceClaimMatches(JsonElement claims, string? expected)
    {
        if (string.IsNullOrWhiteSpace(expected) || !claims.TryGetProperty("aud", out JsonElement value))
        {
            return false;
        }

        if (value.ValueKind == JsonValueKind.String)
        {
            return value.GetString() == expected;
        }

        return value.ValueKind == JsonValueKind.Array
            && value.GetArrayLength() > 0
            && value.EnumerateArray().All(static item => item.ValueKind == JsonValueKind.String)
            && value.EnumerateArray().Any(item => item.GetString() == expected);
    }

    internal static string? StringClaim(JsonElement claims, string name) =>
        claims.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static long? PositiveClaim(JsonElement claims, string name)
    {
        if (!claims.TryGetProperty(name, out JsonElement value))
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out long numeric))
        {
            return BadgeSetupValidationHelpers.IsSafePositiveId(numeric) ? numeric : null;
        }

        if (value.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        string? text = value.GetString();
        if (string.IsNullOrEmpty(text)
            || text[0] == '0'
            || text.Any(static character => !char.IsAsciiDigit(character))
            || !long.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out long parsed)
            || !BadgeSetupValidationHelpers.IsSafePositiveId(parsed)
            || !string.Equals(parsed.ToString(CultureInfo.InvariantCulture), text, StringComparison.Ordinal))
        {
            return null;
        }

        return parsed;
    }

    private static long IntegerClaim(JsonElement claims, string name) =>
        claims.TryGetProperty(name, out JsonElement value) && value.TryGetInt64(out long parsed)
            ? parsed
            : throw new InvalidOperationException($"Missing {name} claim.");

    internal static bool TryDecodeBase64Url(string value, out byte[] bytes)
    {
        bytes = [];
        if (value.Length == 0 || value.Any(static character =>
                !(char.IsAsciiLetterOrDigit(character) || character is '-' or '_')) || value.Length % 4 == 1)
        {
            return false;
        }

        string encoded = value.Replace('-', '+').Replace('_', '/');
        encoded += new string('=', (4 - encoded.Length % 4) % 4);
        try
        {
            bytes = Convert.FromBase64String(encoded);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    internal static string? ExtractOidcToken(string response)
    {
        string trimmed = response.Trim();
        if ((trimmed.Length == 0 || trimmed[0] != '{')
            && trimmed.Count(static character => character == '.') == 2)
        {
            return trimmed;
        }

        try
        {
            using JsonDocument envelope = JsonDocument.Parse(response);
            if (envelope.RootElement.ValueKind != JsonValueKind.Object
                || !envelope.RootElement.TryGetProperty("value", out JsonElement value)
                || value.ValueKind != JsonValueKind.String)
            {
                return null;
            }

            return value.GetString();
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
