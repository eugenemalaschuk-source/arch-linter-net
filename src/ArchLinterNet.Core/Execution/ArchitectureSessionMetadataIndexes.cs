using System.Reflection;
using ArchLinterNet.Core.Discovery;

namespace ArchLinterNet.Core.Execution;

// Lazy, session-owned projections over immutable analysis inputs. These indexes deliberately do
// not become a second project/assembly authority: they preserve the first retained/discovered
// entry exactly as the former GroupBy(...).First()/FirstOrDefault lookup paths did.
internal sealed class ArchitectureSessionMetadataIndexes
{
    private readonly IReadOnlyCollection<Assembly> _targetAssemblies;
    private readonly IReadOnlyCollection<ArchitectureDiscoveredProject> _discoveredProjects;
    private readonly AnalysisSessionProfilingCounters _profilingCounters;
    private readonly Lazy<IReadOnlyDictionary<string, Assembly>> _assembliesByName;
    private readonly Lazy<ProjectMetadataIndexes> _projectMetadata;

    public ArchitectureSessionMetadataIndexes(ArchitectureAnalysisContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        _targetAssemblies = context.TargetAssemblies;
        _discoveredProjects = context.ProjectDiscovery?.DiscoveredProjects
            ?? Array.Empty<ArchitectureDiscoveredProject>();
        _profilingCounters = context.ProfilingCounters;
        _assembliesByName = new Lazy<IReadOnlyDictionary<string, Assembly>>(BuildAssembliesByName);
        _projectMetadata = new Lazy<ProjectMetadataIndexes>(BuildProjectMetadata);
    }

    public IReadOnlyDictionary<string, Assembly> AssembliesByName => _assembliesByName.Value;

    public bool TryGetAssembly(string assemblyName, out Assembly assembly) =>
        AssembliesByName.TryGetValue(assemblyName, out assembly!);

    public bool TryGetProjectByAssemblyName(string assemblyName, out ArchitectureDiscoveredProject project) =>
        _projectMetadata.Value.ByAssemblyName.TryGetValue(assemblyName, out project!);

    public bool TryGetProjectByNormalizedPath(string normalizedProjectPath, out ArchitectureDiscoveredProject project) =>
        _projectMetadata.Value.ByNormalizedPath.TryGetValue(normalizedProjectPath, out project!);

    public bool TryGetPackageReferences(
        string assemblyName,
        out IReadOnlyList<ArchitectureDiscoveredPackageReference> references) =>
        _projectMetadata.Value.PackageReferencesByAssemblyName.TryGetValue(assemblyName, out references!);

    private Dictionary<string, Assembly> BuildAssembliesByName()
    {
        _profilingCounters.RecordSessionAssemblyIndexMaterialization();
        Dictionary<string, Assembly> assembliesByName = new(StringComparer.Ordinal);

        foreach (Assembly assembly in _targetAssemblies)
        {
            assembliesByName.TryAdd(assembly.GetName().Name ?? string.Empty, assembly);
        }

        return assembliesByName;
    }

    private ProjectMetadataIndexes BuildProjectMetadata()
    {
        _profilingCounters.RecordSessionProjectMetadataIndexMaterialization();
        Dictionary<string, ArchitectureDiscoveredProject> projectsByAssemblyName = new(StringComparer.Ordinal);
        Dictionary<string, ArchitectureDiscoveredProject> projectsByNormalizedPath = new(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, IReadOnlyList<ArchitectureDiscoveredPackageReference>> packageReferencesByAssemblyName =
            new(StringComparer.Ordinal);

        foreach (ArchitectureDiscoveredProject project in _discoveredProjects)
        {
            projectsByAssemblyName.TryAdd(project.AssemblyName, project);
            projectsByNormalizedPath.TryAdd(ProjectPathNormalizer.Normalize(project.Path), project);
            packageReferencesByAssemblyName.TryAdd(project.AssemblyName, project.PackageReferences);
        }

        return new ProjectMetadataIndexes(
            projectsByAssemblyName,
            projectsByNormalizedPath,
            packageReferencesByAssemblyName);
    }

    private sealed record ProjectMetadataIndexes(
        IReadOnlyDictionary<string, ArchitectureDiscoveredProject> ByAssemblyName,
        IReadOnlyDictionary<string, ArchitectureDiscoveredProject> ByNormalizedPath,
        IReadOnlyDictionary<string, IReadOnlyList<ArchitectureDiscoveredPackageReference>> PackageReferencesByAssemblyName);
}
