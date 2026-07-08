using ArchLinterNet.Core.Contracts.Abstractions;
using ArchLinterNet.Core.IO;
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
        ArchitectureBaselineDocument baseline = LoadFromPath(baselinePath);
        MergeAndValidate(document, baseline);
    }

    public ArchitectureBaselineDocument Load(string baselinePath)
    {
        return LoadFromPath(baselinePath);
    }

    internal ArchitectureBaselineDocument LoadFromPath(string baselinePath)
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

        ValidateBaseline(document);
        return document;
    }

    private static void ValidateBaseline(ArchitectureBaselineDocument document)
    {
        if (document.Version != 1)
        {
            throw new InvalidOperationException(
                $"Unsupported baseline version: {document.Version}. Only version 1 is supported.");
        }

        ValidateGroupEntries(document.Baseline.Strict, "strict");
        ValidateGroupEntries(document.Baseline.Audit, "audit");
        ValidateGroupEntries(document.Baseline.StrictLayers, "strict_layers");
        ValidateGroupEntries(document.Baseline.AuditLayers, "audit_layers");
        ValidateGroupEntries(document.Baseline.StrictAllowOnly, "strict_allow_only");
        ValidateGroupEntries(document.Baseline.AuditAllowOnly, "audit_allow_only");
        ValidateGroupEntries(document.Baseline.StrictCycles, "strict_cycles");
        ValidateGroupEntries(document.Baseline.AuditCycles, "audit_cycles");
        ValidateGroupEntries(document.Baseline.StrictAcyclicSiblings, "strict_acyclic_siblings");
        ValidateGroupEntries(document.Baseline.AuditAcyclicSiblings, "audit_acyclic_siblings");
        ValidateGroupEntries(document.Baseline.StrictMethodBody, "strict_method_body");
        ValidateGroupEntries(document.Baseline.AuditMethodBody, "audit_method_body");
        ValidateGroupEntries(document.Baseline.StrictIndependence, "strict_independence");
        ValidateGroupEntries(document.Baseline.AuditIndependence, "audit_independence");
        ValidateGroupEntries(document.Baseline.StrictProtected, "strict_protected");
        ValidateGroupEntries(document.Baseline.AuditProtected, "audit_protected");
        ValidateGroupEntries(document.Baseline.StrictExternal, "strict_external");
        ValidateGroupEntries(document.Baseline.AuditExternal, "audit_external");
        ValidateGroupEntries(document.Baseline.StrictProjectMetadata, "strict_project_metadata");
        ValidateGroupEntries(document.Baseline.AuditProjectMetadata, "audit_project_metadata");
        ValidateGroupEntries(document.Baseline.StrictCoverage, "strict_coverage");
        ValidateGroupEntries(document.Baseline.AuditCoverage, "audit_coverage");
    }

    private static void ValidateGroupEntries(List<ArchitectureBaselineContractEntry> entries, string groupName)
    {
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
            }
        }
    }

    internal void Merge(ArchitectureContractDocument policyDocument, ArchitectureBaselineDocument baselineDocument)
    {
        var groupMerger = new ContractGroupMerger(policyDocument.Contracts);
        var baseline = baselineDocument.Baseline;

        groupMerger.MergeGroup(baseline.Strict, "strict");
        groupMerger.MergeGroup(baseline.Audit, "audit");
        groupMerger.MergeGroup(baseline.StrictLayers, "strict_layers");
        groupMerger.MergeGroup(baseline.AuditLayers, "audit_layers");
        groupMerger.MergeGroup(baseline.StrictAllowOnly, "strict_allow_only");
        groupMerger.MergeGroup(baseline.AuditAllowOnly, "audit_allow_only");
        groupMerger.MergeGroup(baseline.StrictCycles, "strict_cycles");
        groupMerger.MergeGroup(baseline.AuditCycles, "audit_cycles");
        groupMerger.MergeGroup(baseline.StrictAcyclicSiblings, "strict_acyclic_siblings");
        groupMerger.MergeGroup(baseline.AuditAcyclicSiblings, "audit_acyclic_siblings");
        groupMerger.MergeGroup(baseline.StrictMethodBody, "strict_method_body");
        groupMerger.MergeGroup(baseline.AuditMethodBody, "audit_method_body");
        groupMerger.MergeGroup(baseline.StrictIndependence, "strict_independence");
        groupMerger.MergeGroup(baseline.AuditIndependence, "audit_independence");
        groupMerger.MergeGroup(baseline.StrictProtected, "strict_protected");
        groupMerger.MergeGroup(baseline.AuditProtected, "audit_protected");
        groupMerger.MergeGroup(baseline.StrictExternal, "strict_external");
        groupMerger.MergeGroup(baseline.AuditExternal, "audit_external");
        groupMerger.MergeGroup(baseline.StrictProjectMetadata, "strict_project_metadata");
        groupMerger.MergeGroup(baseline.AuditProjectMetadata, "audit_project_metadata");
        groupMerger.MergeGroup(baseline.StrictCoverage, "strict_coverage");
        groupMerger.MergeGroup(baseline.AuditCoverage, "audit_coverage");
    }

    internal void MergeAndValidate(
        ArchitectureContractDocument policyDocument,
        ArchitectureBaselineDocument baselineDocument)
    {
        var unknownIds = new List<(string GroupName, string ContractId)>();
        var groupMerger = new ContractGroupMerger(policyDocument.Contracts);
        var baseline = baselineDocument.Baseline;

        unknownIds.AddRange(groupMerger.MergeGroup(baseline.Strict, "strict"));
        unknownIds.AddRange(groupMerger.MergeGroup(baseline.Audit, "audit"));
        unknownIds.AddRange(groupMerger.MergeGroup(baseline.StrictLayers, "strict_layers"));
        unknownIds.AddRange(groupMerger.MergeGroup(baseline.AuditLayers, "audit_layers"));
        unknownIds.AddRange(groupMerger.MergeGroup(baseline.StrictAllowOnly, "strict_allow_only"));
        unknownIds.AddRange(groupMerger.MergeGroup(baseline.AuditAllowOnly, "audit_allow_only"));
        unknownIds.AddRange(groupMerger.MergeGroup(baseline.StrictCycles, "strict_cycles"));
        unknownIds.AddRange(groupMerger.MergeGroup(baseline.AuditCycles, "audit_cycles"));
        unknownIds.AddRange(groupMerger.MergeGroup(baseline.StrictAcyclicSiblings, "strict_acyclic_siblings"));
        unknownIds.AddRange(groupMerger.MergeGroup(baseline.AuditAcyclicSiblings, "audit_acyclic_siblings"));
        unknownIds.AddRange(groupMerger.MergeGroup(baseline.StrictMethodBody, "strict_method_body"));
        unknownIds.AddRange(groupMerger.MergeGroup(baseline.AuditMethodBody, "audit_method_body"));
        unknownIds.AddRange(groupMerger.MergeGroup(baseline.StrictIndependence, "strict_independence"));
        unknownIds.AddRange(groupMerger.MergeGroup(baseline.AuditIndependence, "audit_independence"));
        unknownIds.AddRange(groupMerger.MergeGroup(baseline.StrictProtected, "strict_protected"));
        unknownIds.AddRange(groupMerger.MergeGroup(baseline.AuditProtected, "audit_protected"));
        unknownIds.AddRange(groupMerger.MergeGroup(baseline.StrictExternal, "strict_external"));
        unknownIds.AddRange(groupMerger.MergeGroup(baseline.AuditExternal, "audit_external"));
        unknownIds.AddRange(groupMerger.MergeGroup(baseline.StrictProjectMetadata, "strict_project_metadata"));
        unknownIds.AddRange(groupMerger.MergeGroup(baseline.AuditProjectMetadata, "audit_project_metadata"));
        unknownIds.AddRange(groupMerger.MergeGroup(baseline.StrictCoverage, "strict_coverage"));
        unknownIds.AddRange(groupMerger.MergeGroup(baseline.AuditCoverage, "audit_coverage"));

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
        private readonly ArchitectureContractGroups _groups;

        public ContractGroupMerger(ArchitectureContractGroups groups)
        {
            _groups = groups;
        }

        public List<(string GroupName, string ContractId)> MergeGroup(
            List<ArchitectureBaselineContractEntry> baselineEntries,
            string groupName)
        {
            var unknownIds = new List<(string GroupName, string ContractId)>();
            var contracts = GetContracts(groupName);

            foreach (var baselineEntry in baselineEntries)
            {
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
                    bool isDuplicate = ignores.Any(existing =>
                        string.Equals(existing.SourceType, baselineIgnore.SourceType, StringComparison.Ordinal) &&
                        string.Equals(existing.ForbiddenReference, baselineIgnore.ForbiddenReference, StringComparison.Ordinal));

                    if (!isDuplicate)
                    {
                        ignores.Add(new ArchitectureIgnoredViolation
                        {
                            SourceType = baselineIgnore.SourceType,
                            ForbiddenReference = baselineIgnore.ForbiddenReference,
                            Reason = baselineIgnore.Reason
                        });
                    }
                }
            }

            return unknownIds;
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
                "strict_method_body" => _groups.StrictMethodBody.Select(c => (IArchitectureContract)c).ToList(),
                "audit_method_body" => _groups.AuditMethodBody.Select(c => (IArchitectureContract)c).ToList(),
                "strict_independence" => _groups.StrictIndependence.Select(c => (IArchitectureContract)c).ToList(),
                "audit_independence" => _groups.AuditIndependence.Select(c => (IArchitectureContract)c).ToList(),
                "strict_protected" => _groups.StrictProtected.Select(c => (IArchitectureContract)c).ToList(),
                "audit_protected" => _groups.AuditProtected.Select(c => (IArchitectureContract)c).ToList(),
                "strict_external" => _groups.StrictExternal.Select(c => (IArchitectureContract)c).ToList(),
                "audit_external" => _groups.AuditExternal.Select(c => (IArchitectureContract)c).ToList(),
                "strict_project_metadata" => _groups.StrictProjectMetadata.Select(c => (IArchitectureContract)c).ToList(),
                "audit_project_metadata" => _groups.AuditProjectMetadata.Select(c => (IArchitectureContract)c).ToList(),
                "strict_coverage" => _groups.StrictCoverage.Select(c => (IArchitectureContract)c).ToList(),
                "audit_coverage" => _groups.AuditCoverage.Select(c => (IArchitectureContract)c).ToList(),
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
                ArchitectureMethodBodyContract c => c.IgnoredViolations,
                ArchitectureIndependenceContract c => c.IgnoredViolations,
                ArchitectureProtectedContract c => c.IgnoredViolations,
                ArchitectureExternalDependencyContract c => c.IgnoredViolations,
                ArchitectureProjectMetadataContract c => c.IgnoredViolations,
                ArchitectureCoverageContract c => c.IgnoredViolations,
                _ => null!
            };
        }
    }
}
