namespace ArchLinterNet.Core.Execution.Abstractions;

// Aggregation sink for family `ConfigurationContributor` delegates. Replaces the mutable locals
// and closures that used to live inline in ArchitectureAnalysisSession.CheckConfiguration so each
// family's contribution logic can live on its own descriptor instead of a central switchboard.
internal sealed class ArchitectureConfigurationReferenceCollector
{
    private readonly Dictionary<string, HashSet<string>> _layerReferencingContractIds = new(StringComparer.Ordinal);
    private readonly HashSet<string> _referencedExternalGroups = new(StringComparer.Ordinal);
    private readonly HashSet<string> _referencedPackageGroups = new(StringComparer.Ordinal);
    private readonly List<(string ContractName, string? ContractId, string Source)> _packageContractSources = new();
    private readonly List<(string ContractName, string? ContractId, string ProjectPath)> _projectMetadataContractProjects = new();

    public IReadOnlyDictionary<string, HashSet<string>> LayerReferencingContractIds => _layerReferencingContractIds;

    public IReadOnlyCollection<string> ReferencedExternalGroups => _referencedExternalGroups;

    public IReadOnlyCollection<string> ReferencedPackageGroups => _referencedPackageGroups;

    public IReadOnlyList<(string ContractName, string? ContractId, string Source)> PackageContractSources => _packageContractSources;

    public IReadOnlyList<(string ContractName, string? ContractId, string ProjectPath)> ProjectMetadataContractProjects => _projectMetadataContractProjects;

    public void AddLayerNames(string? contractId, IEnumerable<string> names)
    {
        foreach (string name in names)
        {
            if (!_layerReferencingContractIds.TryGetValue(name, out HashSet<string>? contractIds))
            {
                contractIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                _layerReferencingContractIds[name] = contractIds;
            }

            if (contractId != null)
            {
                contractIds.Add(contractId);
            }
        }
    }

    public void AddExternalGroupNames(IEnumerable<string> names)
    {
        foreach (string name in names)
        {
            _referencedExternalGroups.Add(name);
        }
    }

    public void AddPackageGroupNames(IEnumerable<string> names)
    {
        foreach (string name in names)
        {
            _referencedPackageGroups.Add(name);
        }
    }

    public void AddPackageContractSource(string contractName, string? contractId, string source)
    {
        _packageContractSources.Add((contractName, contractId, source));
    }

    public void AddProjectMetadataProject(string contractName, string? contractId, string projectPath)
    {
        _projectMetadataContractProjects.Add((contractName, contractId, projectPath));
    }
}
