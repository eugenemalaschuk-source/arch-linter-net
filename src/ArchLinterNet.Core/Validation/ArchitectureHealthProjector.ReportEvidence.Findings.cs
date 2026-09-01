using System.Text.Json;
using System.Text.Json.Nodes;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.PolicyContext;
using ArchLinterNet.Core.PolicyWeakening;
using ArchLinterNet.Core.Reporting;

namespace ArchLinterNet.Core.Validation;

internal static class ArchitectureHealthReportFindingEvidenceWriter
{
    internal static bool HasExternalEvidence(ValidationOutcome outcome) =>
        outcome.ExternalEvidenceRequirements.Count > 0
        || outcome.ImportedDiagnosticFindings.Count > 0
        || outcome.ApplicabilityRecords.Any(record =>
            string.Equals(record.Family, "external_diagnostics", StringComparison.Ordinal));

    internal static JsonObject BuildExternalEvidence(ValidationOutcome outcome, string mode)
    {
        var requirements = new JsonArray();
        foreach (ArchitectureExternalEvidenceRequirement requirement in outcome.ExternalEvidenceRequirements
            .OrderBy(item => item.Id, StringComparer.Ordinal))
        {
            JsonObject item = new()
            {
                ["id"] = requirement.Id,
                ["format"] = requirement.Format,
                ["required"] = requirement.Required,
                ["tool"] = requirement.Tool,
                ["tool_version"] = requirement.ToolVersion,
                ["run"] = requirement.Run,
                ["require_repository"] = requirement.RequireRepository,
                ["require_revision"] = requirement.RequireRevision,
                ["require_scope"] = requirement.RequireScope,
            };
            if (requirement.DiagnosticFilter is not null)
            {
                ArchitectureExternalEvidenceDiagnosticFilter filter = requirement.DiagnosticFilter;
                item["diagnostic_filter"] = new JsonObject
                {
                    ["rule_ids"] = ToStringArray(filter.RuleIds),
                    ["rule_tags"] = ToStringArray(filter.RuleTags),
                    ["projects"] = ToStringArray(filter.Projects),
                    ["path_prefixes"] = ToStringArray(filter.PathPrefixes),
                    ["severity"] = BuildStringMap(filter.Severity),
                    ["require_matches"] = filter.RequireMatches,
                };
            }

            requirements.Add(item);
        }

        return new JsonObject
        {
            ["mode"] = mode,
            ["requirements"] = requirements,
            ["findings"] = BuildFindings(outcome.ImportedDiagnosticFindings, mode),
        };
    }

    internal static JsonArray BuildFindings(ValidationOutcome outcome, string mode)
    {
        var findings = new List<ArchitectureFinding>();
        findings.AddRange(ArchitectureFindingMapper.FromViolations(outcome.Violations, mode));
        findings.AddRange(ArchitectureFindingMapper.FromViolations(outcome.CoverageFindings, mode));
        findings.AddRange(outcome.CycleFindings.Select(cycle =>
            ArchitectureFindingMapper.FromDiagnostic(ArchitectureDiagnosticMapper.FromCycle(cycle), mode)));
        if (outcome.CycleFindings.Count == 0)
        {
            findings.AddRange(outcome.Cycles.Select(cycle => ArchitectureFindingMapper.FromDiagnostic(
                ArchitectureDiagnosticMapper.FromCycle(cycle, string.Empty, null), mode)));
        }

        findings.AddRange(outcome.UnmatchedIgnoredViolations
            .Select(item => ArchitectureFindingMapper.FromDiagnostic(
                ArchitectureDiagnosticMapper.FromUnmatchedIgnore(item), mode)));
        findings.AddRange(outcome.PolicyConsistencyFindings
            .Select(item => ArchitectureFindingMapper.FromDiagnostic(item, mode)));
        findings.AddRange(outcome.ApplicabilityFindings);
        findings.AddRange(outcome.ImportedDiagnosticFindings);
        findings.AddRange(outcome.PreflightDiagnostics
            .Select(item => ArchitectureFindingMapper.FromDiagnostic(item, mode)));
        return BuildFindings(ArchitectureFindingMapper.Order(findings), mode);
    }

    internal static JsonArray BuildFindings(IEnumerable<ArchitectureFinding> findings, string? mode)
    {
        var result = new JsonArray();
        foreach (ArchitectureFinding finding in findings
            .OrderBy(item => item.ContractId ?? item.ContractName, StringComparer.Ordinal)
            .ThenBy(item => item.CanonicalIdentity, StringComparer.Ordinal)
            .ThenBy(item => item.Kind, StringComparer.Ordinal))
        {
            // Mode is part of the canonical finding receipt. Only fill it for legacy findings that
            // did not carry one; this does not alter identity or severity.
            ArchitectureFinding normalized = finding.Mode is null ? finding with { Mode = mode } : finding;
            result.Add(JsonSerializer.SerializeToNode(
                ArchitectureDiagnosticFormatter.FormatNormalizedFindingForJson(normalized)));
        }

        return result;
    }

    internal static JsonObject BuildProvenance(ValidationOutcome outcome) =>
        new()
        {
            ["repository_root"] = outcome.RepositoryRoot,
            ["policy_import_paths"] = ToStringArray(outcome.PolicyImportPaths),
            ["resolved_assembly_paths"] = ToStringArray(outcome.ResolvedAssemblyPaths),
            ["discovered_project_paths"] = ToStringArray(outcome.DiscoveredProjectPaths),
        };

    internal static JsonObject BuildStringMap(IReadOnlyDictionary<string, string> values)
    {
        var result = new JsonObject();
        foreach ((string key, string value) in values.OrderBy(item => item.Key, StringComparer.Ordinal))
        {
            result[key] = value;
        }

        return result;
    }

    internal static JsonArray ToStringArray(IEnumerable<string> values)
    {
        var result = new JsonArray();
        foreach (string value in values.OrderBy(item => item, StringComparer.Ordinal))
        {
            result.Add(value);
        }

        return result;
    }
}
