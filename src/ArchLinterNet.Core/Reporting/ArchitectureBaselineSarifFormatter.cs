using System.Text.Json;
using ArchLinterNet.Core.Model;

namespace ArchLinterNet.Core.Reporting;

/// <summary>Projects baseline lifecycle entries to deterministic SARIF 2.1.0 results.</summary>
public static class ArchitectureBaselineSarifFormatter
{
    private const string SchemaUri =
        "https://raw.githubusercontent.com/oasis-tcs/sarif-spec/master/Schemata/sarif-schema-2.1.0.json";

    public static string Format(IReadOnlyList<BaselineLifecycleEntry> entries, string toolVersion)
    {
        var ordered = entries.OrderBy(entry => entry.Entry.ContractId, StringComparer.Ordinal)
            .ThenBy(entry => entry.Entry.SourceType, StringComparer.Ordinal)
            .ThenBy(entry => entry.Entry.ForbiddenReference, StringComparer.Ordinal)
            .ThenBy(entry => BaselineEntryLifecycleNames.WireName(entry.Lifecycle), StringComparer.Ordinal)
            .ToArray();
        object[] rules = ordered.Select(entry => entry.Entry.ContractId).Distinct(StringComparer.Ordinal)
            .OrderBy(id => id, StringComparer.Ordinal).Select(id => (object)new Dictionary<string, object?>
            {
                ["id"] = id, ["shortDescription"] = new Dictionary<string, object?> { ["text"] = id },
            }).ToArray();

        object[] results = ordered.Select(entry => (object)BuildResult(entry)).ToArray();
        return JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["$schema"] = SchemaUri,
            ["version"] = "2.1.0",
            ["runs"] = new object[] { new Dictionary<string, object?>
            {
                ["tool"] = new Dictionary<string, object?> { ["driver"] = new Dictionary<string, object?>
                {
                    ["name"] = "arch-linter-net", ["version"] = toolVersion, ["rules"] = rules,
                } },
                ["results"] = results,
            } },
        });
    }

    private static Dictionary<string, object?> BuildResult(BaselineLifecycleEntry lifecycle)
    {
        ArchitectureBaselineComparisonEntry entry = lifecycle.Entry;
        var properties = new Dictionary<string, object?>
        {
            ["baseline_status"] = BaselineEntryLifecycleNames.WireName(lifecycle.Lifecycle),
            ["contract_group"] = entry.ContractGroup,
            ["contract_id"] = entry.ContractId,
            ["source_type"] = entry.SourceType,
            ["forbidden_reference"] = entry.ForbiddenReference,
        };
        if (entry.Identity is { } identity)
        {
            properties["identity_version"] = identity.IdentityVersion;
            properties["contract_family"] = identity.ContractFamily;
            properties["kind"] = identity.Kind;
            properties["source_assembly"] = identity.SourceAssembly;
            properties["source_member"] = identity.SourceMember;
            properties["target_assembly"] = identity.TargetAssembly;
            properties["target_type"] = identity.TargetType;
            properties["target_member"] = identity.TargetMember;
            properties["occurrence"] = identity.Occurrence;
            properties["configuration"] = identity.Configuration;
        }

        return new Dictionary<string, object?>
        {
            ["ruleId"] = entry.ContractId,
            ["level"] = lifecycle.Lifecycle is BaselineEntryLifecycle.Matched ? "note" : "warning",
            ["message"] = new Dictionary<string, string> { ["text"] =
                $"[{BaselineEntryLifecycleNames.WireName(lifecycle.Lifecycle)}] {entry.SourceType} -> {entry.ForbiddenReference}" },
            ["logicalLocations"] = new object[] { new Dictionary<string, string> { ["fullyQualifiedName"] = entry.SourceType } },
            ["properties"] = properties,
        };
    }
}
