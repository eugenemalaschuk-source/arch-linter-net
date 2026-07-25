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
        _isolatedLoadScope = isolatedLoadScope;
    }

    public string RepositoryRoot { get; }

    public IReadOnlyCollection<Assembly> TargetAssemblies { get; }

    public IReadOnlyCollection<string> MissingAssemblyNames { get; }

    public IReadOnlyCollection<string> AssemblyProbingPaths { get; }

    public IReadOnlyCollection<ArchitectureProjectDiscoveryDiagnostic> DiscoveryDiagnostics { get; }

    public ProjectDiscoveryResult? ProjectDiscovery { get; }

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
