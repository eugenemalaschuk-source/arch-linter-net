using ArchLinterNet.Core.Contracts;

namespace ArchLinterNet.Core.Execution.Abstractions;

// Aggregation sink for family `ConfigurationContributor` delegates. Replaces the mutable locals
// and closures that used to live inline in ArchitectureAnalysisSession.CheckConfiguration so each
// family's contribution logic can live on its own descriptor instead of a central switchboard.
internal sealed class ArchitectureConfigurationReferenceCollector
{
    private readonly Dictionary<string, List<IArchitectureContract>> _layerReferencingContracts =
        new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<IArchitectureContract>> _referencedExternalGroups =
        new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<IArchitectureContract>> _referencedPackageGroups =
        new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<IArchitectureContract>> _referencedFrameworkGroups =
        new(StringComparer.Ordinal);
    private readonly List<(IArchitectureContract Contract, string Source)> _packageContractSources = new();
    private readonly List<(IArchitectureContract Contract, string Source)> _frameworkContractSources = new();
    private readonly List<(IArchitectureContract Contract, string ProjectPath)> _projectMetadataContractProjects = new();

    public IReadOnlyDictionary<string, List<IArchitectureContract>> LayerReferencingContracts =>
        _layerReferencingContracts;

    public IReadOnlyDictionary<string, List<IArchitectureContract>> ReferencedExternalGroups =>
        _referencedExternalGroups;

    public IReadOnlyDictionary<string, List<IArchitectureContract>> ReferencedPackageGroups =>
        _referencedPackageGroups;

    public IReadOnlyDictionary<string, List<IArchitectureContract>> ReferencedFrameworkGroups =>
        _referencedFrameworkGroups;

    public IReadOnlyList<(IArchitectureContract Contract, string Source)> PackageContractSources =>
        _packageContractSources;

    public IReadOnlyList<(IArchitectureContract Contract, string Source)> FrameworkContractSources =>
        _frameworkContractSources;

    public IReadOnlyList<(IArchitectureContract Contract, string ProjectPath)> ProjectMetadataContractProjects =>
        _projectMetadataContractProjects;

    public void AddLayerNames(IArchitectureContract contract, IEnumerable<string> names)
    {
        foreach (string name in names)
        {
            AddReference(_layerReferencingContracts, name, contract);
        }
    }

    public void AddExternalGroupNames(IArchitectureContract contract, IEnumerable<string> names)
    {
        foreach (string name in names)
        {
            AddReference(_referencedExternalGroups, name, contract);
        }
    }

    public void AddPackageGroupNames(IArchitectureContract contract, IEnumerable<string> names)
    {
        foreach (string name in names)
        {
            AddReference(_referencedPackageGroups, name, contract);
        }
    }

    public void AddPackageContractSource(IArchitectureContract contract, string source)
    {
        _packageContractSources.Add((contract, source));
    }

    public void AddFrameworkGroupNames(IArchitectureContract contract, IEnumerable<string> names)
    {
        foreach (string name in names)
        {
            AddReference(_referencedFrameworkGroups, name, contract);
        }
    }

    public void AddFrameworkContractSource(IArchitectureContract contract, string source)
    {
        _frameworkContractSources.Add((contract, source));
    }

    public void AddProjectMetadataProject(IArchitectureContract contract, string projectPath)
    {
        _projectMetadataContractProjects.Add((contract, projectPath));
    }

    private static void AddReference(
        Dictionary<string, List<IArchitectureContract>> references,
        string name,
        IArchitectureContract contract)
    {
        if (!references.TryGetValue(name, out List<IArchitectureContract>? contracts))
        {
            contracts = new List<IArchitectureContract>();
            references[name] = contracts;
        }

        if (!contracts.Any(candidate => ReferenceEquals(candidate, contract)))
        {
            contracts.Add(contract);
        }
    }
}
