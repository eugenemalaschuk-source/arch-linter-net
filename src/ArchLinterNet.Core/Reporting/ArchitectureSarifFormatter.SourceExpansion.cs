using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Model;

namespace ArchLinterNet.Core.Reporting;

public sealed partial class ArchitectureSarifFormatter
{
    /// <summary>
    /// Additive overload carrying the resolved source-set expansion, so a SARIF consumer can prove
    /// which sources an authored contract expanded to without parsing display text. Exists
    /// alongside the prior overloads rather than extending them, matching the pattern used for
    /// build-state preflight and coverage summaries.
    /// </summary>
    public string FormatResultAsSarif(
        string mode,
        IReadOnlyCollection<ArchitectureViolation> violations,
        IReadOnlyCollection<string> cycles,
        IReadOnlyCollection<BuildStatePreflightDiagnostic> preflightDiagnostics,
        IReadOnlyCollection<ArchitectureCoverageSummary> coverageSummaries,
        ArchitectureSourceExpansionInventory sourceExpansion,
        string toolVersion)
    {
        return FormatResultAsSarifCore(
            mode,
            violations,
            cycles.Select(cycle => (Func<string, ResultEntry>)(level => BuildCycleEntry(cycle, level))),
            toolVersion,
            preflightDiagnostics,
            coverageSummaries,
            sourceExpansion);
    }

    public static string FormatResultAsSarif(
        string mode,
        IReadOnlyCollection<ArchitectureViolation> violations,
        IReadOnlyCollection<ArchitectureCycleFinding> cycles,
        IReadOnlyCollection<BuildStatePreflightDiagnostic> preflightDiagnostics,
        IReadOnlyCollection<ArchitectureCoverageSummary> coverageSummaries,
        ArchitectureSourceExpansionInventory sourceExpansion,
        string toolVersion)
    {
        return FormatResultAsSarifCore(
            mode,
            violations,
            cycles.Select(cycle => (Func<string, ResultEntry>)(level =>
                BuildCycleEntry(ArchitectureDiagnosticMapper.FromCycle(cycle), level))),
            toolVersion,
            preflightDiagnostics,
            coverageSummaries,
            sourceExpansion);
    }

    internal static Dictionary<string, object?> FormatSourceExpansion(ArchitectureSourceExpansionInventory inventory)
    {
        return new Dictionary<string, object?>
        {
            ["sets"] = inventory.Sets.Select(set => (object)new Dictionary<string, object?>
            {
                ["name"] = set.Name,
                ["kind"] = set.Kind.ToString().ToLowerInvariant(),
                ["resolved_sources"] = set.ResolvedSources,
                ["optional"] = set.Optional,
                ["reason"] = set.Reason,
                ["policy_location"] = FormatSourceExpansionLocation(set.PolicyLocation)
            }).ToArray(),
            ["contracts"] = inventory.Contracts.Select(expansion => (object)new Dictionary<string, object?>
            {
                ["group"] = expansion.Group,
                ["authored_contract_id"] = expansion.AuthoredContractId,
                ["authored_contract_name"] = expansion.AuthoredContractName,
                ["kind"] = FormatExpansionKind(expansion.Kind),
                ["selector_field"] = expansion.SelectorField,
                ["source_sets"] = expansion.SetNames,
                ["optional_empty"] = expansion.OptionalEmpty,
                ["optional_reason"] = expansion.OptionalReason,
                ["policy_location"] = FormatSourceExpansionLocation(expansion.PolicyLocation),
                ["exclusions"] = expansion.Exclusions.Select(exclusion => (object)new Dictionary<string, object?>
                {
                    ["source"] = exclusion.Source,
                    ["source_set"] = exclusion.SetName,
                    ["selector"] = exclusion.Selector,
                    ["matched"] = exclusion.Matched,
                    ["policy_location"] = FormatSourceExpansionLocation(exclusion.PolicyLocation)
                }).ToArray(),
                ["instances"] = expansion.Instances.Select(instance => (object)new Dictionary<string, object?>
                {
                    ["contract_id"] = instance.ContractId,
                    ["source"] = instance.Source,
                    ["source_set"] = instance.SetName,
                    ["selector"] = instance.Selector
                }).ToArray()
            }).ToArray()
        };
    }

    private static Dictionary<string, object?>? FormatSourceExpansionLocation(ArchitecturePolicySourceLocation? location)
    {
        return location is null
            ? null
            : new Dictionary<string, object?>
            {
                ["source_path"] = location.SourcePath,
                ["yaml_path"] = location.YamlPath
            };
    }

    private static string FormatExpansionKind(ArchitectureContractExpansionKind kind) => kind switch
    {
        ArchitectureContractExpansionKind.FanOut => "fan_out",
        ArchitectureContractExpansionKind.InlineUnion => "inline_union",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
    };
}
