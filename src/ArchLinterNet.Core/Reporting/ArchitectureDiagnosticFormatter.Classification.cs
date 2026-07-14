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
        return BuildCiArtifactsJson(new CiArtifactsRequest(
            mode, passed, violations, cycles, null, coverageFindings, unmatched,
            policyConsistencyFindings, coverageSummaries, null, classificationConflicts, classificationMetadataFailures,
            null));
    }

    public string FormatResultForCiArtifacts(
        string mode,
        bool passed,
        IReadOnlyCollection<ArchitectureViolation> violations,
        IReadOnlyCollection<string> cycles,
        IReadOnlyCollection<ArchitectureCycleFinding> cycleFindings,
        IReadOnlyCollection<ArchitectureViolation>? coverageFindings = null,
        IReadOnlyCollection<ArchitectureUnmatchedIgnoredViolation>? unmatched = null,
        IReadOnlyCollection<PolicyConsistencyDiagnostic>? policyConsistencyFindings = null,
        IReadOnlyCollection<ArchitectureCoverageSummary>? coverageSummaries = null,
        IReadOnlyCollection<ArchitectureClassificationConflict>? classificationConflicts = null,
        IReadOnlyCollection<ArchitectureClassificationMetadataFailure>? classificationMetadataFailures = null)
    {
        return BuildCiArtifactsJson(new CiArtifactsRequest(
            mode, passed, violations, cycles, null, coverageFindings, unmatched,
            policyConsistencyFindings, coverageSummaries, cycleFindings, classificationConflicts,
            classificationMetadataFailures, null));
    }

    /// <summary>
    /// Additive overload — see the matching declaration on <see cref="IArchitectureDiagnosticFormatter"/>
    /// for why this exists alongside the original overload instead of extending it.
    /// </summary>
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
        return BuildCiArtifactsJson(new CiArtifactsRequest(
            mode, passed, violations, cycles, classificationRoles, coverageFindings, unmatched,
            policyConsistencyFindings, coverageSummaries, null, classificationConflicts, classificationMetadataFailures,
            null));
    }

    public string FormatResultForCiArtifacts(
        string mode,
        bool passed,
        IReadOnlyCollection<ArchitectureViolation> violations,
        IReadOnlyCollection<string> cycles,
        IReadOnlyCollection<ArchitectureCycleFinding> cycleFindings,
        IReadOnlyCollection<ArchitectureClassificationRoleFact> classificationRoles,
        IReadOnlyCollection<ArchitectureViolation>? coverageFindings = null,
        IReadOnlyCollection<ArchitectureUnmatchedIgnoredViolation>? unmatched = null,
        IReadOnlyCollection<PolicyConsistencyDiagnostic>? policyConsistencyFindings = null,
        IReadOnlyCollection<ArchitectureCoverageSummary>? coverageSummaries = null,
        IReadOnlyCollection<ArchitectureClassificationConflict>? classificationConflicts = null,
        IReadOnlyCollection<ArchitectureClassificationMetadataFailure>? classificationMetadataFailures = null)
    {
        return BuildCiArtifactsJson(new CiArtifactsRequest(
            mode, passed, violations, cycles, classificationRoles, coverageFindings, unmatched,
            policyConsistencyFindings, coverageSummaries, cycleFindings, classificationConflicts,
            classificationMetadataFailures, null));
    }

    /// <summary>
    /// Additive overload — see the matching declaration on <see cref="IArchitectureDiagnosticFormatter"/>
    /// for why this exists alongside the roles overload instead of extending it.
    /// </summary>
    public string FormatResultForCiArtifacts(
        string mode,
        bool passed,
        IReadOnlyCollection<ArchitectureViolation> violations,
        IReadOnlyCollection<string> cycles,
        IReadOnlyCollection<ArchitectureClassificationRoleFact> classificationRoles,
        ArchitectureClassificationPathDeferredNotice? classificationPathDeferred,
        IReadOnlyCollection<ArchitectureViolation>? coverageFindings = null,
        IReadOnlyCollection<ArchitectureUnmatchedIgnoredViolation>? unmatched = null,
        IReadOnlyCollection<PolicyConsistencyDiagnostic>? policyConsistencyFindings = null,
        IReadOnlyCollection<ArchitectureCoverageSummary>? coverageSummaries = null,
        IReadOnlyCollection<ArchitectureClassificationConflict>? classificationConflicts = null,
        IReadOnlyCollection<ArchitectureClassificationMetadataFailure>? classificationMetadataFailures = null)
    {
        return BuildCiArtifactsJson(new CiArtifactsRequest(
            mode, passed, violations, cycles, classificationRoles, coverageFindings, unmatched,
            policyConsistencyFindings, coverageSummaries, null, classificationConflicts, classificationMetadataFailures,
            classificationPathDeferred));
    }

    public string FormatResultForCiArtifacts(
        string mode,
        bool passed,
        IReadOnlyCollection<ArchitectureViolation> violations,
        IReadOnlyCollection<string> cycles,
        IReadOnlyCollection<ArchitectureCycleFinding> cycleFindings,
        IReadOnlyCollection<ArchitectureClassificationRoleFact> classificationRoles,
        ArchitectureClassificationPathDeferredNotice? classificationPathDeferred,
        IReadOnlyCollection<ArchitectureViolation>? coverageFindings = null,
        IReadOnlyCollection<ArchitectureUnmatchedIgnoredViolation>? unmatched = null,
        IReadOnlyCollection<PolicyConsistencyDiagnostic>? policyConsistencyFindings = null,
        IReadOnlyCollection<ArchitectureCoverageSummary>? coverageSummaries = null,
        IReadOnlyCollection<ArchitectureClassificationConflict>? classificationConflicts = null,
        IReadOnlyCollection<ArchitectureClassificationMetadataFailure>? classificationMetadataFailures = null)
    {
        return BuildCiArtifactsJson(new CiArtifactsRequest(
            mode, passed, violations, cycles, classificationRoles, coverageFindings, unmatched,
            policyConsistencyFindings, coverageSummaries, cycleFindings, classificationConflicts,
            classificationMetadataFailures, classificationPathDeferred));
    }

    // Bundles FormatResultForCiArtifacts's parameters into one value so the private builder below
    // takes a single argument instead of eleven — the public overloads above still expose each
    // section as its own named parameter for callers.
    private readonly record struct CiArtifactsRequest(
        string Mode,
        bool Passed,
        IReadOnlyCollection<ArchitectureViolation> Violations,
        IReadOnlyCollection<string> Cycles,
        IReadOnlyCollection<ArchitectureClassificationRoleFact>? ClassificationRoles,
        IReadOnlyCollection<ArchitectureViolation>? CoverageFindings,
        IReadOnlyCollection<ArchitectureUnmatchedIgnoredViolation>? Unmatched,
        IReadOnlyCollection<PolicyConsistencyDiagnostic>? PolicyConsistencyFindings,
        IReadOnlyCollection<ArchitectureCoverageSummary>? CoverageSummaries,
        IReadOnlyCollection<ArchitectureCycleFinding>? CycleFindings,
        IReadOnlyCollection<ArchitectureClassificationConflict>? ClassificationConflicts,
        IReadOnlyCollection<ArchitectureClassificationMetadataFailure>? ClassificationMetadataFailures,
        ArchitectureClassificationPathDeferredNotice? ClassificationPathDeferred);

    private static string BuildCiArtifactsJson(CiArtifactsRequest request)
    {
        var unmatchedSerialized = (request.Unmatched ?? Array.Empty<ArchitectureUnmatchedIgnoredViolation>())
            .Select(ArchitectureDiagnosticMapper.FromUnmatchedIgnore)
            .Select(ToUnmatchedJsonObject)
            .ToArray();

        var policyConsistencySerialized = (request.PolicyConsistencyFindings ?? Array.Empty<PolicyConsistencyDiagnostic>())
            .Select(ToPolicyConsistencyJsonObject)
            .ToArray();

        var classificationConflictsSerialized = BuildClassificationConflictsJson(request.ClassificationConflicts);
        var classificationMetadataFailuresSerialized = BuildClassificationMetadataFailuresJson(request.ClassificationMetadataFailures);
        var classificationRolesSerialized = BuildClassificationRolesJson(request.ClassificationRoles);
        var cycleDiagnosticsSerialized = (request.CycleFindings ?? Array.Empty<ArchitectureCycleFinding>())
            .Select(ToCycleJsonObject)
            .ToArray();

        var payload = new
        {
            passed = request.Passed,
            mode = request.Mode,
            violations = request.Violations
                .Select(ArchitectureDiagnosticMapper.FromViolation)
                .Select(d => ToCiJsonObject(d, includeContract: true))
                .ToArray(),
            cycles = request.Cycles
                .Select(cycle => ArchitectureDiagnosticMapper.FromCycle(cycle, contractName: string.Empty, contractId: null))
                .Select(d => d.Path)
                .ToArray(),
            cycle_diagnostics = cycleDiagnosticsSerialized,
            coverage_findings = (request.CoverageFindings ?? Array.Empty<ArchitectureViolation>())
                .Select(ArchitectureDiagnosticMapper.FromViolation)
                .Select(d => ToCiJsonObject(d, includeContract: true))
                .ToArray(),
            unmatched_ignored_violations = unmatchedSerialized,
            policy_consistency_findings = policyConsistencySerialized,
            coverage_summary = (request.CoverageSummaries ?? Array.Empty<ArchitectureCoverageSummary>())
                .OrderBy(s => s.ContractId ?? s.ContractName, StringComparer.Ordinal)
                .Select(ToCoverageSummaryJsonObject)
                .ToArray(),
            classification_conflicts = classificationConflictsSerialized,
            classification_metadata_failures = classificationMetadataFailuresSerialized,
            classification_roles = classificationRolesSerialized,
            classification_path_deferred = request.ClassificationPathDeferred == null
                ? null
                : new
                {
                    declared_entry_count = request.ClassificationPathDeferred.DeclaredEntryCount,
                    policy_locations = request.ClassificationPathDeferred.PolicyLocations
                        .Select(FormatPolicyLocationForJson)
                        .ToArray()
                }
        };

        return JsonSerializer.Serialize(payload);
    }

    public string FormatClassificationFactsForHumans(
        IReadOnlyCollection<ArchitectureClassificationConflict> conflicts,
        IReadOnlyCollection<ArchitectureClassificationMetadataFailure> metadataFailures)
    {
        return FormatClassificationFactsForHumans(conflicts, metadataFailures, null);
    }

    /// <summary>
    /// Additive overload — see the matching declaration on <see cref="IArchitectureDiagnosticFormatter"/>
    /// for why this exists alongside the original overload instead of extending it.
    /// </summary>
    public string FormatClassificationFactsForHumans(
        IReadOnlyCollection<ArchitectureClassificationConflict> conflicts,
        IReadOnlyCollection<ArchitectureClassificationMetadataFailure> metadataFailures,
        ArchitectureClassificationPathDeferredNotice? classificationPathDeferred)
    {
        if (conflicts.Count == 0 && metadataFailures.Count == 0 && classificationPathDeferred == null)
        {
            return string.Empty;
        }

        var conflictLines = conflicts
            .OrderBy(c => c.Subject, StringComparer.Ordinal)
            .ThenBy(c => c.Source)
            .ThenBy(c => c.MetadataDetail, StringComparer.Ordinal)
            .Select(c => $"  conflict: [{c.Source}] {c.Subject}: kept '{c.WinningRole}', discarded '{c.DiscardedRole}'"
                + (c.MetadataDetail != null ? $" ({c.MetadataDetail})" : string.Empty)
                + FormatClassificationPolicyLocationSuffix(c.PolicyLocation, c.RelatedPolicyLocations));

        var failureLines = metadataFailures
            .OrderBy(f => f.Subject, StringComparer.Ordinal)
            .ThenBy(f => f.MetadataKey, StringComparer.Ordinal)
            .Select(f => $"  metadata_failure: [{f.Source}] {f.Subject}.{f.MetadataKey}: {f.Reason}"
                + FormatClassificationPolicyLocationSuffix(f.PolicyLocation));

        IEnumerable<string> pathDeferredLines = Enumerable.Empty<string>();
        if (classificationPathDeferred != null)
        {
            string entryNoun = classificationPathDeferred.DeclaredEntryCount == 1 ? "entry" : "entries";
            pathDeferredLines = new[]
            {
                $"  path_deferred: classification.path declares {classificationPathDeferred.DeclaredEntryCount} {entryNoun}, "
                    + "but path-convention classification is not yet implemented — it depends on source/declared-type "
                    + "fact discovery (see issue #171, currently open). This section is schema-accepted but produces "
                    + "no role assignment."
                    + (classificationPathDeferred.PolicyLocations.Count == 0
                        ? string.Empty
                        : " (policy: " + string.Join(", ", classificationPathDeferred.PolicyLocations
                            .Select(location => $"{location.SourcePath}:{location.YamlPath}")) + ")")
            };
        }

        return "Classification findings:" + Environment.NewLine
            + string.Join(Environment.NewLine, conflictLines.Concat(failureLines).Concat(pathDeferredLines));
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
                metadata_detail = c.MetadataDetail,
                policy_location = c.PolicyLocation is null ? null : FormatPolicyLocationForJson(c.PolicyLocation),
                related_policy_locations = c.RelatedPolicyLocations
                    .OrderBy(location => location.SourceOrdinal)
                    .ThenBy(location => location.EncounterOrdinal)
                    .Select(FormatPolicyLocationForJson)
                    .ToArray()
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
                reason = f.Reason,
                policy_location = f.PolicyLocation is null ? null : FormatPolicyLocationForJson(f.PolicyLocation)
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

    private static string FormatClassificationPolicyLocationSuffix(
        ArchitecturePolicySourceLocation? primaryLocation,
        IReadOnlyCollection<ArchitecturePolicySourceLocation>? relatedLocations = null)
    {
        if (primaryLocation is null)
        {
            return string.Empty;
        }

        string related = relatedLocations is not { Count: > 0 }
            ? string.Empty
            : "; related: " + string.Join(", ", relatedLocations
                .OrderBy(location => location.SourceOrdinal)
                .ThenBy(location => location.EncounterOrdinal)
                .Select(location => $"{location.SourcePath}:{location.YamlPath}"));
        return $" (policy: {primaryLocation.SourcePath}:{primaryLocation.YamlPath}; root: {primaryLocation.RootPath}{related})";
    }
}
