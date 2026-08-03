using System.Reflection;
using ArchLinterNet.Core.Discovery;
using ArchLinterNet.Core.IO.Abstractions;
using ArchLinterNet.Core.Model;

namespace ArchLinterNet.Core.Execution;

public sealed class ArchitectureAnalysisContext : IDisposable
{
    private readonly IArchitectureAssemblyLoadScope? _isolatedLoadScope;
    private bool _disposed;

    public ArchitectureAnalysisContext(
        string repositoryRoot,
        IReadOnlyCollection<Assembly> targetAssemblies,
        IReadOnlyCollection<string> missingAssemblyNames,
        IReadOnlyCollection<string> assemblyProbingPaths,
        IReadOnlyCollection<ArchitectureProjectDiscoveryDiagnostic>? discoveryDiagnostics = null,
        ProjectDiscoveryResult? projectDiscovery = null,
        IArchitectureAssemblyLoadScope? isolatedLoadScope = null,
        IReadOnlyCollection<string>? selectedAssemblyArtifactPaths = null)
    {
        if (string.IsNullOrWhiteSpace(repositoryRoot))
        {
            throw new ArgumentException("Repository root must be provided.", nameof(repositoryRoot));
        }

        RepositoryRoot = repositoryRoot;
        TargetAssemblies = targetAssemblies?.Where(assembly => assembly != null).Distinct().ToArray()
                           ?? throw new ArgumentNullException(nameof(targetAssemblies));
        MissingAssemblyNames = missingAssemblyNames ?? Array.Empty<string>();
        AssemblyProbingPaths = assemblyProbingPaths ?? Array.Empty<string>();
        DiscoveryDiagnostics = discoveryDiagnostics ?? Array.Empty<ArchitectureProjectDiscoveryDiagnostic>();
        ProjectDiscovery = projectDiscovery;
        // Expose the canonical project files consumed by discovery without making callers in
        // Core.Validation depend on Core.Discovery's result/model types. Hosts use this inventory
        // to ensure a report target cannot overwrite an input that was read for this analysis.
        DiscoveredProjectPaths = projectDiscovery?.DiscoveredProjects
            .Select(project => Path.GetFullPath(Path.Combine(repositoryRoot, project.Path)))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray()
            ?? Array.Empty<string>();
        _isolatedLoadScope = isolatedLoadScope;
        SelectedAssemblyArtifactPaths = (selectedAssemblyArtifactPaths ?? targetAssemblies
                .Select(SafeAssemblyLocation)
                .Where(static path => path is not null)
                .Cast<string>())
            .Select(Path.GetFullPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public string RepositoryRoot { get; }

    public IReadOnlyCollection<Assembly> TargetAssemblies { get; }

    public IReadOnlyCollection<string> MissingAssemblyNames { get; }

    public IReadOnlyCollection<string> AssemblyProbingPaths { get; }

    public IReadOnlyCollection<ArchitectureProjectDiscoveryDiagnostic> DiscoveryDiagnostics { get; }

    public ProjectDiscoveryResult? ProjectDiscovery { get; }

    public IReadOnlyList<string> DiscoveredProjectPaths { get; }

    // ResolutionResult supplies exact selected paths (including post-build LoadFromStream
    // assemblies). These are physical files, not Assembly.Location-derived guesses.
    internal IReadOnlyList<string> SelectedAssemblyArtifactPaths { get; }

    internal IReadOnlyList<ArchitectureLoadedAssemblyArtifact> LoadedAssemblyArtifacts =>
        (_isolatedLoadScope as IArchitectureAssemblyLoadScopeArtifactInventory)?.LoadedAssemblyArtifacts.ToArray()
        ?? Array.Empty<ArchitectureLoadedAssemblyArtifact>();

    // Cache evidence alone needs the eager local reference closure. Keep it out of ordinary
    // post-build resolution so a cache-disabled run retains the historical lazy-load behavior.
    internal bool MaterializeCacheArtifactReferences(CancellationToken cancellationToken)
    {
        return _isolatedLoadScope is not IArchitectureAssemblyLoadScopeArtifactInventory inventory
            || inventory.MaterializeProbingPathReferences(
                TargetAssemblies,
                maximumAdditionalArtifactCount: 256,
                maximumAdditionalArtifactBytes: 512L * 1024 * 1024,
                cancellationToken: cancellationToken);
    }

    // The session receives this one shared recorder when it creates lazily-materialized indexes.
    // ArchitectureAnalysisSnapshot projects the values through its immutable public counters.
    internal AnalysisSessionProfilingCounters ProfilingCounters { get; } = new();

    // Set once by ArchitectureRunnerSetupService at construction time. Deep type/IL/source scanning
    // and fact-index materialization code (spread across many ArchitectureAnalysisSession partial-
    // class files) reads this to check cancellation at its own natural per-file/per-type loop
    // boundaries, instead of every one of those methods taking a CancellationToken parameter.
    public CancellationToken CancellationToken { get; init; }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _isolatedLoadScope?.Dispose();
    }

    private static string? SafeAssemblyLocation(Assembly assembly)
    {
        try
        {
            return string.IsNullOrEmpty(assembly.Location) ? null : assembly.Location;
        }
        catch (NotSupportedException)
        {
            return null;
        }
    }
}
