using System.Text.Json;

namespace ArchLinterNet.Cli.Commands.Badge.Application.Setup;

internal sealed record BadgeDoctorObservationParseResult(
    bool IsValid,
    BadgeDoctorObservations? Observations,
    IReadOnlyList<BadgeSetupDiagnostic> Diagnostics);

internal static class BadgeDoctorObservationParser
{
    private const string Source = "live-doctor-inspector/v1";
    private static readonly TimeSpan _maximumAge = TimeSpan.FromMinutes(15);

    internal static BadgeDoctorObservationParseResult Parse(
        string source,
        BadgeSetupConfiguration configuration)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(source);
            JsonElement root = document.RootElement;
            string[] names =
            [
                "schema_id", "source", "observed_at", "repository", "repository_id", "repository_owner_id",
                "identity_valid", "pins_valid", "oidc_valid", "required_check_available", "rules_api_available",
                "destination_reachable", "first_evidence_available", "artifact_valid", "validity_current",
                "destination_revoked", "provider_quota_available", "cache_fresh",
            ];
            HashSet<string> allowed = new(names, StringComparer.Ordinal);
            HashSet<string> seen = new(StringComparer.Ordinal);
            if (root.ValueKind != JsonValueKind.Object
                || root.EnumerateObject().Any(property => !allowed.Contains(property.Name) || !seen.Add(property.Name)))
            {
                return Invalid("Observation has unsupported or duplicate properties.");
            }

            string schema = RequiredString(root, "schema_id");
            string observationSource = RequiredString(root, "source");
            DateTimeOffset observedAt = DateTimeOffset.Parse(RequiredString(root, "observed_at"), System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.RoundtripKind);
            string repository = RequiredString(root, "repository");
            long? repositoryId = ReadPositiveLong(root, "repository_id", configuration.Mode == BadgeSetupMode.Relay.ToWireValue());
            long? repositoryOwnerId = ReadPositiveLong(root, "repository_owner_id", configuration.Mode == BadgeSetupMode.Relay.ToWireValue());
            DateTimeOffset now = DateTimeOffset.UtcNow;
            if (schema != BadgeSetupContract.DoctorObservationSchemaId
                || observationSource != Source
                || observedAt < now - _maximumAge
                || observedAt > now.AddMinutes(5)
                || repository != $"{configuration.Repository.Owner}/{configuration.Repository.Name}"
                || configuration.Repository.RepositoryId is long configuredRepositoryId && repositoryId != configuredRepositoryId
                || configuration.Repository.RepositoryOwnerId is long configuredRepositoryOwnerId && repositoryOwnerId != configuredRepositoryOwnerId
                || configuration.Mode == BadgeSetupMode.Relay.ToWireValue() && (repositoryId is null || repositoryOwnerId is null))
            {
                return Invalid("Observation is stale or does not bind to the configured repository identity.");
            }

            return new(
                true,
                new(
                    DestinationReachable: RequiredBoolean(root, "destination_reachable"),
                    FirstEvidenceAvailable: RequiredBoolean(root, "first_evidence_available"),
                    ArtifactValid: RequiredBoolean(root, "artifact_valid"),
                    ValidityCurrent: RequiredBoolean(root, "validity_current"),
                    DestinationRevoked: RequiredBoolean(root, "destination_revoked"),
                    ProviderQuotaAvailable: RequiredBoolean(root, "provider_quota_available"),
                    IdentityValid: RequiredBoolean(root, "identity_valid"),
                    PinsValid: RequiredBoolean(root, "pins_valid"),
                    OidcValid: RequiredBoolean(root, "oidc_valid"),
                    RequiredCheckAvailable: RequiredBoolean(root, "required_check_available"),
                    RulesApiAvailable: RequiredBoolean(root, "rules_api_available"),
                    CacheFresh: RequiredBoolean(root, "cache_fresh")),
                []);
        }
        catch (Exception exception) when (exception is FormatException or JsonException or InvalidOperationException or OverflowException)
        {
            return Invalid($"Observation could not be parsed ({exception.GetType().Name}).");
        }
    }

    private static BadgeDoctorObservationParseResult Invalid(string detail) => new(
        false,
        null,
        [BadgeSetupDiagnosticCatalog.Create(BadgeSetupDiagnosticCodes.InvalidObservation, privateDetail: detail)]);

    private static string RequiredString(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out JsonElement value) || value.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(value.GetString()))
        {
            throw new InvalidOperationException($"Missing {name}.");
        }

        return value.GetString()!;
    }

    private static long? ReadPositiveLong(JsonElement root, string name, bool required)
    {
        if (!root.TryGetProperty(name, out JsonElement element) || element.ValueKind == JsonValueKind.Null)
        {
            return required
                ? throw new InvalidOperationException($"Missing {name}.")
                : null;
        }

        long value = element.TryGetInt64(out long parsed) ? parsed : 0;
        return value is > 0 and <= 9_007_199_254_740_991
            ? value
            : throw new InvalidOperationException($"Invalid {name}.");
    }

    private static bool RequiredBoolean(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out JsonElement element) || element.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
        {
            throw new InvalidOperationException($"Missing {name}.");
        }

        return element.GetBoolean();
    }
}
