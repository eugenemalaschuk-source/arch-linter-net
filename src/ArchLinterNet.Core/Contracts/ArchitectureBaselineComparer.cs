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
        var ambiguous = new List<ArchitectureBaselineComparisonEntry>();

        // Baseline entries carry the exact contract id a finding carries, which for an expanded
        // contract is the derived per-source instance id. Selecting the authored id the policy
        // author wrote must therefore also select every instance it produced, or `--contract
        // <authored-id>` silently matches no entry. Entry identity itself stays exact.
        HashSet<string>? selectedIds = selectedContractIds is { Count: > 0 }
            ? new HashSet<string>(
                selectedContractIds.SelectMany(id =>
                    new[] { id }.Concat(policyDocument.SourceExpansion.InstanceIdsFor(id))),
                StringComparer.OrdinalIgnoreCase)
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
                new BaselineGroupScope(groupName, entries, groupInScope, selectedIds, knownIds, canonicalIds),
                candidates,
                useStructuredIdentity,
                new BaselineClassification(outOfScope, configurationErrors, frozen, resolved, ambiguous));

            if (!groupInScope)
            {
                continue;
            }

            ProcessNewCandidates(groupName, candidates, selectedIds, canonicalIds, baselineKeys, useStructuredIdentity, newEntries);
        }

        return new ArchitectureBaselineComparisonResult(newEntries, frozen, resolved, configurationErrors, outOfScope)
        {
            Ambiguous = ambiguous,
        };
    }

    // The per-group inputs comparison needs, bundled so classification reads as one step rather than
    // a dozen positional arguments threaded through it.
    private sealed record BaselineGroupScope(
        string GroupName,
        List<ArchitectureBaselineContractEntry> Entries,
        bool GroupInScope,
        HashSet<string>? SelectedIds,
        HashSet<string> KnownIds,
        Dictionary<string, string> CanonicalIds);

    // The five buckets an entry can land in.
    private sealed record BaselineClassification(
        List<ArchitectureBaselineComparisonEntry> OutOfScope,
        List<ArchitectureBaselineComparisonEntry> ConfigurationErrors,
        List<ArchitectureBaselineComparisonEntry> Frozen,
        List<ArchitectureBaselineComparisonEntry> Resolved,
        List<ArchitectureBaselineComparisonEntry> Ambiguous);

    private static HashSet<string> ProcessBaselineEntries(
        BaselineGroupScope scope,
        IReadOnlyList<ArchitectureBaselineCandidate> candidates,
        bool useStructuredIdentity,
        BaselineClassification classification)
    {
        var baselineKeys = new HashSet<string>(StringComparer.Ordinal);

        foreach (var entry in scope.Entries)
        {
            bool entryInScope = scope.GroupInScope && (scope.SelectedIds == null || scope.SelectedIds.Contains(entry.Id));

            // Out-of-scope entries (wrong mode, or not among the selected --contract ids) are
            // carried through verbatim so scoped update/prune never drops unrelated debt.
            if (!entryInScope)
            {
                foreach (var ignore in entry.IgnoredViolations)
                {
                    classification.OutOfScope.Add(BuildComparisonEntry(scope.GroupName, entry.Id, ignore, useStructuredIdentity));
                }

                continue;
            }

            bool idKnown = scope.KnownIds.Contains(entry.Id);
            string canonicalContractId = CanonicalizeContractId(scope.CanonicalIds, entry.Id);

            foreach (var ignore in entry.IgnoredViolations)
            {
                baselineKeys.Add(useStructuredIdentity
                    ? BuildIdentityKey(ignore.ToIdentity(canonicalContractId))
                    : BuildLegacyKey(canonicalContractId, ignore.SourceType, ignore.ForbiddenReference));

                ClassifyBaselineEntry(
                    scope.GroupName, entry.Id, canonicalContractId, ignore, idKnown,
                    candidates, useStructuredIdentity, classification);
            }
        }

        return baselineKeys;
    }

    private static void ClassifyBaselineEntry(
        string groupName,
        string entryId,
        string canonicalContractId,
        ArchitectureBaselineIgnoredViolation ignore,
        bool idKnown,
        IReadOnlyList<ArchitectureBaselineCandidate> candidates,
        bool useStructuredIdentity,
        BaselineClassification classification)
    {
        ArchitectureBaselineComparisonEntry comparisonEntry =
            BuildComparisonEntry(groupName, entryId, ignore, useStructuredIdentity);

        if (!idKnown)
        {
            classification.ConfigurationErrors.Add(comparisonEntry);
            return;
        }

        // Counting every match — rather than stopping at the first — is what separates "this entry
        // suppresses exactly the violation it was written for" from "this entry would suppress several
        // distinct violations". The latter is a broadening ratchet, so it is reported for review
        // instead of being silently treated as matched.
        List<ArchitectureBaselineCandidate> matches = useStructuredIdentity
            ? MatchCandidatesByIdentity(candidates, groupName, ignore.ToIdentity(canonicalContractId))
            : MatchCandidatesLegacy(candidates, groupName, canonicalContractId, ignore.SourceType, ignore.ForbiddenReference);

        switch (matches.Count)
        {
            case 0:
                classification.Resolved.Add(comparisonEntry);
                break;
            case 1:
                ArchitectureViolationIdentity matchedIdentity = matches[0].Identity
                    ?? BuildFallbackIdentity(groupName, canonicalContractId, matches[0]);
                classification.Frozen.Add(
                    comparisonEntry with
                    {
                        Identity = matchedIdentity with { ContractId = canonicalContractId },
                        CurrentForbiddenReference = matches[0].ForbiddenReference,
                    });
                break;
            default:
                classification.Ambiguous.Add(comparisonEntry);
                break;
        }
    }

    private static ArchitectureBaselineComparisonEntry BuildComparisonEntry(
        string groupName, string contractId, ArchitectureBaselineIgnoredViolation ignore, bool useStructuredIdentity)
    {
        return new ArchitectureBaselineComparisonEntry(
            groupName, contractId, ignore.SourceType, ignore.ForbiddenReference, ignore.Reason,
            useStructuredIdentity ? ignore.ToIdentity(contractId) : null)
        {
            Issue = ignore.Issue,
        };
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
            "strict_framework_dependency" => groups.StrictFrameworkDependency.Select(c => c.Id),
            "audit_framework_dependency" => groups.AuditFrameworkDependency.Select(c => c.Id),
            "strict_framework_allow_only" => groups.StrictFrameworkAllowOnly.Select(c => c.Id),
            "audit_framework_allow_only" => groups.AuditFrameworkAllowOnly.Select(c => c.Id),
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
            "strict_module_containers" => groups.StrictModuleContainers.Select(c => c.Id),
            "audit_module_containers" => groups.AuditModuleContainers.Select(c => c.Id),
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
            "strict_metric_budgets" => groups.StrictMetricBudgets.Select(c => c.Id),
            "audit_metric_budgets" => groups.AuditMetricBudgets.Select(c => c.Id),
            _ => Enumerable.Empty<string?>(),
        };

        return new HashSet<string>(ids.Where(id => id != null)!, StringComparer.OrdinalIgnoreCase);
    }

    private static List<ArchitectureBaselineCandidate> MatchCandidatesLegacy(
        IReadOnlyList<ArchitectureBaselineCandidate> candidates,
        string groupName,
        string contractId,
        string sourceType,
        string forbiddenReference)
    {
        var matches = new List<ArchitectureBaselineCandidate>();

        foreach (var candidate in candidates)
        {
            if (candidate.ContractGroup == groupName
                && string.Equals(candidate.ContractId, contractId, StringComparison.OrdinalIgnoreCase)
                && candidate.SourceType == sourceType
                && candidate.ForbiddenReference == forbiddenReference)
            {
                matches.Add(candidate);
            }
        }

        return matches;
    }

    private static List<ArchitectureBaselineCandidate> MatchCandidatesByIdentity(
        IReadOnlyList<ArchitectureBaselineCandidate> candidates,
        string groupName,
        ArchitectureViolationIdentity targetIdentity)
    {
        var matches = new List<ArchitectureBaselineCandidate>();

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
                matches.Add(candidate);
            }
        }

        return matches;
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
