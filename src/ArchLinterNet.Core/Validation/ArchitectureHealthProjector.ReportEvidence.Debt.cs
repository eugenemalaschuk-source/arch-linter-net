using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.PolicyContext;
using ArchLinterNet.Core.PolicyWeakening;
using ArchLinterNet.Core.Reporting;

namespace ArchLinterNet.Core.Validation;

public static partial class ArchitectureHealthProjector
{
    private static JsonObject BuildDebtGateEvidence(ArchitectureDebtGateOutcome outcome)
    {
        var result = new JsonObject
        {
            ["succeeded"] = outcome.Succeeded,
            ["passed"] = outcome.Passed,
            ["evaluation"] = new JsonObject
            {
                ["completed"] = outcome.Evaluation.Completed,
                ["mode"] = outcome.Evaluation.Mode,
                ["reused_analysis_snapshot"] = outcome.Evaluation.ReusedAnalysisSnapshot,
                ["preflight_diagnostics"] = BuildPreflightFindings(
                    outcome.Evaluation.PreflightDiagnostics, outcome.Evaluation.Mode),
            },
            ["persistent_debt"] = BuildPersistentDebt(outcome.PersistentDebt),
            ["policy_weakening"] = BuildPolicyWeakening(outcome),
        };
        return result;
    }

    private static JsonObject BuildPersistentDebt(BaselineVerifyOutcome outcome)
    {
        var entries = new JsonArray();
        foreach (BaselineLifecycleEntry lifecycle in LifecycleEntries(outcome))
        {
            ArchitectureBaselineComparisonEntry entry = lifecycle.Entry;
            entries.Add(new JsonObject
            {
                ["status"] = BaselineEntryLifecycleNames.WireName(lifecycle.Lifecycle),
                ["disposition"] = BaselineEntryDispositionNames.WireName(lifecycle.Disposition),
                ["contract_group"] = entry.ContractGroup,
                ["contract_id"] = entry.ContractId,
                ["source_type"] = entry.SourceType,
                ["forbidden_reference"] = entry.ForbiddenReference,
                ["reason"] = entry.Reason,
                ["issue"] = entry.Issue,
                ["current_forbidden_reference"] = entry.CurrentForbiddenReference,
                ["identity"] = entry.Identity is null ? null : ArchitectureViolationIdentityJson.Serialize(entry.Identity),
            });
        }

        return new JsonObject
        {
            ["succeeded"] = outcome.Succeeded,
            ["in_sync"] = outcome.InSync,
            ["entries"] = entries,
            ["configuration_violations"] = BuildFindings(
                ArchitectureFindingMapper.FromViolations(outcome.ConfigurationViolations, mode: null), mode: null),
        };
    }

    private static JsonObject BuildPolicyWeakening(ArchitectureDebtGateOutcome outcome)
    {
        if (outcome.PolicyWeakening is null)
        {
            return new JsonObject { ["requested"] = outcome.PolicyWeakeningRequested };
        }

        ArchitecturePolicyWeakeningResult weakening = outcome.PolicyWeakening;
        var findings = new JsonArray();
        foreach (ArchitecturePolicyWeakeningFinding finding in weakening.Findings
            .OrderBy(item => item.Kind, StringComparer.Ordinal)
            .ThenBy(item => item.ControlIdentity, StringComparer.Ordinal)
            .ThenBy(item => item.Identity, StringComparer.Ordinal))
        {
            findings.Add(new JsonObject
            {
                ["identity"] = finding.Identity,
                ["kind"] = finding.Kind,
                ["control_identity"] = finding.ControlIdentity,
                ["classification"] = finding.Classification,
                ["severity"] = finding.Severity,
                ["base_values"] = ToStringArray(finding.BaseValues),
                ["current_values"] = ToStringArray(finding.CurrentValues),
                ["affected_subjects"] = ToStringArray(finding.AffectedSubjects),
                ["base_provenance"] = BuildPolicyContextProvenance(finding.BaseProvenance),
                ["current_provenance"] = BuildPolicyContextProvenance(finding.CurrentProvenance),
                ["rationale"] = finding.Rationale,
            });
        }

        return new JsonObject
        {
            ["requested"] = true,
            ["schema_version"] = weakening.SchemaVersion,
            ["kind"] = weakening.Kind,
            ["policy_name"] = weakening.PolicyName,
            ["policy_version"] = weakening.PolicyVersion,
            ["severity"] = weakening.Severity,
            ["findings"] = findings,
        };
    }

    private static JsonObject? BuildPolicyContextProvenance(
        ArchitecturePolicyContextProvenance? provenance) => provenance is null
        ? null
        : new JsonObject
        {
            ["source_path"] = provenance.SourcePath,
            ["root_path"] = provenance.RootPath,
            ["role"] = provenance.Role,
            ["yaml_path"] = provenance.YamlPath,
            ["source_order"] = provenance.SourceOrder,
        };

    private static JsonArray BuildPreflightFindings(
        IEnumerable<BuildStatePreflightDiagnostic> diagnostics,
        string mode) => BuildFindings(diagnostics.Select(item => ArchitectureFindingMapper.FromDiagnostic(item, mode)), mode);

    private static IReadOnlyList<BaselineLifecycleEntry> LifecycleEntries(BaselineVerifyOutcome outcome)
    {
        if (outcome.Entries.Count > 0)
        {
            return outcome.Entries
                .OrderBy(item => item.Entry.Identity?.ToString(), StringComparer.Ordinal)
                .ThenBy(item => item.Entry.ContractGroup, StringComparer.Ordinal)
                .ToArray();
        }

        return outcome.New.Select(item => new BaselineLifecycleEntry(item, BaselineEntryLifecycle.New))
            .Concat(outcome.Frozen.Select(item => new BaselineLifecycleEntry(item, BaselineEntryLifecycle.Matched)))
            .Concat(outcome.Resolved.Select(item => new BaselineLifecycleEntry(item, BaselineEntryLifecycle.Resolved)))
            .Concat(outcome.Ambiguous.Select(item => new BaselineLifecycleEntry(item, BaselineEntryLifecycle.Ambiguous)))
            .Concat(outcome.ConfigurationErrors.Select(item => new BaselineLifecycleEntry(item, BaselineEntryLifecycle.ConfigurationError)))
            .OrderBy(item => item.Entry.Identity?.ToString(), StringComparer.Ordinal)
            .ThenBy(item => item.Entry.ContractGroup, StringComparer.Ordinal)
            .ToArray();
    }

    private static string? FormatDate(DateOnly? date) =>
        date?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static string CompletionToken(ArchitectureAssessmentCompletionState state) => state switch
    {
        ArchitectureAssessmentCompletionState.Pass => "pass",
        ArchitectureAssessmentCompletionState.Fail => "fail",
        ArchitectureAssessmentCompletionState.Unassessable => "unassessable",
        _ => throw new ArgumentOutOfRangeException(nameof(state), state, null),
    };
}
