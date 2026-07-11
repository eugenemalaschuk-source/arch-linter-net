using System.Text.Json;
using ArchLinterNet.Core.Model;

namespace ArchLinterNet.Core.Reporting;

public sealed partial class ArchitectureDiagnosticFormatter
{
    public string FormatResultForCiArtifacts(
        string mode,
        bool passed,
        IReadOnlyCollection<ArchitectureViolation> violations,
        IReadOnlyCollection<string> cycles,
        IReadOnlyCollection<ArchitectureViolation>? coverageFindings = null,
        IReadOnlyCollection<ArchitectureUnmatchedIgnoredViolation>? unmatched = null,
        IReadOnlyCollection<PolicyConsistencyDiagnostic>? policyConsistencyFindings = null,
        IReadOnlyCollection<ArchitectureCoverageSummary>? coverageSummaries = null,
        IReadOnlyCollection<ArchitectureClassificationConflict>? classificationConflicts = null,
        IReadOnlyCollection<ArchitectureClassificationMetadataFailure>? classificationMetadataFailures = null)
    {
        return BuildCiArtifactsJson(
            mode, passed, violations, cycles, null, coverageFindings, unmatched,
            policyConsistencyFindings, coverageSummaries, classificationConflicts, classificationMetadataFailures);
    }

    // Additive overload (not a modification of the one above) so binaries already compiled against
    // the original 10-parameter FormatResultForCiArtifacts keep resolving to it unchanged;
    // classificationRoles is required here (no default) so it stays unambiguous against that
    // overload for every call site, named or positional.
    public string FormatResultForCiArtifacts(
        string mode,
        bool passed,
        IReadOnlyCollection<ArchitectureViolation> violations,
        IReadOnlyCollection<string> cycles,
        IReadOnlyCollection<ArchitectureClassificationRoleFact> classificationRoles,
        IReadOnlyCollection<ArchitectureViolation>? coverageFindings = null,
        IReadOnlyCollection<ArchitectureUnmatchedIgnoredViolation>? unmatched = null,
        IReadOnlyCollection<PolicyConsistencyDiagnostic>? policyConsistencyFindings = null,
        IReadOnlyCollection<ArchitectureCoverageSummary>? coverageSummaries = null,
        IReadOnlyCollection<ArchitectureClassificationConflict>? classificationConflicts = null,
        IReadOnlyCollection<ArchitectureClassificationMetadataFailure>? classificationMetadataFailures = null)
    {
        return BuildCiArtifactsJson(
            mode, passed, violations, cycles, classificationRoles, coverageFindings, unmatched,
            policyConsistencyFindings, coverageSummaries, classificationConflicts, classificationMetadataFailures);
    }

    private static string BuildCiArtifactsJson(
        string mode,
        bool passed,
        IReadOnlyCollection<ArchitectureViolation> violations,
        IReadOnlyCollection<string> cycles,
        IReadOnlyCollection<ArchitectureClassificationRoleFact>? classificationRoles,
        IReadOnlyCollection<ArchitectureViolation>? coverageFindings,
        IReadOnlyCollection<ArchitectureUnmatchedIgnoredViolation>? unmatched,
        IReadOnlyCollection<PolicyConsistencyDiagnostic>? policyConsistencyFindings,
        IReadOnlyCollection<ArchitectureCoverageSummary>? coverageSummaries,
        IReadOnlyCollection<ArchitectureClassificationConflict>? classificationConflicts,
        IReadOnlyCollection<ArchitectureClassificationMetadataFailure>? classificationMetadataFailures)
    {
        var unmatchedSerialized = (unmatched ?? Array.Empty<ArchitectureUnmatchedIgnoredViolation>())
            .Select(ArchitectureDiagnosticMapper.FromUnmatchedIgnore)
            .Select(u => new
            {
                contract = u.ContractName,
                contract_id = u.ContractId,
                ignore_index = u.IgnoreIndex,
                source_type = u.SourceType,
                forbidden_reference = u.ForbiddenReference,
                reason = u.Reason
            })
            .ToArray();

        var policyConsistencySerialized = (policyConsistencyFindings ?? Array.Empty<PolicyConsistencyDiagnostic>())
            .Select(ToPolicyConsistencyJsonObject)
            .ToArray();

        var classificationConflictsSerialized = BuildClassificationConflictsJson(classificationConflicts);
        var classificationMetadataFailuresSerialized = BuildClassificationMetadataFailuresJson(classificationMetadataFailures);
        var classificationRolesSerialized = BuildClassificationRolesJson(classificationRoles);

        var payload = new
        {
            passed,
            mode,
            violations = violations
                .Select(ArchitectureDiagnosticMapper.FromViolation)
                .Select(d => ToCiJsonObject(d, includeContract: true))
                .ToArray(),
            cycles = cycles
                .Select(cycle => ArchitectureDiagnosticMapper.FromCycle(cycle, contractName: string.Empty, contractId: null))
                .Select(d => d.Path)
                .ToArray(),
            coverage_findings = (coverageFindings ?? Array.Empty<ArchitectureViolation>())
                .Select(ArchitectureDiagnosticMapper.FromViolation)
                .Select(d => ToCiJsonObject(d, includeContract: true))
                .ToArray(),
            unmatched_ignored_violations = unmatchedSerialized,
            policy_consistency_findings = policyConsistencySerialized,
            coverage_summary = (coverageSummaries ?? Array.Empty<ArchitectureCoverageSummary>())
                .OrderBy(s => s.ContractId ?? s.ContractName, StringComparer.Ordinal)
                .Select(ToCoverageSummaryJsonObject)
                .ToArray(),
            classification_conflicts = classificationConflictsSerialized,
            classification_metadata_failures = classificationMetadataFailuresSerialized,
            classification_roles = classificationRolesSerialized
        };

        return JsonSerializer.Serialize(payload);
    }

    public string FormatClassificationFactsForHumans(
        IReadOnlyCollection<ArchitectureClassificationConflict> conflicts,
        IReadOnlyCollection<ArchitectureClassificationMetadataFailure> metadataFailures)
    {
        if (conflicts.Count == 0 && metadataFailures.Count == 0)
        {
            return string.Empty;
        }

        var conflictLines = conflicts
            .OrderBy(c => c.Subject, StringComparer.Ordinal)
            .ThenBy(c => c.Source)
            .ThenBy(c => c.MetadataDetail, StringComparer.Ordinal)
            .Select(c => $"  conflict: [{c.Source}] {c.Subject}: kept '{c.WinningRole}', discarded '{c.DiscardedRole}'"
                + (c.MetadataDetail != null ? $" ({c.MetadataDetail})" : string.Empty));

        var failureLines = metadataFailures
            .OrderBy(f => f.Subject, StringComparer.Ordinal)
            .ThenBy(f => f.MetadataKey, StringComparer.Ordinal)
            .Select(f => $"  metadata_failure: [{f.Source}] {f.Subject}.{f.MetadataKey}: {f.Reason}");

        return "Classification findings:" + Environment.NewLine
            + string.Join(Environment.NewLine, conflictLines.Concat(failureLines));
    }

    private static object[] BuildClassificationConflictsJson(
        IReadOnlyCollection<ArchitectureClassificationConflict>? classificationConflicts)
    {
        return (classificationConflicts ?? Array.Empty<ArchitectureClassificationConflict>())
            .OrderBy(c => c.Subject, StringComparer.Ordinal)
            .ThenBy(c => c.MetadataDetail, StringComparer.Ordinal)
            .Select(c => (object)new
            {
                subject = c.Subject,
                source = c.Source.ToString(),
                winning_role = c.WinningRole,
                discarded_role = c.DiscardedRole,
                metadata_detail = c.MetadataDetail
            })
            .ToArray();
    }

    private static object[] BuildClassificationMetadataFailuresJson(
        IReadOnlyCollection<ArchitectureClassificationMetadataFailure>? classificationMetadataFailures)
    {
        return (classificationMetadataFailures ?? Array.Empty<ArchitectureClassificationMetadataFailure>())
            .OrderBy(f => f.Subject, StringComparer.Ordinal)
            .ThenBy(f => f.MetadataKey, StringComparer.Ordinal)
            .Select(f => (object)new
            {
                subject = f.Subject,
                source = f.Source.ToString(),
                metadata_key = f.MetadataKey,
                reason = f.Reason
            })
            .ToArray();
    }

    private static object[] BuildClassificationRolesJson(
        IReadOnlyCollection<ArchitectureClassificationRoleFact>? classificationRoles)
    {
        return (classificationRoles ?? Array.Empty<ArchitectureClassificationRoleFact>())
            .OrderBy(r => r.Subject, StringComparer.Ordinal)
            .Select(r => (object)new
            {
                subject = r.Subject,
                role = r.Role,
                source = r.Source.ToString(),
                evidence = r.Evidence,
                metadata = r.Metadata
            })
            .ToArray();
    }
}
