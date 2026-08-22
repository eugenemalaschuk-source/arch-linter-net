using System.Text;
using System.Text.Json;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.PolicyWeakening;
using ArchLinterNet.Core.Reporting;

namespace ArchLinterNet.Core.Validation;

/// <summary>Deterministic Human, JSON, and SARIF projections for one debt-gate result.</summary>
public static class ArchitectureDebtGateFormatter
{
    private const string SarifSchema =
        "https://raw.githubusercontent.com/oasis-tcs/sarif-spec/master/Schemata/sarif-schema-2.1.0.json";

    public static string FormatAsHuman(ArchitectureDebtGateOutcome outcome)
    {
        ArgumentNullException.ThrowIfNull(outcome);
        StringBuilder builder = new();
        builder.AppendLine("Architecture debt gate");
        builder.AppendLine($"Decision: {(outcome.Passed ? "pass" : "fail")}");
        builder.AppendLine("Evaluation:");
        builder.AppendLine($"- mode: {outcome.Evaluation.Mode}");
        builder.AppendLine($"- completed: {outcome.Evaluation.Completed.ToString().ToLowerInvariant()}");
        builder.AppendLine("Persistent debt:");
        builder.AppendLine($"- in sync: {outcome.PersistentDebt.InSync.ToString().ToLowerInvariant()}");
        foreach (BaselineLifecycleEntry entry in LifecycleEntries(outcome.PersistentDebt))
        {
            builder.AppendLine(
                $"- [{BaselineEntryLifecycleNames.WireName(entry.Lifecycle)}] {entry.Entry.SourceType} -> {entry.Entry.ForbiddenReference}");
        }

        builder.AppendLine("Policy weakening:");
        if (outcome.PolicyWeakening is null)
        {
            builder.AppendLine("- not requested");
        }
        else
        {
            builder.AppendLine($"- severity: {outcome.PolicyWeakening.Severity}");
            foreach (ArchitecturePolicyWeakeningFinding finding in OrderWeakening(outcome.PolicyWeakening.Findings))
            {
                builder.AppendLine($"- [{finding.Severity}] [{finding.Classification}] [{finding.Kind}] {finding.ControlIdentity}");
            }
        }

        return builder.ToString().TrimEnd();
    }

    public static string FormatAsJson(ArchitectureDebtGateOutcome outcome)
    {
        ArgumentNullException.ThrowIfNull(outcome);
        return JsonSerializer.Serialize(BuildJsonPayload(outcome));
    }

    public static string FormatAsSarif(ArchitectureDebtGateOutcome outcome, string toolVersion)
    {
        ArgumentNullException.ThrowIfNull(outcome);
        List<Dictionary<string, object?>> results = LifecycleEntries(outcome.PersistentDebt)
            .Select(BuildPersistentSarifResult)
            .Concat(outcome.PolicyWeakening is null
                ? Array.Empty<Dictionary<string, object?>>()
                : OrderWeakening(outcome.PolicyWeakening.Findings).Select(BuildWeakeningSarifResult))
            .OrderBy(result => (string)result["ruleId"]!, StringComparer.Ordinal)
            .ThenBy(result => JsonSerializer.Serialize(result["properties"]), StringComparer.Ordinal)
            .ToList();
        object[] rules = results.Select(result => (string)result["ruleId"]!)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(ruleId => ruleId, StringComparer.Ordinal)
            .Select(ruleId => (object)new Dictionary<string, object?>
            {
                ["id"] = ruleId,
                ["shortDescription"] = new Dictionary<string, string> { ["text"] = ruleId },
            })
            .ToArray();
        return JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["$schema"] = SarifSchema,
            ["version"] = "2.1.0",
            ["runs"] = new object[]
            {
                new Dictionary<string, object?>
                {
                    ["tool"] = new Dictionary<string, object?>
                    {
                        ["driver"] = new Dictionary<string, object?>
                        {
                            ["name"] = "arch-linter-net architecture debt gate",
                            ["version"] = toolVersion,
                            ["rules"] = rules,
                        },
                    },
                    ["results"] = results,
                },
            },
        });
    }

    private static object BuildJsonPayload(ArchitectureDebtGateOutcome outcome) => new Dictionary<string, object?>
    {
        ["schema_version"] = 1,
        ["kind"] = "architecture-debt-gate",
        ["succeeded"] = outcome.Succeeded,
        ["passed"] = outcome.Passed,
        ["evaluation"] = new Dictionary<string, object?>
        {
            ["completed"] = outcome.Evaluation.Completed,
            ["mode"] = outcome.Evaluation.Mode,
            ["preflight_diagnostics"] = outcome.Evaluation.PreflightDiagnostics,
        },
        ["persistent_debt"] = new Dictionary<string, object?>
        {
            ["in_sync"] = outcome.PersistentDebt.InSync,
            ["entries"] = LifecycleEntries(outcome.PersistentDebt).Select(BuildPersistentJsonEntry).ToArray(),
            ["configuration_violations"] = outcome.PersistentDebt.ConfigurationViolations,
        },
        ["policy_weakening"] = outcome.PolicyWeakening is null
            ? new Dictionary<string, object?> { ["requested"] = false }
            : new Dictionary<string, object?>
            {
                ["requested"] = true,
                ["schema_version"] = outcome.PolicyWeakening.SchemaVersion,
                ["kind"] = outcome.PolicyWeakening.Kind,
                ["policy_name"] = outcome.PolicyWeakening.PolicyName,
                ["policy_version"] = outcome.PolicyWeakening.PolicyVersion,
                ["severity"] = outcome.PolicyWeakening.Severity,
                ["findings"] = OrderWeakening(outcome.PolicyWeakening.Findings).Select(BuildWeakeningJsonEntry).ToArray(),
            },
    };

    private static Dictionary<string, object?> BuildPersistentJsonEntry(BaselineLifecycleEntry lifecycle)
    {
        ArchitectureBaselineComparisonEntry entry = lifecycle.Entry;
        return new Dictionary<string, object?>
        {
            ["status"] = BaselineEntryLifecycleNames.WireName(lifecycle.Lifecycle),
            ["contract_group"] = entry.ContractGroup,
            ["contract_id"] = entry.ContractId,
            ["source_type"] = entry.SourceType,
            ["forbidden_reference"] = entry.ForbiddenReference,
            ["reason"] = entry.Reason,
            ["issue"] = entry.Issue,
            ["identity"] = entry.Identity,
        };
    }

    private static Dictionary<string, object?> BuildWeakeningJsonEntry(ArchitecturePolicyWeakeningFinding finding) => new()
    {
        ["identity"] = finding.Identity,
        ["kind"] = finding.Kind,
        ["control_identity"] = finding.ControlIdentity,
        ["classification"] = finding.Classification,
        ["severity"] = finding.Severity,
        ["base_values"] = finding.BaseValues.OrderBy(value => value, StringComparer.Ordinal).ToArray(),
        ["current_values"] = finding.CurrentValues.OrderBy(value => value, StringComparer.Ordinal).ToArray(),
        ["affected_subjects"] = finding.AffectedSubjects.OrderBy(value => value, StringComparer.Ordinal).ToArray(),
        ["base_provenance"] = finding.BaseProvenance,
        ["current_provenance"] = finding.CurrentProvenance,
        ["rationale"] = finding.Rationale,
    };

    private static Dictionary<string, object?> BuildPersistentSarifResult(BaselineLifecycleEntry lifecycle)
    {
        ArchitectureBaselineComparisonEntry entry = lifecycle.Entry;
        Dictionary<string, object?> result = ArchitectureBaselineSarifFormatter.BuildResult(
            lifecycle,
            "ArchLinterNet.DebtGate.Persistent." + entry.ContractId);
        var properties = (Dictionary<string, object?>)result["properties"]!;
        properties["gate_section"] = "persistent_debt";
        properties["canonical_identity"] = ArchitectureFindingMapper.FromBaseline(lifecycle).CanonicalIdentity;
        return result;
    }

    private static Dictionary<string, object?> BuildWeakeningSarifResult(ArchitecturePolicyWeakeningFinding finding) => new()
    {
        ["ruleId"] = "ArchLinterNet.DebtGate.PolicyWeakening." + finding.Kind,
        ["level"] = finding.Severity switch { "error" => "error", "warn" => "warning", _ => "note" },
        ["message"] = new Dictionary<string, string> { ["text"] = $"{finding.Kind} weakens {finding.ControlIdentity}." },
        ["properties"] = new Dictionary<string, object?>
        {
            ["gate_section"] = "policy_weakening",
            ["identity"] = finding.Identity,
            ["kind"] = finding.Kind,
            ["control_identity"] = finding.ControlIdentity,
            ["classification"] = finding.Classification,
            ["severity"] = finding.Severity,
            ["base_values"] = finding.BaseValues,
            ["current_values"] = finding.CurrentValues,
            ["affected_subjects"] = finding.AffectedSubjects,
            ["base_provenance"] = finding.BaseProvenance,
            ["current_provenance"] = finding.CurrentProvenance,
            ["rationale"] = finding.Rationale,
        },
    };

    private static IReadOnlyList<BaselineLifecycleEntry> LifecycleEntries(BaselineVerifyOutcome outcome)
    {
        if (outcome.Entries.Count > 0)
        {
            return outcome.Entries;
        }

        return outcome.New.Select(entry => new BaselineLifecycleEntry(entry, BaselineEntryLifecycle.New))
            .Concat(outcome.Frozen.Select(entry => new BaselineLifecycleEntry(entry, BaselineEntryLifecycle.Matched)))
            .Concat(outcome.Resolved.Select(entry => new BaselineLifecycleEntry(entry, BaselineEntryLifecycle.Resolved)))
            .Concat(outcome.Ambiguous.Select(entry => new BaselineLifecycleEntry(entry, BaselineEntryLifecycle.Ambiguous)))
            .Concat(outcome.ConfigurationErrors.Select(entry => new BaselineLifecycleEntry(entry, BaselineEntryLifecycle.ConfigurationError)))
            .OrderBy(entry => entry.Entry.ContractGroup, StringComparer.Ordinal)
            .ThenBy(entry => entry.Entry.ContractId, StringComparer.Ordinal)
            .ThenBy(entry => entry.Entry.SourceType, StringComparer.Ordinal)
            .ThenBy(entry => entry.Entry.ForbiddenReference, StringComparer.Ordinal)
            .ToArray();
    }

    private static IEnumerable<ArchitecturePolicyWeakeningFinding> OrderWeakening(
        IEnumerable<ArchitecturePolicyWeakeningFinding> findings) => findings
        .OrderBy(finding => finding.Kind, StringComparer.Ordinal)
        .ThenBy(finding => finding.ControlIdentity, StringComparer.Ordinal)
        .ThenBy(finding => finding.Identity, StringComparer.Ordinal);
}
