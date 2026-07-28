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
        var ordered = entries.OrderBy(entry => entry.Entry.ContractGroup, StringComparer.Ordinal)
            .ThenBy(entry => entry.Entry.ContractId, StringComparer.Ordinal)
            .ThenBy(entry => entry.Entry.SourceType, StringComparer.Ordinal)
            .ThenBy(entry => entry.Entry.ForbiddenReference, StringComparer.Ordinal)
            .ThenBy(entry => entry.Entry.Identity, ArchitectureViolationIdentityComparer.Instance)
            .ThenBy(entry => BaselineEntryLifecycleNames.WireName(entry.Lifecycle), StringComparer.Ordinal)
            .ToArray();
        object[] rules = ordered.Select(entry => entry.Entry.ContractId).Distinct(StringComparer.Ordinal)
            .OrderBy(id => id, StringComparer.Ordinal).Select(id => (object)new Dictionary<string, object?>
            {
                ["id"] = id,
                ["shortDescription"] = new Dictionary<string, object?>
                {
                    ["text"] = id,
                },
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

    private sealed class ArchitectureViolationIdentityComparer : IComparer<ArchitectureViolationIdentity?>
    {
        public static ArchitectureViolationIdentityComparer Instance { get; } = new();

        public int Compare(ArchitectureViolationIdentity? left, ArchitectureViolationIdentity? right)
        {
            if (ReferenceEquals(left, right))
            {
                return 0;
            }

            if (left is null)
            {
                return -1;
            }

            if (right is null)
            {
                return 1;
            }

            return CompareFields(
                left.IdentityVersion, right.IdentityVersion,
                left.ContractFamily, right.ContractFamily,
                left.Kind, right.Kind,
                left.ContractId, right.ContractId,
                left.SourceAssembly, right.SourceAssembly,
                left.SourceType, right.SourceType,
                left.SourceMember, right.SourceMember,
                left.TargetAssembly, right.TargetAssembly,
                left.TargetType, right.TargetType,
                left.TargetMember, right.TargetMember,
                left.Occurrence, right.Occurrence,
                left.Configuration, right.Configuration);
        }

        private static int CompareFields(params object?[] fields)
        {
            for (int index = 0; index < fields.Length; index += 2)
            {
                int comparison = fields[index] switch
                {
                    int left => left.CompareTo((int)fields[index + 1]!),
                    string left => CompareNullableStrings(left, (string?)fields[index + 1]),
                    null => fields[index + 1] is null ? 0 : -1,
                    _ => throw new InvalidOperationException("Unsupported canonical identity field."),
                };
                if (comparison != 0)
                {
                    return comparison;
                }
            }

            return 0;
        }

        private static int CompareNullableStrings(string? left, string? right)
        {
            return left is null ? right is null ? 0 : -1 : right is null ? 1 : StringComparer.Ordinal.Compare(left, right);
        }
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
            ["arch_linter_net"] = new Dictionary<string, object?>
            {
                ["schema_version"] = ArchitectureFinding.CurrentSchemaVersion,
                ["kind"] = "baseline",
                ["canonical_identity"] = entry.Identity?.ToString()
                    ?? $"{entry.ContractGroup}:{entry.ContractId}:{entry.SourceType}:{entry.ForbiddenReference}",
                ["baseline_state"] = BaselineEntryLifecycleNames.WireName(lifecycle.Lifecycle),
                ["details"] = new Dictionary<string, object?>
                {
                    ["contract_group"] = entry.ContractGroup,
                    ["source_type"] = entry.SourceType,
                    ["forbidden_reference"] = entry.ForbiddenReference,
                    ["identity"] = entry.Identity,
                },
            },
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
            ["message"] = new Dictionary<string, string>
            {
                ["text"] =
                    $"[{BaselineEntryLifecycleNames.WireName(lifecycle.Lifecycle)}] {entry.SourceType} -> {entry.ForbiddenReference}",
            },
            ["locations"] = new object[]
            {
                new Dictionary<string, object?>
                {
                    ["logicalLocations"] = new object[]
                    {
                        new Dictionary<string, string> { ["fullyQualifiedName"] = entry.SourceType },
                    },
                },
            },
            ["properties"] = properties,
        };
    }
}
