using System.Text.Json;
using System.Text.Json.Nodes;
using ArchLinterNet.Core.Model;

namespace ArchLinterNet.Core.Reporting;

public sealed partial class ArchitectureDiagnosticFormatter
{
    /// <summary>Renders the canonical effective-policy inventory in a compact human form.</summary>
    public static string FormatPolicyInventoryForHumans(ArchitecturePolicyInventory? inventory)
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

    /// <summary>
    /// Adds the Core-owned policy inventory to a completed CI-artifact JSON document. A null
    /// inventory deliberately remains absent so a cache-era result is never mistaken for a
    /// zero-control, zero-debt policy.
    /// </summary>
    public static string AddPolicyInventoryToCiArtifacts(
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

        ArchitecturePolicyInventoryRules rules = inventory.Rules;
        ArchitecturePolicyInventoryIgnoreDebt debt = inventory.IgnoreDebt;
        payload["policy_inventory"] = new JsonObject
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
            ["waivers"] = new JsonArray(inventory.Waivers
                .Select(FormatWaiverForJson)
                .ToArray()),
        };

        return payload.ToJsonString();
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

    private static JsonNode FormatWaiverForJson(ArchitectureWaiverLifecycleRecord waiver) => new JsonObject
    {
        ["id"] = waiver.Id,
        ["state"] = waiver.State,
        ["contract"] = waiver.ContractName,
        ["contract_id"] = waiver.ContractId,
        ["contract_group"] = waiver.ContractGroup,
        ["source_type"] = waiver.SourceType,
        ["forbidden_reference"] = waiver.ForbiddenReference,
        ["target_fingerprint"] = waiver.TargetFingerprint,
        ["reason"] = waiver.Reason,
        ["owner"] = waiver.Owner,
        ["issue"] = waiver.Issue,
        ["introduced"] = FormatDate(waiver.Introduced),
        ["expires"] = FormatDate(waiver.Expires),
        ["evaluation_date"] = waiver.EvaluationDate.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
        ["matches_governed_finding"] = waiver.MatchesGovernedFinding,
        ["policy_location"] = waiver.PolicyLocation is null
            ? null
            : JsonSerializer.SerializeToNode(FormatPolicyLocationForJson(waiver.PolicyLocation)),
    };
}
