using System.Reflection;
using ArchLinterNet.Core.Discovery;

namespace ArchLinterNet.Core.Execution;

// Lazy, session-owned projections over immutable analysis inputs. Legacy project metadata keeps
// its simple-name lookup for existing validation consumers; metrics use the separate exact
// artifact binding below so same-name outputs cannot be chosen by discovery order.
internal sealed class ArchitectureSessionMetadataIndexes
{
    private readonly ArchitectureAnalysisContext _context;
    private readonly IReadOnlyCollection<Assembly> _targetAssemblies;
    private readonly IReadOnlyCollection<ArchitectureDiscoveredProject> _discoveredProjects;
    private readonly AnalysisSessionProfilingCounters _profilingCounters;
    private readonly Lazy<IReadOnlyDictionary<string, Assembly>> _assembliesByName;
    private readonly Lazy<ProjectMetadataIndexes> _projectMetadata;

    public ArchitectureSessionMetadataIndexes(ArchitectureAnalysisContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        _context = context;
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

    public bool TryGetProjectByResolvedAssembly(Assembly assembly, out ArchitectureDiscoveredProject project) =>
        _projectMetadata.Value.ByResolvedAssembly.TryGetValue(assembly, out project!);

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
        Dictionary<string, List<ArchitectureDiscoveredProject>> projectsByArtifactPath =
            new(StringComparer.OrdinalIgnoreCase);

        foreach (ArchitectureDiscoveredProject project in _discoveredProjects)
        {
            projectsByAssemblyName.TryAdd(project.AssemblyName, project);
            string normalizedProjectPath = ProjectPathNormalizer.Normalize(project.Path);
            projectsByNormalizedPath.TryAdd(normalizedProjectPath, project);
            packageReferencesByAssemblyName.TryAdd(project.AssemblyName, project.PackageReferences);

            if (_context.ProjectDiscovery?.ResolvedAssemblyPathsByNormalizedProjectPath
                    .TryGetValue(normalizedProjectPath, out string? artifactPath) == true)
            {
                string normalizedArtifactPath = Path.GetFullPath(artifactPath);
                if (!projectsByArtifactPath.TryGetValue(normalizedArtifactPath, out List<ArchitectureDiscoveredProject>? projects))
                {
                    projects = new List<ArchitectureDiscoveredProject>();
                    projectsByArtifactPath.Add(normalizedArtifactPath, projects);
                }

                projects.Add(project);
            }
        }

        Dictionary<Assembly, ArchitectureDiscoveredProject> projectsByResolvedAssembly = new();
        foreach (Assembly assembly in _targetAssemblies)
        {
            if (!_context.TryGetResolvedAssemblyArtifactPath(assembly, out string artifactPath)
                || !projectsByArtifactPath.TryGetValue(artifactPath, out List<ArchitectureDiscoveredProject>? candidates)
                || candidates.Count != 1)
            {
                continue;
            }

            projectsByResolvedAssembly.TryAdd(assembly, candidates[0]);
        }

        return new ProjectMetadataIndexes(
            projectsByAssemblyName,
            projectsByNormalizedPath,
            packageReferencesByAssemblyName,
            projectsByResolvedAssembly);
    }

    private sealed record ProjectMetadataIndexes(
        IReadOnlyDictionary<string, ArchitectureDiscoveredProject> ByAssemblyName,
        IReadOnlyDictionary<string, ArchitectureDiscoveredProject> ByNormalizedPath,
        IReadOnlyDictionary<string, IReadOnlyList<ArchitectureDiscoveredPackageReference>> PackageReferencesByAssemblyName,
        IReadOnlyDictionary<Assembly, ArchitectureDiscoveredProject> ByResolvedAssembly);
}
