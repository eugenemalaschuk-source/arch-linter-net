using ArchLinterNet.Core.Model;

namespace ArchLinterNet.Core.Contracts;

public static class ArchitectureBaselineComparer
{
    public static ArchitectureBaselineComparisonResult Compare(
        ArchitectureContractDocument policyDocument,
        ArchitectureBaselineDocument baselineDocument,
        IReadOnlyList<ArchitectureBaselineCandidate> candidates,
        string mode,
        IReadOnlyCollection<string>? selectedContractIds = null)
    {
        var newEntries = new List<ArchitectureBaselineComparisonEntry>();
        var frozen = new List<ArchitectureBaselineComparisonEntry>();
        var resolved = new List<ArchitectureBaselineComparisonEntry>();
        var configurationErrors = new List<ArchitectureBaselineComparisonEntry>();

        HashSet<string>? selectedIds = selectedContractIds is { Count: > 0 }
            ? new HashSet<string>(selectedContractIds, StringComparer.OrdinalIgnoreCase)
            : null;

        foreach (var groupName in _allGroupNames)
        {
            if (!IsInScope(groupName, mode))
            {
                continue;
            }

            List<ArchitectureBaselineContractEntry> entries = GetEntries(baselineDocument.Baseline, groupName);
            HashSet<string> knownIds = GetKnownContractIds(policyDocument.Contracts, groupName);
            var baselineKeys = new HashSet<string>(StringComparer.Ordinal);

            foreach (var entry in entries)
            {
                if (selectedIds != null && !selectedIds.Contains(entry.Id))
                {
                    continue;
                }

                bool idKnown = knownIds.Contains(entry.Id);

                foreach (var ignore in entry.IgnoredViolations)
                {
                    string key = $"{entry.Id}|{ignore.SourceType}|{ignore.ForbiddenReference}";
                    baselineKeys.Add(key);

                    var comparisonEntry = new ArchitectureBaselineComparisonEntry(
                        groupName, entry.Id, ignore.SourceType, ignore.ForbiddenReference, ignore.Reason);

                    if (!idKnown)
                    {
                        configurationErrors.Add(comparisonEntry);
                    }
                    else if (HasMatchingCandidate(candidates, groupName, entry.Id, ignore.SourceType, ignore.ForbiddenReference))
                    {
                        frozen.Add(comparisonEntry);
                    }
                    else
                    {
                        resolved.Add(comparisonEntry);
                    }
                }
            }

            var seenNewKeys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var candidate in candidates)
            {
                if (candidate.ContractGroup != groupName || candidate.ContractId == null)
                {
                    continue;
                }

                if (selectedIds != null && !selectedIds.Contains(candidate.ContractId))
                {
                    continue;
                }

                string key = $"{candidate.ContractId}|{candidate.SourceType}|{candidate.ForbiddenReference}";
                if (baselineKeys.Contains(key) || !seenNewKeys.Add(key))
                {
                    continue;
                }

                newEntries.Add(new ArchitectureBaselineComparisonEntry(
                    groupName, candidate.ContractId, candidate.SourceType, candidate.ForbiddenReference, null));
            }
        }

        return new ArchitectureBaselineComparisonResult(newEntries, frozen, resolved, configurationErrors);
    }

    private static bool HasMatchingCandidate(
        IReadOnlyList<ArchitectureBaselineCandidate> candidates,
        string groupName,
        string contractId,
        string sourceType,
        string forbiddenReference)
    {
        foreach (var candidate in candidates)
        {
            if (candidate.ContractGroup == groupName
                && candidate.ContractId == contractId
                && candidate.SourceType == sourceType
                && candidate.ForbiddenReference == forbiddenReference)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsInScope(string groupName, string mode)
    {
        if (mode == "all")
        {
            return true;
        }

        return groupName == mode || groupName.StartsWith(mode + "_", StringComparison.Ordinal);
    }

    private static readonly string[] _allGroupNames =
    {
        "strict", "audit",
        "strict_layers", "audit_layers",
        "strict_allow_only", "audit_allow_only",
        "strict_cycles", "audit_cycles",
        "strict_acyclic_siblings", "audit_acyclic_siblings",
        "strict_method_body", "audit_method_body",
        "strict_independence", "audit_independence",
        "strict_protected", "audit_protected",
        "strict_external", "audit_external",
        "strict_project_metadata", "audit_project_metadata",
        "strict_coverage", "audit_coverage",
    };

    private static List<ArchitectureBaselineContractEntry> GetEntries(ArchitectureBaselineContractGroups groups, string groupName)
    {
        return groupName switch
        {
            "strict" => groups.Strict,
            "audit" => groups.Audit,
            "strict_layers" => groups.StrictLayers,
            "audit_layers" => groups.AuditLayers,
            "strict_allow_only" => groups.StrictAllowOnly,
            "audit_allow_only" => groups.AuditAllowOnly,
            "strict_cycles" => groups.StrictCycles,
            "audit_cycles" => groups.AuditCycles,
            "strict_acyclic_siblings" => groups.StrictAcyclicSiblings,
            "audit_acyclic_siblings" => groups.AuditAcyclicSiblings,
            "strict_method_body" => groups.StrictMethodBody,
            "audit_method_body" => groups.AuditMethodBody,
            "strict_independence" => groups.StrictIndependence,
            "audit_independence" => groups.AuditIndependence,
            "strict_protected" => groups.StrictProtected,
            "audit_protected" => groups.AuditProtected,
            "strict_external" => groups.StrictExternal,
            "audit_external" => groups.AuditExternal,
            "strict_project_metadata" => groups.StrictProjectMetadata,
            "audit_project_metadata" => groups.AuditProjectMetadata,
            "strict_coverage" => groups.StrictCoverage,
            "audit_coverage" => groups.AuditCoverage,
            _ => new List<ArchitectureBaselineContractEntry>(),
        };
    }

    private static HashSet<string> GetKnownContractIds(ArchitectureContractGroups groups, string groupName)
    {
        IEnumerable<string?> ids = groupName switch
        {
            "strict" => groups.Strict.Select(c => c.Id),
            "audit" => groups.Audit.Select(c => c.Id),
            "strict_layers" => groups.StrictLayers.Select(c => c.Id),
            "audit_layers" => groups.AuditLayers.Select(c => c.Id),
            "strict_allow_only" => groups.StrictAllowOnly.Select(c => c.Id),
            "audit_allow_only" => groups.AuditAllowOnly.Select(c => c.Id),
            "strict_cycles" => groups.StrictCycles.Select(c => c.Id),
            "audit_cycles" => groups.AuditCycles.Select(c => c.Id),
            "strict_acyclic_siblings" => groups.StrictAcyclicSiblings.Select(c => c.Id),
            "audit_acyclic_siblings" => groups.AuditAcyclicSiblings.Select(c => c.Id),
            "strict_method_body" => groups.StrictMethodBody.Select(c => c.Id),
            "audit_method_body" => groups.AuditMethodBody.Select(c => c.Id),
            "strict_independence" => groups.StrictIndependence.Select(c => c.Id),
            "audit_independence" => groups.AuditIndependence.Select(c => c.Id),
            "strict_protected" => groups.StrictProtected.Select(c => c.Id),
            "audit_protected" => groups.AuditProtected.Select(c => c.Id),
            "strict_external" => groups.StrictExternal.Select(c => c.Id),
            "audit_external" => groups.AuditExternal.Select(c => c.Id),
            "strict_project_metadata" => groups.StrictProjectMetadata.Select(c => c.Id),
            "audit_project_metadata" => groups.AuditProjectMetadata.Select(c => c.Id),
            "strict_coverage" => groups.StrictCoverage.Select(c => c.Id),
            "audit_coverage" => groups.AuditCoverage.Select(c => c.Id),
            _ => Enumerable.Empty<string?>(),
        };

        return new HashSet<string>(ids.Where(id => id != null)!, StringComparer.OrdinalIgnoreCase);
    }
}
