using System.Reflection;
using ArchLinterNet.Core.Discovery;
using ArchLinterNet.Core.IO.Abstractions;

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
        IArchitectureAssemblyLoadScope? isolatedLoadScope = null)
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
    }

    public string RepositoryRoot { get; }

    public IReadOnlyCollection<Assembly> TargetAssemblies { get; }

    public IReadOnlyCollection<string> MissingAssemblyNames { get; }

    public IReadOnlyCollection<string> AssemblyProbingPaths { get; }

    public IReadOnlyCollection<ArchitectureProjectDiscoveryDiagnostic> DiscoveryDiagnostics { get; }

    public ProjectDiscoveryResult? ProjectDiscovery { get; }

    public IReadOnlyList<string> DiscoveredProjectPaths { get; }

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
}
