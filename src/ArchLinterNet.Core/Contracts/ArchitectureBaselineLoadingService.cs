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

        foreach (string groupName in ArchitectureBaselineContractGroups.GroupNames)
        {
            ValidateGroupEntries(document.Baseline.GetGroup(groupName), groupName);
        }
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
        foreach (string groupName in ArchitectureBaselineContractGroups.GroupNames)
        {
            groupMerger.MergeGroup(baselineDocument.Baseline.GetGroup(groupName), groupName);
        }
    }

    internal void MergeAndValidate(
        ArchitectureContractDocument policyDocument,
        ArchitectureBaselineDocument baselineDocument)
    {
        var unknownIds = new List<(string GroupName, string ContractId)>();
        var groupMerger = new ContractGroupMerger(policyDocument.Contracts);

        foreach (string groupName in ArchitectureBaselineContractGroups.GroupNames)
        {
            unknownIds.AddRange(groupMerger.MergeGroup(baselineDocument.Baseline.GetGroup(groupName), groupName));
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
                "strict_public_api_surface" => _groups.StrictPublicApiSurface.Select(c => (IArchitectureContract)c).ToList(),
                "audit_public_api_surface" => _groups.AuditPublicApiSurface.Select(c => (IArchitectureContract)c).ToList(),
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
                ArchitectureAssemblyIndependenceContract c => c.IgnoredViolations,
                ArchitectureAssemblyDependencyContract c => c.IgnoredViolations,
                ArchitectureAssemblyAllowOnlyContract c => c.IgnoredViolations,
                ArchitecturePackageDependencyContract c => c.IgnoredViolations,
                ArchitecturePackageAllowOnlyContract c => c.IgnoredViolations,
                ArchitectureProjectMetadataContract c => c.IgnoredViolations,
                ArchitectureProtectedContract c => c.IgnoredViolations,
                ArchitectureExternalDependencyContract c => c.IgnoredViolations,
                ArchitectureExternalAllowOnlyContract c => c.IgnoredViolations,
                ArchitectureTypePlacementContract c => c.IgnoredViolations,
                ArchitecturePublicApiSurfaceContract c => c.IgnoredViolations,
                ArchitectureAttributeUsageContract c => c.IgnoredViolations,
                ArchitectureInheritanceContract c => c.IgnoredViolations,
                ArchitectureInterfaceImplementationContract c => c.IgnoredViolations,
                ArchitectureCompositionContract c => c.IgnoredViolations,
                ArchitectureCoverageContract c => c.IgnoredViolations,
                _ => null!
            };
        }
    }
}
