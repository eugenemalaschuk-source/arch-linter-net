using System.Text.Json;
using ArchLinterNet.Cli.Commands;

namespace ArchLinterNet.Cli.Commands.Badge.Application;

internal sealed record ArchitectureHealthBadgeProjection(string Message, string Color, int ExitCode);

internal static class ArchitectureHealthBadgeProjector
{
    private const string HealthSchema = "architecture-health/v1";
    private const string InventorySchema = "architecture-policy-inventory/v1";

    internal static ArchitectureHealthBadgeProjection Project(string input)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(input);
            JsonElement root = document.RootElement;
            RequireString(root, "schema_id", HealthSchema);
            string gate = RequiredString(root, "gate");
            string health = RequiredString(root, "health");
            if (gate == "unassessable" || health == "unassessable")
            {
                return Unassessable();
            }

            (int ignores, int rules) = ReadInventory(root);
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

    private static (int Ignores, int Rules) ReadInventory(JsonElement root)
    {
        JsonElement evidence = Required(root, "report_evidence", JsonValueKind.Object);
        JsonElement outcomes = Required(evidence, "validation_outcomes", JsonValueKind.Array);
        List<(int Ignores, int Rules)> inventories = [];
        foreach (JsonElement outcome in outcomes.EnumerateArray())
        {
            if (outcome.ValueKind != JsonValueKind.Object
                || !outcome.TryGetProperty("policy_inventory", out JsonElement inventory))
            {
                continue;
            }

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
        "unassessable" => CliExitCodes.InvalidArgumentsOrRuntimeError,
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
