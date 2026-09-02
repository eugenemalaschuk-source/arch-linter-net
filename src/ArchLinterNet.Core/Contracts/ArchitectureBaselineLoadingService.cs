using ArchLinterNet.Core.Contracts.Abstractions;
using ArchLinterNet.Core.Contracts.Families;
using ArchLinterNet.Core.IO;
using ArchLinterNet.Core.IO.Abstractions;
using ArchLinterNet.Core.Model;
using YamlDotNet.RepresentationModel;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace ArchLinterNet.Core.Contracts;

public sealed class ArchitectureBaselineLoadingService : IArchitectureBaselineLoadingService
{
    private readonly IArchitectureFileSystem _fileSystem;

    public ArchitectureBaselineLoadingService()
        : this(ArchitectureFileSystem.Real)
    {
    }

    public ArchitectureBaselineLoadingService(IArchitectureFileSystem fileSystem)
    {
        _fileSystem = fileSystem;
    }

    public void LoadAndMerge(ArchitectureContractDocument document, string baselinePath)
    {
        LoadedBaseline baseline = LoadWithIdentity(baselinePath);
        MergeAndValidate(document, baseline.Document);
        document.BaselineContentIdentity = baseline.ContentIdentity;
        document.MetricBaselines = baseline.Document.MetricBaselines.ToArray();
    }

    public ArchitectureBaselineDocument Load(string baselinePath)
    {
        return LoadWithIdentity(baselinePath).Document;
    }

    public string ReadRawText(string baselinePath)
    {
        if (!_fileSystem.FileExists(baselinePath))
        {
            throw new FileNotFoundException($"Baseline file not found: {baselinePath}");
        }

        return _fileSystem.ReadAllText(baselinePath);
    }

    internal ArchitectureBaselineDocument LoadFromPath(string baselinePath)
    {
        return LoadWithIdentity(baselinePath).Document;
    }

    private LoadedBaseline LoadWithIdentity(string baselinePath)
    {
        if (!_fileSystem.FileExists(baselinePath))
        {
            throw new FileNotFoundException($"Baseline file not found: {baselinePath}");
        }

        IDeserializer deserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();

        string yaml = _fileSystem.ReadAllText(baselinePath);
        ArchitectureBaselineDocument? document = deserializer.Deserialize<ArchitectureBaselineDocument>(yaml);

        if (document == null)
        {
            throw new InvalidOperationException("Failed to deserialize baseline YAML.");
        }

        ValidateBaseline(document, yaml);
        return new LoadedBaseline(
            document,
            ArchitectureLoadedTextIdentityFactory.FromText(baselinePath, yaml));
    }

    private sealed record LoadedBaseline(
        ArchitectureBaselineDocument Document,
        ArchitectureLoadedTextIdentity ContentIdentity);

    private static void ValidateBaseline(ArchitectureBaselineDocument document, string yaml)
    {
        if (document.Version is not (1 or 2 or 3))
        {
            throw new InvalidOperationException(
                $"Unsupported baseline version: {document.Version}. Only versions 1, 2, and 3 are supported.");
        }

        if (document.MetricBaselines is null)
        {
            throw new InvalidOperationException("Baseline 'metric_baselines' must be an array when present.");
        }

        if (document.Version != 3 && document.MetricBaselines.Count > 0)
        {
            throw new InvalidOperationException(
                $"Baseline contains 'metric_baselines', but the document is 'version: {document.Version}'. " +
                "Metric baselines are only valid in a 'version: 3' document.");
        }

        foreach (string groupName in ArchitectureBaselineContractGroups.GroupNames)
        {
            ValidateGroupEntries(document.Baseline.GetGroup(groupName), groupName, document.Version);
        }

        if (document.Version == 3)
        {
            ValidateMetricBaselineRequiredFields(yaml);
            ValidateMetricBaselines(document.MetricBaselines);
        }
    }

    // The baseline deserializer deliberately ignores unmatched properties for forward compatibility.
    // For the versioned metric record, however, required keys must be fail-closed: an omitted key
    // or a typo such as `valu` must not be silently mapped to a scalar default value.
    private static void ValidateMetricBaselineRequiredFields(string yaml)
    {
        var stream = new YamlStream();
        using var reader = new StringReader(yaml);
        stream.Load(reader);

        if (stream.Documents.Count != 1
            || stream.Documents[0].RootNode is not YamlMappingNode root
            || !TryGetChild(root, "metric_baselines", out YamlNode? metricBaselines)
            || metricBaselines is not YamlSequenceNode entries)
        {
            return;
        }

        string[] requiredKeys =
        [
            "metric_identity_version", "metric_id", "metric_kind", "native_subject", "effective_scope", "value",
        ];
        string[] allowedKeys = [.. requiredKeys, "unit"];
        for (int index = 0; index < entries.Children.Count; index++)
        {
            if (entries.Children[index] is not YamlMappingNode entry)
            {
                continue;
            }

            foreach (YamlNode field in entry.Children.Keys)
            {
                if (field is not YamlScalarNode { Value: { } fieldName })
                {
                    throw new InvalidOperationException(
                        $"metric_baselines entry at index {index} has an unknown non-scalar field.");
                }

                if (!allowedKeys.Contains(fieldName, StringComparer.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"metric_baselines entry at index {index} has an unknown field '{fieldName}'.");
                }
            }

            foreach (string requiredKey in requiredKeys)
            {
                if (!TryGetChild(entry, requiredKey, out _))
                {
                    throw new InvalidOperationException(
                        $"metric_baselines entry at index {index} has a missing '{requiredKey}'.");
                }
            }
        }
    }

    private static bool TryGetChild(YamlMappingNode mapping, string key, out YamlNode? value)
    {
        foreach ((YamlNode node, YamlNode child) in mapping.Children)
        {
            if (node is YamlScalarNode { Value: { } nodeKey }
                && string.Equals(nodeKey, key, StringComparison.Ordinal))
            {
                value = child;
                return true;
            }
        }

        value = null;
        return false;
    }

    private static void ValidateMetricBaselines(IReadOnlyList<ArchitectureMetricBaselineEntry> entries)
    {
        var metricIds = new HashSet<string>(StringComparer.Ordinal);
        for (int index = 0; index < entries.Count; index++)
        {
            ArchitectureMetricBaselineEntry entry = entries[index];
            string location = $"metric_baselines entry at index {index}";

            if (entry.MetricIdentityVersion is null)
            {
                throw new InvalidOperationException(
                    $"{location} has a missing 'metric_identity_version'.");
            }

            if (entry.MetricIdentityVersion != ArchitectureMetricBaselineIdentity.CurrentVersion)
            {
                throw new InvalidOperationException(
                    $"{location} has unsupported 'metric_identity_version' " +
                    $"(expected {ArchitectureMetricBaselineIdentity.CurrentVersion}).");
            }

            if (string.IsNullOrWhiteSpace(entry.MetricId))
            {
                throw new InvalidOperationException(
                    $"{location} has an empty or missing 'metric_id'.");
            }

            if (!metricIds.Add(entry.MetricId))
            {
                throw new InvalidOperationException(
                    $"Duplicate metric baseline id '{entry.MetricId}'. Each metric ID may appear only once.");
            }

            if (string.IsNullOrWhiteSpace(entry.MetricKind))
            {
                throw new InvalidOperationException(
                    $"{location} for metric '{entry.MetricId}' has an empty or missing 'metric_kind'.");
            }

            if (!ArchitectureMetricKinds.All.Contains(entry.MetricKind, StringComparer.Ordinal))
            {
                throw new InvalidOperationException(
                    $"{location} for metric '{entry.MetricId}' has unsupported 'metric_kind' '{entry.MetricKind}'.");
            }

            if (string.IsNullOrWhiteSpace(entry.NativeSubject))
            {
                throw new InvalidOperationException(
                    $"{location} for metric '{entry.MetricId}' has an empty or missing 'native_subject'.");
            }

            if (entry.Unit is not null)
            {
                if (string.IsNullOrWhiteSpace(entry.Unit))
                {
                    throw new InvalidOperationException(
                        $"{location} for metric '{entry.MetricId}' has an empty 'unit'.");
                }

                if (entry.Unit is not ("project" or "assembly"))
                {
                    throw new InvalidOperationException(
                        $"{location} for metric '{entry.MetricId}' has unsupported 'unit' '{entry.Unit}'.");
                }
            }

            if (string.IsNullOrWhiteSpace(entry.EffectiveScope))
            {
                throw new InvalidOperationException(
                    $"{location} for metric '{entry.MetricId}' has an empty or missing 'effective_scope'.");
            }

            if (entry.Value is null)
            {
                throw new InvalidOperationException(
                    $"{location} for metric '{entry.MetricId}' has a missing 'value'.");
            }

            if (entry.Value < 0)
            {
                throw new InvalidOperationException(
                    $"{location} for metric '{entry.MetricId}' has a negative 'value'.");
            }
        }
    }

    private static void ValidateGroupEntries(List<ArchitectureBaselineContractEntry> entries, string groupName, int documentVersion)
    {
        bool isStructured = documentVersion is 2 or 3;

        for (int i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            if (string.IsNullOrWhiteSpace(entry.Id))
            {
                throw new InvalidOperationException(
                    $"Baseline entry at index {i} in group '{groupName}' has an empty or missing 'id'. " +
                    "Each baseline entry must reference a contract by its 'id'.");
            }

            for (int j = 0; j < entry.IgnoredViolations.Count; j++)
            {
                var ignore = entry.IgnoredViolations[j];
                if (string.IsNullOrWhiteSpace(ignore.SourceType))
                {
                    throw new InvalidOperationException(
                        $"Baseline entry '{entry.Id}' in group '{groupName}' has an ignored_violations entry " +
                        $"at index {j} with an empty or missing 'source_type'.");
                }

                if (string.IsNullOrWhiteSpace(ignore.ForbiddenReference))
                {
                    throw new InvalidOperationException(
                        $"Baseline entry '{entry.Id}' in group '{groupName}' has an ignored_violations entry " +
                        $"at index {j} with an empty or missing 'forbidden_reference'.");
                }

                if (isStructured)
                {
                    ValidateStructuredIdentity(ignore, entry.Id, groupName, j);
                }
                else if (ignore.IdentityVersion != null)
                {
                    throw new InvalidOperationException(
                        $"Baseline entry '{entry.Id}' in group '{groupName}' has an ignored_violations entry " +
                        $"at index {j} with an 'identity_version' field, but the document is 'version: {documentVersion}'. " +
                        "Structured identity fields are only valid in a 'version: 2' or 'version: 3' document.");
                }
            }
        }
    }

    // A `version: 2` or `version: 3` document is only trustworthy if every entry actually carries the structured
    // identity that version claims — otherwise a legacy-shaped (or hand-edited, or corrupted) entry
    // mislabeled as version 2 would silently fall back to default/empty identity fields and match
    // differently at `validate` time than it does in `diff`/`verify`/`migrate`. Fail closed instead.
    private static void ValidateStructuredIdentity(ArchitectureBaselineIgnoredViolation ignore, string entryId, string groupName, int index)
    {
        if (ignore.IdentityVersion != ArchitectureViolationIdentity.CurrentVersion)
        {
            throw new InvalidOperationException(
                $"Baseline entry '{entryId}' in group '{groupName}' has an ignored_violations entry at index {index} " +
                $"with a missing or unsupported 'identity_version' (expected {ArchitectureViolationIdentity.CurrentVersion}) " +
                "in a 'version: 2' or 'version: 3' document. Every entry in a structured baseline must carry structured identity.");
        }

        if (string.IsNullOrWhiteSpace(ignore.ContractFamily))
        {
            throw new InvalidOperationException(
                $"Baseline entry '{entryId}' in group '{groupName}' has an ignored_violations entry at index {index} " +
                "with an empty or missing 'contract_family', required in a 'version: 2' or 'version: 3' document.");
        }

        if (string.IsNullOrWhiteSpace(ignore.Kind))
        {
            throw new InvalidOperationException(
                $"Baseline entry '{entryId}' in group '{groupName}' has an ignored_violations entry at index {index} " +
                "with an empty or missing 'kind', required in a 'version: 2' or 'version: 3' document.");
        }

        if (ignore.Occurrence == null || ignore.Occurrence < 0)
        {
            throw new InvalidOperationException(
                $"Baseline entry '{entryId}' in group '{groupName}' has an ignored_violations entry at index {index} " +
                "with a missing or negative 'occurrence', required in a 'version: 2' or 'version: 3' document.");
        }
    }

    internal static void Merge(ArchitectureContractDocument policyDocument, ArchitectureBaselineDocument baselineDocument)
    {
        var groupMerger = new ContractGroupMerger(policyDocument);
        foreach (string groupName in ArchitectureBaselineContractGroups.GroupNames)
        {
            groupMerger.MergeGroup(baselineDocument.Baseline.GetGroup(groupName), groupName, baselineDocument.Version);
        }
    }

    internal static void MergeAndValidate(
        ArchitectureContractDocument policyDocument,
        ArchitectureBaselineDocument baselineDocument)
    {
        var unknownIds = new List<(string GroupName, string ContractId)>();
        var groupMerger = new ContractGroupMerger(policyDocument);

        foreach (string groupName in ArchitectureBaselineContractGroups.GroupNames)
        {
            unknownIds.AddRange(groupMerger.MergeGroup(
                baselineDocument.Baseline.GetGroup(groupName), groupName, baselineDocument.Version));
        }

        if (unknownIds.Count > 0)
        {
            string details = string.Join("; ", unknownIds.Select(x => $"group '{x.GroupName}', id '{x.ContractId}'"));
            throw new InvalidOperationException(
                $"Baseline references unknown contract IDs: {details}." +
                " These contracts do not exist in the current policy document.");
        }
    }

    private sealed class ContractGroupMerger
    {
        private readonly Families.ArchitectureContractGroups _groups;
        private readonly HashSet<string> _externalEvidenceIds;

        public ContractGroupMerger(ArchitectureContractDocument document)
        {
            _groups = document.Contracts;
            _externalEvidenceIds = new HashSet<string>(
                document.ExternalEvidence.Select(requirement => requirement.Id),
                StringComparer.Ordinal);
        }

        public List<(string GroupName, string ContractId)> MergeGroup(
            List<ArchitectureBaselineContractEntry> baselineEntries,
            string groupName,
            int documentVersion)
        {
            var unknownIds = new List<(string GroupName, string ContractId)>();
            var contracts = GetContracts(groupName);
            bool isStructured = documentVersion is 2 or 3;

            foreach (var baselineEntry in baselineEntries)
            {
                if (IsImportedExternalDiagnosticEntry(baselineEntry, groupName, documentVersion))
                {
                    if (!_externalEvidenceIds.Contains(baselineEntry.Id))
                    {
                        unknownIds.Add((groupName, baselineEntry.Id));
                    }

                    // Imported findings are compared through their exact structured identities by
                    // baseline lifecycle services. They must not become native external-contract
                    // ignores, which could otherwise affect ArchitectureIgnoreMatcher.
                    continue;
                }

                var contract = contracts.FirstOrDefault(c =>
                    string.Equals(c.Id, baselineEntry.Id, StringComparison.OrdinalIgnoreCase));

                if (contract == null)
                {
                    unknownIds.Add((groupName, baselineEntry.Id));
                    continue;
                }

                var ignores = GetIgnoredViolations(contract);
                foreach (var baselineIgnore in baselineEntry.IgnoredViolations)
                {
                    // Applicability findings are evidence of an assessment boundary, not
                    // ordinary conformance violations. Validate their owning entry above, but do
                    // not feed them into ArchitectureIgnoreMatcher: doing so would let a matching
                    // baseline entry turn an unassessable assessment into a pass (and would also
                    // create a spurious unmatched-ignore result).
                    if (isStructured
                        && string.Equals(baselineIgnore.Kind, "applicability", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    // Version-1 entries dedup by the legacy display pair, exactly as before. Version-2
                    // entries dedup by the full structured identity — two entries can legitimately
                    // share (source_type, forbidden_reference) display text while being distinct
                    // identities (different assembly/member/occurrence), and must both be kept.
                    bool isDuplicate = isStructured
                        ? ignores.Any(existing => IdentityEquals(existing, baselineIgnore))
                        : ignores.Any(existing =>
                            string.Equals(existing.SourceType, baselineIgnore.SourceType, StringComparison.Ordinal) &&
                            string.Equals(existing.ForbiddenReference, baselineIgnore.ForbiddenReference, StringComparison.Ordinal));

                    if (!isDuplicate)
                    {
                        ignores.Add(new ArchitectureIgnoredViolation
                        {
                            SourceType = baselineIgnore.SourceType,
                            ForbiddenReference = baselineIgnore.ForbiddenReference,
                            Reason = baselineIgnore.Reason,
                            IdentityVersion = isStructured ? baselineIgnore.IdentityVersion ?? ArchitectureViolationIdentity.CurrentVersion : null,
                            ContractFamily = isStructured ? baselineIgnore.ContractFamily : null,
                            Kind = isStructured ? baselineIgnore.Kind : null,
                            SourceAssembly = isStructured ? baselineIgnore.SourceAssembly : null,
                            SourceMember = isStructured ? baselineIgnore.SourceMember : null,
                            TargetAssembly = isStructured ? baselineIgnore.TargetAssembly : null,
                            TargetType = isStructured ? baselineIgnore.TargetType : null,
                            TargetMember = isStructured ? baselineIgnore.TargetMember : null,
                            Occurrence = isStructured ? baselineIgnore.Occurrence ?? 0 : null,
                            Configuration = isStructured ? baselineIgnore.Configuration : null,
                        });
                    }
                }
            }

            return unknownIds;
        }

        private static bool IsImportedExternalDiagnosticEntry(
            ArchitectureBaselineContractEntry entry,
            string groupName,
            int documentVersion)
        {
            if (documentVersion is not (2 or 3)
                || groupName is not ("strict_external" or "audit_external"))
            {
                return false;
            }

            bool containsImportedDiagnostic = entry.IgnoredViolations.Any(ignore =>
                string.Equals(ignore.Kind, "external_diagnostic", StringComparison.Ordinal));
            if (!containsImportedDiagnostic)
            {
                return false;
            }

            if (entry.IgnoredViolations.Any(ignore =>
                    !string.Equals(ignore.Kind, "external_diagnostic", StringComparison.Ordinal)))
            {
                throw new InvalidOperationException(
                    $"Baseline entry '{entry.Id}' in group '{groupName}' cannot mix imported external diagnostics with native contract ignores.");
            }

            return true;
        }

        private static bool IdentityEquals(ArchitectureIgnoredViolation existing, ArchitectureBaselineIgnoredViolation candidate)
        {
            return existing.IdentityVersion == ArchitectureViolationIdentity.CurrentVersion
                && string.Equals(existing.ContractFamily, candidate.ContractFamily, StringComparison.Ordinal)
                && string.Equals(existing.SourceAssembly, candidate.SourceAssembly, StringComparison.Ordinal)
                && string.Equals(existing.SourceType, candidate.SourceType, StringComparison.Ordinal)
                && string.Equals(existing.SourceMember, candidate.SourceMember, StringComparison.Ordinal)
                && string.Equals(existing.TargetAssembly, candidate.TargetAssembly, StringComparison.Ordinal)
                && string.Equals(existing.TargetType, candidate.TargetType, StringComparison.Ordinal)
                && string.Equals(existing.TargetMember, candidate.TargetMember, StringComparison.Ordinal)
                && (existing.Occurrence ?? 0) == (candidate.Occurrence ?? 0)
                && string.Equals(existing.Configuration, candidate.Configuration, StringComparison.Ordinal);
        }

        private List<IArchitectureContract> GetContracts(string groupName)
        {
            return groupName switch
            {
                "strict" => _groups.Strict.Select(c => (IArchitectureContract)c).ToList(),
                "audit" => _groups.Audit.Select(c => (IArchitectureContract)c).ToList(),
                "strict_layers" => _groups.StrictLayers.Select(c => (IArchitectureContract)c).ToList(),
                "audit_layers" => _groups.AuditLayers.Select(c => (IArchitectureContract)c).ToList(),
                "strict_allow_only" => _groups.StrictAllowOnly.Select(c => (IArchitectureContract)c).ToList(),
                "audit_allow_only" => _groups.AuditAllowOnly.Select(c => (IArchitectureContract)c).ToList(),
                "strict_cycles" => _groups.StrictCycles.Select(c => (IArchitectureContract)c).ToList(),
                "audit_cycles" => _groups.AuditCycles.Select(c => (IArchitectureContract)c).ToList(),
                "strict_acyclic_siblings" => _groups.StrictAcyclicSiblings.Select(c => (IArchitectureContract)c).ToList(),
                "audit_acyclic_siblings" => _groups.AuditAcyclicSiblings.Select(c => (IArchitectureContract)c).ToList(),
                "strict_module_containers" => _groups.StrictModuleContainers.Select(c => (IArchitectureContract)c).ToList(),
                "audit_module_containers" => _groups.AuditModuleContainers.Select(c => (IArchitectureContract)c).ToList(),
                "strict_method_body" => _groups.StrictMethodBody.Select(c => (IArchitectureContract)c).ToList(),
                "audit_method_body" => _groups.AuditMethodBody.Select(c => (IArchitectureContract)c).ToList(),
                "strict_independence" => _groups.StrictIndependence.Select(c => (IArchitectureContract)c).ToList(),
                "audit_independence" => _groups.AuditIndependence.Select(c => (IArchitectureContract)c).ToList(),
                "strict_assembly_independence" => _groups.StrictAssemblyIndependence.Select(c => (IArchitectureContract)c).ToList(),
                "audit_assembly_independence" => _groups.AuditAssemblyIndependence.Select(c => (IArchitectureContract)c).ToList(),
                "strict_assembly_dependency" => _groups.StrictAssemblyDependency.Select(c => (IArchitectureContract)c).ToList(),
                "audit_assembly_dependency" => _groups.AuditAssemblyDependency.Select(c => (IArchitectureContract)c).ToList(),
                "strict_assembly_allow_only" => _groups.StrictAssemblyAllowOnly.Select(c => (IArchitectureContract)c).ToList(),
                "audit_assembly_allow_only" => _groups.AuditAssemblyAllowOnly.Select(c => (IArchitectureContract)c).ToList(),
                "strict_package_dependency" => _groups.StrictPackageDependency.Select(c => (IArchitectureContract)c).ToList(),
                "audit_package_dependency" => _groups.AuditPackageDependency.Select(c => (IArchitectureContract)c).ToList(),
                "strict_package_allow_only" => _groups.StrictPackageAllowOnly.Select(c => (IArchitectureContract)c).ToList(),
                "audit_package_allow_only" => _groups.AuditPackageAllowOnly.Select(c => (IArchitectureContract)c).ToList(),
                "strict_framework_dependency" => _groups.StrictFrameworkDependency.Select(c => (IArchitectureContract)c).ToList(),
                "audit_framework_dependency" => _groups.AuditFrameworkDependency.Select(c => (IArchitectureContract)c).ToList(),
                "strict_framework_allow_only" => _groups.StrictFrameworkAllowOnly.Select(c => (IArchitectureContract)c).ToList(),
                "audit_framework_allow_only" => _groups.AuditFrameworkAllowOnly.Select(c => (IArchitectureContract)c).ToList(),
                "strict_project_metadata" => _groups.StrictProjectMetadata.Select(c => (IArchitectureContract)c).ToList(),
                "audit_project_metadata" => _groups.AuditProjectMetadata.Select(c => (IArchitectureContract)c).ToList(),
                "strict_protected" => _groups.StrictProtected.Select(c => (IArchitectureContract)c).ToList(),
                "audit_protected" => _groups.AuditProtected.Select(c => (IArchitectureContract)c).ToList(),
                "strict_external" => _groups.StrictExternal.Select(c => (IArchitectureContract)c).ToList(),
                "audit_external" => _groups.AuditExternal.Select(c => (IArchitectureContract)c).ToList(),
                "strict_external_allow_only" => _groups.StrictExternalAllowOnly.Select(c => (IArchitectureContract)c).ToList(),
                "audit_external_allow_only" => _groups.AuditExternalAllowOnly.Select(c => (IArchitectureContract)c).ToList(),
                "strict_type_placement" => _groups.StrictTypePlacement.Select(c => (IArchitectureContract)c).ToList(),
                "audit_type_placement" => _groups.AuditTypePlacement.Select(c => (IArchitectureContract)c).ToList(),
                "strict_layout_conventions" => _groups.StrictLayoutConventions.Select(c => (IArchitectureContract)c).ToList(),
                "audit_layout_conventions" => _groups.AuditLayoutConventions.Select(c => (IArchitectureContract)c).ToList(),
                "strict_layout_convention_applicability" => _groups.StrictLayoutConventionApplicability.Select(c => (IArchitectureContract)c).ToList(),
                "audit_layout_convention_applicability" => _groups.AuditLayoutConventionApplicability.Select(c => (IArchitectureContract)c).ToList(),
                "strict_public_api_surface" => _groups.StrictPublicApiSurface.Select(c => (IArchitectureContract)c).ToList(),
                "audit_public_api_surface" => _groups.AuditPublicApiSurface.Select(c => (IArchitectureContract)c).ToList(),
                "strict_contract_surface_exposure" => _groups.StrictContractSurfaceExposure.Select(c => (IArchitectureContract)c).ToList(),
                "audit_contract_surface_exposure" => _groups.AuditContractSurfaceExposure.Select(c => (IArchitectureContract)c).ToList(),
                "strict_versioned_contract_surface_isolation" => _groups.StrictVersionedContractSurfaceIsolation.Select(c => (IArchitectureContract)c).ToList(),
                "audit_versioned_contract_surface_isolation" => _groups.AuditVersionedContractSurfaceIsolation.Select(c => (IArchitectureContract)c).ToList(),
                "strict_attribute_usage" => _groups.StrictAttributeUsage.Select(c => (IArchitectureContract)c).ToList(),
                "audit_attribute_usage" => _groups.AuditAttributeUsage.Select(c => (IArchitectureContract)c).ToList(),
                "strict_inheritance" => _groups.StrictInheritance.Select(c => (IArchitectureContract)c).ToList(),
                "audit_inheritance" => _groups.AuditInheritance.Select(c => (IArchitectureContract)c).ToList(),
                "strict_interface_implementation" => _groups.StrictInterfaceImplementation.Select(c => (IArchitectureContract)c).ToList(),
                "audit_interface_implementation" => _groups.AuditInterfaceImplementation.Select(c => (IArchitectureContract)c).ToList(),
                "strict_composition" => _groups.StrictComposition.Select(c => (IArchitectureContract)c).ToList(),
                "audit_composition" => _groups.AuditComposition.Select(c => (IArchitectureContract)c).ToList(),
                "strict_coverage" => _groups.StrictCoverage.Select(c => (IArchitectureContract)c).ToList(),
                "audit_coverage" => _groups.AuditCoverage.Select(c => (IArchitectureContract)c).ToList(),
                "strict_metric_budgets" => _groups.StrictMetricBudgets.Select(c => (IArchitectureContract)c).ToList(),
                "audit_metric_budgets" => _groups.AuditMetricBudgets.Select(c => (IArchitectureContract)c).ToList(),
                "strict_context_dependencies" => _groups.StrictContextDependencies.Select(c => (IArchitectureContract)c).ToList(),
                "audit_context_dependencies" => _groups.AuditContextDependencies.Select(c => (IArchitectureContract)c).ToList(),
                "strict_context_allow_only" => _groups.StrictContextAllowOnly.Select(c => (IArchitectureContract)c).ToList(),
                "audit_context_allow_only" => _groups.AuditContextAllowOnly.Select(c => (IArchitectureContract)c).ToList(),
                "strict_port_boundaries" => _groups.StrictPortBoundaries.Select(c => (IArchitectureContract)c).ToList(),
                "audit_port_boundaries" => _groups.AuditPortBoundaries.Select(c => (IArchitectureContract)c).ToList(),
                _ => new List<IArchitectureContract>()
            };
        }

        private static List<ArchitectureIgnoredViolation> GetIgnoredViolations(IArchitectureContract contract)
        {
            return contract switch
            {
                ArchitectureDependencyContract c => c.IgnoredViolations,
                ArchitectureLayerContract c => c.IgnoredViolations,
                ArchitectureAllowOnlyContract c => c.IgnoredViolations,
                ArchitectureCycleContract c => c.IgnoredViolations,
                ArchitectureAcyclicSiblingContract c => c.IgnoredViolations,
                ArchitectureModuleContainerContract c => c.IgnoredViolations,
                ArchitectureMethodBodyContract c => c.IgnoredViolations,
                ArchitectureIndependenceContract c => c.IgnoredViolations,
                ArchitectureAssemblyIndependenceContract c => c.IgnoredViolations,
                ArchitectureAssemblyDependencyContract c => c.IgnoredViolations,
                ArchitectureAssemblyAllowOnlyContract c => c.IgnoredViolations,
                ArchitecturePackageDependencyContract c => c.IgnoredViolations,
                ArchitecturePackageAllowOnlyContract c => c.IgnoredViolations,
                ArchitectureFrameworkReferenceContract c => c.IgnoredViolations,
                ArchitectureFrameworkReferenceAllowOnlyContract c => c.IgnoredViolations,
                ArchitectureProjectMetadataContract c => c.IgnoredViolations,
                ArchitectureProtectedContract c => c.IgnoredViolations,
                ArchitectureExternalDependencyContract c => c.IgnoredViolations,
                ArchitectureExternalAllowOnlyContract c => c.IgnoredViolations,
                ArchitectureTypePlacementContract c => c.IgnoredViolations,
                ArchitectureLayoutConventionContract c => c.IgnoredViolations,
                ArchitecturePublicApiSurfaceContract c => c.IgnoredViolations,
                ArchitectureContractSurfaceExposureContract c => c.IgnoredViolations,
                ArchitectureVersionedContractSurfaceIsolationContract c => c.IgnoredViolations,
                ArchitectureAttributeUsageContract c => c.IgnoredViolations,
                ArchitectureInheritanceContract c => c.IgnoredViolations,
                ArchitectureInterfaceImplementationContract c => c.IgnoredViolations,
                ArchitectureCompositionContract c => c.IgnoredViolations,
                ArchitectureCoverageContract c => c.IgnoredViolations,
                ArchitectureMetricBudgetContract c => c.IgnoredViolations,
                ArchitectureContextDependencyContract c => c.IgnoredViolations,
                ArchitectureContextAllowOnlyContract c => c.IgnoredViolations,
                ArchitecturePortBoundaryContract c => c.IgnoredViolations,
                _ => null!
            };
        }
    }
}
