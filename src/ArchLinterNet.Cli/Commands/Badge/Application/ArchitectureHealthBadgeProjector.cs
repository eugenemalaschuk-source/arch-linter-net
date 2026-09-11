using System.Globalization;
using System.Text.Json;
using ArchLinterNet.Cli.Commands;

namespace ArchLinterNet.Cli.Commands.Badge.Application;

internal sealed record ArchitectureHealthBadgeProjection(
    string Message,
    string Color,
    int ExitCode,
    string? VerifiedAt = null,
    string? ValidUntil = null,
    string? DisclosureProfile = null,
    string? Diagnostic = null);

internal static class ArchitectureHealthBadgeProjector
{
    private const string HealthSchema = "architecture-health/v1";
    private const string InventorySchema = "architecture-policy-inventory/v1";
    private const int ReportEvidenceSchemaVersion = 2;
    private const string ReportEvidenceKind = "architecture-health-report-evidence";

    internal static ArchitectureHealthBadgeProjection Project(
        string input,
        string? disclosureProfile = null,
        string? verifiedAt = null)
    {
        if (disclosureProfile is not null && disclosureProfile is not ("headline-only/v1" or "headline-plus-freshness/v1"))
        {
            return Unassessable($"Unsupported disclosure profile '{disclosureProfile}'.");
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(input);
            JsonElement root = document.RootElement;
            RequireString(root, "schema_id", HealthSchema);
            string gate = RequiredString(root, "gate");
            string health = RequiredString(root, "health");
            JsonElement evidence = ReadCanonicalEvidence(root, gate, health);
            if (gate == "unassessable" || health == "unassessable")
            {
                return disclosureProfile is null
                    ? Unassessable()
                    : Unassessable("Architecture Health itself is unassessable and cannot produce a ready disclosure profile.");
            }

            (int ignores, int rules) = ReadInventory(evidence);
            if (disclosureProfile is not null && (ignores > 9999 || rules > 9999))
            {
                return Unassessable("Canonical disclosure counts must be in the range 0 through 9999.");
            }

            ArchitectureHealthBadgeProjection projection = health switch
            {
                "healthy" => Headline(gate, "HEALTHY", ignores, rules, "brightgreen"),
                "debt" => Headline(gate, "DEBT", ignores, rules, "yellow"),
                "degrading" => Headline(gate, "DEGRADING", ignores, rules, "orange"),
                "failing" => Headline(gate, "FAILING", ignores, rules, "red"),
                _ => Unassessable(),
            };
            return ProjectProfile(evidence, projection, disclosureProfile, verifiedAt);
        }
        catch (JsonException)
        {
            return Unassessable("Architecture Health input is not valid JSON.");
        }
        catch (InvalidOperationException exception)
        {
            return Unassessable(exception.Message);
        }
    }

    private static ArchitectureHealthBadgeProjection Headline(
        string gate,
        string health,
        int ignores,
        int rules,
        string color) =>
        new($"{GateName(gate)} \u00B7 {health} \u00B7 {ignores} ignores \u00B7 {rules} rules", color, ExitCode(gate));

    private static ArchitectureHealthBadgeProjection ProjectProfile(
        JsonElement evidence,
        ArchitectureHealthBadgeProjection projection,
        string? disclosureProfile,
        string? verifiedAt)
    {
        if (disclosureProfile is null)
        {
            return string.IsNullOrWhiteSpace(verifiedAt) ? projection : Unassessable();
        }

        JsonElement publication;
        try
        {
            publication = Required(evidence, "publication_evidence", JsonValueKind.Object);
            RequireString(publication, "schema_id", "architecture-health-publication-evidence/v1");
            RequireString(publication, "state", "ready");
            JsonElement reasons = Required(publication, "reasons", JsonValueKind.Array);
            if (reasons.GetArrayLength() != 0)
            {
                return Unassessable("Publication evidence is not ready because it contains disqualifying reasons.");
            }
        }
        catch (InvalidOperationException exception)
        {
            return Unassessable($"Publication evidence is missing, legacy, or unsupported: {exception.Message}");
        }

        DateTimeOffset horizon;
        try
        {
            horizon = ParseUtcTimestamp(RequiredString(publication, "semantic_horizon"));
        }
        catch (InvalidOperationException exception)
        {
            return Unassessable($"Publication evidence has no canonical semantic horizon: {exception.Message}");
        }
        if (disclosureProfile == "headline-only/v1")
        {
            return string.IsNullOrWhiteSpace(verifiedAt)
                ? projection with { DisclosureProfile = disclosureProfile }
                : Unassessable("headline-only/v1 does not permit --verified-at.");
        }

        DateTimeOffset verified = ParseUtcTimestamp(verifiedAt ?? string.Empty);
        if (verified >= horizon)
        {
            return Unassessable("--verified-at must be earlier than the publication semantic horizon.");
        }

        DateTimeOffset validUntil = verified.AddMinutes(60) < horizon ? verified.AddMinutes(60) : horizon;
        return projection with
        {
            VerifiedAt = FormatUtcTimestamp(verified),
            ValidUntil = FormatUtcTimestamp(validUntil),
            DisclosureProfile = disclosureProfile,
        };
    }

    private static DateTimeOffset ParseUtcTimestamp(string value)
    {
        if (!DateTimeOffset.TryParseExact(
                value,
                "yyyy-MM-dd'T'HH:mm:ss'Z'",
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out DateTimeOffset timestamp))
        {
            throw new InvalidOperationException("Publication timestamps must use canonical UTC seconds.");
        }

        return timestamp;
    }

    private static string FormatUtcTimestamp(DateTimeOffset value) =>
        value.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);

    private static JsonElement ReadCanonicalEvidence(JsonElement root, string gate, string health)
    {
        JsonElement evidence = Required(root, "report_evidence", JsonValueKind.Object);
        RequiredInt(evidence, "schema_version", ReportEvidenceSchemaVersion);
        RequireString(evidence, "kind", ReportEvidenceKind);
        RequireString(evidence, "gate", gate);
        RequireString(evidence, "health", health);
        return evidence;
    }

    private static (int Ignores, int Rules) ReadInventory(JsonElement evidence)
    {
        JsonElement outcomes = Required(evidence, "validation_outcomes", JsonValueKind.Array);
        List<(int Ignores, int Rules)> inventories = [];
        foreach (JsonElement outcome in outcomes.EnumerateArray())
        {
            if (outcome.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidOperationException("Malformed validation outcome.");
            }

            _ = RequiredString(outcome, "mode");
            JsonElement availability = Required(outcome, "availability", JsonValueKind.Object);
            RequireString(availability, "policy_inventory", "available");
            _ = Required(outcome, "findings", JsonValueKind.Array);
            _ = Required(outcome, "provenance", JsonValueKind.Object);
            JsonElement inventory = Required(outcome, "policy_inventory", JsonValueKind.Object);
            RequireString(inventory, "schema", InventorySchema);
            int rules = RequiredNonNegativeInt(inventory, "effective_rule_count");
            JsonElement debt = Required(inventory, "ignore_debt", JsonValueKind.Object);
            int ignores = RequiredNonNegativeInt(debt, "total");
            inventories.Add((ignores, rules));
        }

        if (inventories.Count == 0 || inventories.Any(candidate => candidate != inventories[0]))
        {
            throw new InvalidOperationException("Canonical policy inventory is unavailable or inconsistent.");
        }

        return inventories[0];
    }

    internal static ArchitectureHealthBadgeProjection Unassessable(string? diagnostic = null) =>
        new(
            "UNASSESSABLE \u00B7 ? ignores \u00B7 ? rules",
            "lightgrey",
            CliExitCodes.InvalidArgumentsOrRuntimeError,
            Diagnostic: diagnostic);

    private static int ExitCode(string gate) => gate switch
    {
        "pass" => CliExitCodes.Success,
        "fail" => CliExitCodes.ValidationFailure,
        _ => throw new InvalidOperationException($"Unsupported Architecture Health gate '{gate}'."),
    };

    private static string GateName(string gate) => gate switch
    {
        "pass" => "PASS",
        "fail" => "FAIL",
        _ => throw new InvalidOperationException($"Unsupported Architecture Health gate '{gate}'."),
    };

    private static JsonElement Required(JsonElement element, string name, JsonValueKind kind)
    {
        if (!element.TryGetProperty(name, out JsonElement value) || value.ValueKind != kind)
        {
            throw new InvalidOperationException($"Missing required {name} value.");
        }

        return value;
    }

    private static int RequiredNonNegativeInt(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out JsonElement value) || !value.TryGetInt32(out int parsed) || parsed < 0)
        {
            throw new InvalidOperationException($"Missing non-negative {name} value.");
        }

        return parsed;
    }

    private static void RequiredInt(JsonElement element, string name, int expected)
    {
        if (!element.TryGetProperty(name, out JsonElement value) || !value.TryGetInt32(out int parsed)
            || parsed != expected)
        {
            throw new InvalidOperationException($"Unsupported {name} value.");
        }
    }

    private static string RequiredString(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out JsonElement value) || value.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(value.GetString()))
        {
            throw new InvalidOperationException($"Missing required {name} value.");
        }

        return value.GetString()!;
    }

    private static void RequireString(JsonElement element, string name, string expected)
    {
        if (!string.Equals(RequiredString(element, name), expected, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Unsupported {name} value.");
        }
    }
}
