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
        bool useStructuredIdentity = baselineDocument.Version == 2;

        var newEntries = new List<ArchitectureBaselineComparisonEntry>();
        var frozen = new List<ArchitectureBaselineComparisonEntry>();
        var resolved = new List<ArchitectureBaselineComparisonEntry>();
        var configurationErrors = new List<ArchitectureBaselineComparisonEntry>();
        var outOfScope = new List<ArchitectureBaselineComparisonEntry>();

        HashSet<string>? selectedIds = selectedContractIds is { Count: > 0 }
            ? new HashSet<string>(selectedContractIds, StringComparer.OrdinalIgnoreCase)
            : null;

        Dictionary<string, Dictionary<string, string>> canonicalIdsByGroup =
            BuildCanonicalIdsByGroup(policyDocument.Contracts);

        foreach (var groupName in ArchitectureBaselineContractGroups.GroupNames)
        {
            List<ArchitectureBaselineContractEntry> entries = baselineDocument.Baseline.GetGroup(groupName);
            bool groupInScope = IsInScope(groupName, mode);
            HashSet<string> knownIds = groupInScope
                ? GetKnownContractIds(policyDocument.Contracts, groupName)
                : new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, string> canonicalIds = canonicalIdsByGroup.TryGetValue(groupName, out Dictionary<string, string>? ids)
                ? ids
                : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            HashSet<string> baselineKeys = ProcessBaselineEntries(
                groupName, entries, groupInScope, selectedIds, knownIds, canonicalIds, candidates, useStructuredIdentity,
                outOfScope, configurationErrors, frozen, resolved);

            if (!groupInScope)
            {
                continue;
            }

            ProcessNewCandidates(groupName, candidates, selectedIds, canonicalIds, baselineKeys, useStructuredIdentity, newEntries);
        }

        return new ArchitectureBaselineComparisonResult(newEntries, frozen, resolved, configurationErrors, outOfScope);
    }

    private static HashSet<string> ProcessBaselineEntries( // NOSONAR: arguments are separate comparison inputs and outputs.
        string groupName,
        List<ArchitectureBaselineContractEntry> entries,
        bool groupInScope,
        HashSet<string>? selectedIds,
        HashSet<string> knownIds,
        Dictionary<string, string> canonicalIds,
        IReadOnlyList<ArchitectureBaselineCandidate> candidates,
        bool useStructuredIdentity,
        List<ArchitectureBaselineComparisonEntry> outOfScope,
        List<ArchitectureBaselineComparisonEntry> configurationErrors,
        List<ArchitectureBaselineComparisonEntry> frozen,
        List<ArchitectureBaselineComparisonEntry> resolved)
    {
        var baselineKeys = new HashSet<string>(StringComparer.Ordinal);

        foreach (var entry in entries)
        {
            bool entryInScope = groupInScope && (selectedIds == null || selectedIds.Contains(entry.Id));

            // Out-of-scope entries (wrong mode, or not among the selected --contract ids) are
            // carried through verbatim so scoped update/prune never drops unrelated debt.
            if (!entryInScope)
            {
                foreach (var ignore in entry.IgnoredViolations)
                {
                    outOfScope.Add(BuildComparisonEntry(groupName, entry.Id, ignore, useStructuredIdentity));
                }

                continue;
            }

            bool idKnown = knownIds.Contains(entry.Id);
            string canonicalContractId = CanonicalizeContractId(canonicalIds, entry.Id);

            foreach (var ignore in entry.IgnoredViolations)
            {
                ArchitectureBaselineComparisonEntry comparisonEntry =
                    BuildComparisonEntry(groupName, entry.Id, ignore, useStructuredIdentity);

                string key = useStructuredIdentity
                    ? BuildIdentityKey(ignore.ToIdentity(canonicalContractId))
                    : BuildLegacyKey(canonicalContractId, ignore.SourceType, ignore.ForbiddenReference);
                baselineKeys.Add(key);

                if (!idKnown)
                {
                    configurationErrors.Add(comparisonEntry);
                }
                else if (useStructuredIdentity
                             ? HasMatchingCandidateByIdentity(candidates, groupName, ignore.ToIdentity(canonicalContractId))
                             : HasMatchingCandidateLegacy(candidates, groupName, canonicalContractId, ignore.SourceType, ignore.ForbiddenReference))
                {
                    frozen.Add(comparisonEntry);
                }
                else
                {
                    resolved.Add(comparisonEntry);
                }
            }
        }

        return baselineKeys;
    }

    private static ArchitectureBaselineComparisonEntry BuildComparisonEntry(
        string groupName, string contractId, ArchitectureBaselineIgnoredViolation ignore, bool useStructuredIdentity)
    {
        return new ArchitectureBaselineComparisonEntry(
            groupName, contractId, ignore.SourceType, ignore.ForbiddenReference, ignore.Reason,
            useStructuredIdentity ? ignore.ToIdentity(contractId) : null);
    }

    private static void ProcessNewCandidates(
        string groupName,
        IReadOnlyList<ArchitectureBaselineCandidate> candidates,
        HashSet<string>? selectedIds,
        Dictionary<string, string> canonicalIds,
        HashSet<string> baselineKeys,
        bool useStructuredIdentity,
        List<ArchitectureBaselineComparisonEntry> newEntries)
    {
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

            string canonicalContractId = CanonicalizeContractId(canonicalIds, candidate.ContractId);
            ArchitectureViolationIdentity? candidateIdentity = useStructuredIdentity
                ? (candidate.Identity ?? BuildFallbackIdentity(groupName, canonicalContractId, candidate)) with { ContractId = canonicalContractId }
                : null;

            string key = useStructuredIdentity
                ? BuildIdentityKey(candidateIdentity!)
                : BuildLegacyKey(canonicalContractId, candidate.SourceType, candidate.ForbiddenReference);

            if (baselineKeys.Contains(key) || !seenNewKeys.Add(key))
            {
                continue;
            }

            newEntries.Add(new ArchitectureBaselineComparisonEntry(
                groupName, candidate.ContractId, candidate.SourceType, candidate.ForbiddenReference, null,
                useStructuredIdentity ? candidateIdentity : null));
        }
    }

    private static ArchitectureViolationIdentity BuildFallbackIdentity(
        string groupName, string contractId, ArchitectureBaselineCandidate candidate)
    {
        string contractFamily = ArchitectureViolationIdentity.ResolveContractFamily(groupName);
        return new ArchitectureViolationIdentity(
            ArchitectureViolationIdentity.CurrentVersion,
            contractFamily,
            ArchitectureViolationIdentity.ResolveKind(contractFamily),
            contractId,
            SourceAssembly: null,
            candidate.SourceType,
            SourceMember: null,
            TargetAssembly: null,
            TargetType: null,
            candidate.ForbiddenReference,
            Occurrence: 0);
    }

    private static Dictionary<string, Dictionary<string, string>> BuildCanonicalIdsByGroup(Families.ArchitectureContractGroups groups)
    {
        Dictionary<string, Dictionary<string, string>> result = new(StringComparer.Ordinal);

        foreach (string groupName in ArchitectureBaselineContractGroups.GroupNames)
        {
            Dictionary<string, string> ids = new(StringComparer.OrdinalIgnoreCase);
            foreach (string contractId in GetKnownContractIds(groups, groupName))
            {
                ids[contractId] = contractId;
            }

            result[groupName] = ids;
        }

        return result;
    }

    private static HashSet<string> GetKnownContractIds(Families.ArchitectureContractGroups groups, string groupName)
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
            "strict_method_body" => groups.StrictMethodBody.Select(c => c.Id),
            "audit_method_body" => groups.AuditMethodBody.Select(c => c.Id),
            "strict_independence" => groups.StrictIndependence.Select(c => c.Id),
            "audit_independence" => groups.AuditIndependence.Select(c => c.Id),
            "strict_assembly_independence" => groups.StrictAssemblyIndependence.Select(c => c.Id),
            "audit_assembly_independence" => groups.AuditAssemblyIndependence.Select(c => c.Id),
            "strict_assembly_dependency" => groups.StrictAssemblyDependency.Select(c => c.Id),
            "audit_assembly_dependency" => groups.AuditAssemblyDependency.Select(c => c.Id),
            "strict_assembly_allow_only" => groups.StrictAssemblyAllowOnly.Select(c => c.Id),
            "audit_assembly_allow_only" => groups.AuditAssemblyAllowOnly.Select(c => c.Id),
            "strict_package_dependency" => groups.StrictPackageDependency.Select(c => c.Id),
            "audit_package_dependency" => groups.AuditPackageDependency.Select(c => c.Id),
            "strict_package_allow_only" => groups.StrictPackageAllowOnly.Select(c => c.Id),
            "audit_package_allow_only" => groups.AuditPackageAllowOnly.Select(c => c.Id),
            "strict_project_metadata" => groups.StrictProjectMetadata.Select(c => c.Id),
            "audit_project_metadata" => groups.AuditProjectMetadata.Select(c => c.Id),
            "strict_protected" => groups.StrictProtected.Select(c => c.Id),
            "audit_protected" => groups.AuditProtected.Select(c => c.Id),
            "strict_external" => groups.StrictExternal.Select(c => c.Id),
            "audit_external" => groups.AuditExternal.Select(c => c.Id),
            "strict_external_allow_only" => groups.StrictExternalAllowOnly.Select(c => c.Id),
            "audit_external_allow_only" => groups.AuditExternalAllowOnly.Select(c => c.Id),
            "strict_acyclic_siblings" => groups.StrictAcyclicSiblings.Select(c => c.Id),
            "audit_acyclic_siblings" => groups.AuditAcyclicSiblings.Select(c => c.Id),
            "strict_type_placement" => groups.StrictTypePlacement.Select(c => c.Id),
            "audit_type_placement" => groups.AuditTypePlacement.Select(c => c.Id),
            "strict_layout_conventions" => groups.StrictLayoutConventions.Select(c => c.Id),
            "audit_layout_conventions" => groups.AuditLayoutConventions.Select(c => c.Id),
            "strict_public_api_surface" => groups.StrictPublicApiSurface.Select(c => c.Id),
            "audit_public_api_surface" => groups.AuditPublicApiSurface.Select(c => c.Id),
            "strict_attribute_usage" => groups.StrictAttributeUsage.Select(c => c.Id),
            "audit_attribute_usage" => groups.AuditAttributeUsage.Select(c => c.Id),
            "strict_inheritance" => groups.StrictInheritance.Select(c => c.Id),
            "audit_inheritance" => groups.AuditInheritance.Select(c => c.Id),
            "strict_interface_implementation" => groups.StrictInterfaceImplementation.Select(c => c.Id),
            "audit_interface_implementation" => groups.AuditInterfaceImplementation.Select(c => c.Id),
            "strict_composition" => groups.StrictComposition.Select(c => c.Id),
            "audit_composition" => groups.AuditComposition.Select(c => c.Id),
            "strict_coverage" => groups.StrictCoverage.Select(c => c.Id),
            "audit_coverage" => groups.AuditCoverage.Select(c => c.Id),
            _ => Enumerable.Empty<string?>(),
        };

        return new HashSet<string>(ids.Where(id => id != null)!, StringComparer.OrdinalIgnoreCase);
    }

    private static bool HasMatchingCandidateLegacy(
        IReadOnlyList<ArchitectureBaselineCandidate> candidates,
        string groupName,
        string contractId,
        string sourceType,
        string forbiddenReference)
    {
        foreach (var candidate in candidates)
        {
            if (candidate.ContractGroup == groupName
                && string.Equals(candidate.ContractId, contractId, StringComparison.OrdinalIgnoreCase)
                && candidate.SourceType == sourceType
                && candidate.ForbiddenReference == forbiddenReference)
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasMatchingCandidateByIdentity(
        IReadOnlyList<ArchitectureBaselineCandidate> candidates,
        string groupName,
        ArchitectureViolationIdentity targetIdentity)
    {
        foreach (var candidate in candidates)
        {
            if (candidate.ContractGroup != groupName || candidate.ContractId == null)
            {
                continue;
            }

            ArchitectureViolationIdentity candidateIdentity = candidate.Identity
                ?? BuildFallbackIdentity(groupName, candidate.ContractId, candidate);

            if (string.Equals(candidateIdentity.ContractId, targetIdentity.ContractId, StringComparison.OrdinalIgnoreCase)
                && candidateIdentity with { ContractId = targetIdentity.ContractId } == targetIdentity)
            {
                return true;
            }
        }

        return false;
    }

    private static string CanonicalizeContractId(Dictionary<string, string> canonicalIds, string contractId)
    {
        return canonicalIds.TryGetValue(contractId, out string? canonicalId)
            ? canonicalId
            : contractId;
    }

    private static string BuildLegacyKey(string contractId, string sourceType, string forbiddenReference)
    {
        return $"{contractId}|{sourceType}|{forbiddenReference}";
    }

    private static string BuildIdentityKey(ArchitectureViolationIdentity identity)
    {
        return identity.ToString();
    }

    private static bool IsInScope(string groupName, string mode)
    {
        if (mode == "all")
        {
            return true;
        }

        return groupName == mode || groupName.StartsWith(mode + "_", StringComparison.Ordinal);
    }
}
