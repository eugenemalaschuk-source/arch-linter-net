using System.Text.Json.Nodes;
using ArchLinterNet.Core.Model;

namespace ArchLinterNet.Core.Reporting;

/// <summary>Renders the Core-owned effective-policy inventory.</summary>
internal static class ArchitecturePolicyInventoryRenderer
{
    internal static string RenderForHumans(ArchitecturePolicyInventory? inventory)
    {
        if (inventory is null)
        {
            return string.Empty;
        }

        ArchitecturePolicyInventoryRules rules = inventory.Rules;
        ArchitecturePolicyInventoryIgnoreDebt debt = inventory.IgnoreDebt;
        return $"Policy rules       {inventory.EffectiveRuleCount}"
            + $"  (strict {rules.Strict}, audit {rules.Audit}, coverage {rules.Coverage})"
            + Environment.NewLine
            + $"Waiver debt       {debt.Total}  ({FormatWaiverDebtBreakdown(debt)})";
    }

    internal static string AddToCiArtifacts(
        string ciArtifacts,
        ArchitecturePolicyInventory? inventory)
    {
        ArgumentNullException.ThrowIfNull(ciArtifacts);

        if (inventory is null)
        {
            return ciArtifacts;
        }

        JsonNode? parsed = JsonNode.Parse(ciArtifacts);
        if (parsed is not JsonObject payload)
        {
            throw new InvalidOperationException("CI artifact output must be a JSON object before policy inventory can be added.");
        }

        payload["policy_inventory"] = FormatForJson(inventory, inventory.Waivers);

        return payload.ToJsonString();
    }

    internal static JsonObject FormatForJson(
        ArchitecturePolicyInventory inventory,
        IEnumerable<ArchitectureWaiverLifecycleRecord> waivers)
    {
        ArchitecturePolicyInventoryRules rules = inventory.Rules;
        ArchitecturePolicyInventoryIgnoreDebt debt = inventory.IgnoreDebt;
        return new JsonObject
        {
            ["schema"] = inventory.SchemaId,
            ["effective_rule_count"] = inventory.EffectiveRuleCount,
            ["rules"] = new JsonObject
            {
                ["strict"] = rules.Strict,
                ["audit"] = rules.Audit,
                ["coverage"] = rules.Coverage,
            },
            ["ignore_debt"] = new JsonObject
            {
                ["total"] = debt.Total,
                ["active"] = debt.Active,
                ["stale"] = debt.Stale,
                ["expired"] = debt.Expired,
                ["metadata_incomplete"] = debt.MetadataIncomplete,
                ["invalid"] = debt.Invalid,
            },
            ["waivers"] = new JsonArray(waivers
                .Select(ArchitectureWaiverLifecycleRenderer.FormatWaiverForJson)
                .ToArray()),
        };
    }

    private static string FormatWaiverDebtBreakdown(ArchitecturePolicyInventoryIgnoreDebt debt)
    {
        var states = new List<string>();
        AddState(states, debt.Active, "active");
        AddState(states, debt.Stale, "stale");
        AddState(states, debt.Expired, "expired");
        AddState(states, debt.MetadataIncomplete, "metadata incomplete");
        AddState(states, debt.Invalid, "invalid");
        return states.Count == 0 ? "no explicit waivers" : string.Join(", ", states);
    }

    private static void AddState(List<string> states, int count, string state)
    {
        if (count > 0)
        {
            states.Add($"{count} {state}");
        }
    }
}
