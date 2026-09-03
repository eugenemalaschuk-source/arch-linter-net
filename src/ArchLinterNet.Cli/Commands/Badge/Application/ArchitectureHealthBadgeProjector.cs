using System.Text.Json;
using ArchLinterNet.Cli.Commands;

namespace ArchLinterNet.Cli.Commands.Badge.Application;

internal sealed record ArchitectureHealthBadgeProjection(string Message, string Color, int ExitCode);

internal static class ArchitectureHealthBadgeProjector
{
    private const string HealthSchema = "architecture-health/v1";
    private const string InventorySchema = "architecture-policy-inventory/v1";
    private const int ReportEvidenceSchemaVersion = 2;
    private const string ReportEvidenceKind = "architecture-health-report-evidence";

    internal static ArchitectureHealthBadgeProjection Project(string input)
    {
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
                return Unassessable();
            }

            (int ignores, int rules) = ReadInventory(evidence);
            return health switch
            {
                "healthy" => new ArchitectureHealthBadgeProjection(
                    $"HEALTHY · {ignores} ignores · {rules} rules", "brightgreen", ExitCode(gate)),
                "debt" => new ArchitectureHealthBadgeProjection(
                    $"DEBT · {ignores} ignores · {rules} rules", "yellow", ExitCode(gate)),
                "degrading" => new ArchitectureHealthBadgeProjection(
                    $"DEGRADING · {ignores} ignores · {rules} rules", "orange", ExitCode(gate)),
                "failing" => new ArchitectureHealthBadgeProjection(
                    $"FAILING · {ignores} ignores · {rules} rules", "red", ExitCode(gate)),
                _ => Unassessable(),
            };
        }
        catch (JsonException)
        {
            return Unassessable();
        }
        catch (InvalidOperationException)
        {
            return Unassessable();
        }
    }

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
            RequiredObjectReceipt(outcome, "validation outcome");
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

    internal static ArchitectureHealthBadgeProjection Unassessable() =>
        new("UNASSESSABLE · ? ignores · ? rules", "lightgrey", CliExitCodes.InvalidArgumentsOrRuntimeError);

    private static int ExitCode(string gate) => gate switch
    {
        "pass" => CliExitCodes.Success,
        "fail" => CliExitCodes.ValidationFailure,
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

    private static void RequiredObjectReceipt(JsonElement element, string description)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException($"Malformed {description}.");
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
