using System.Reflection;

namespace ArchLinterNet.Core.Execution;

public sealed class ArchitectureAnalysisContext
{
    public ArchitectureAnalysisContext(
        string repositoryRoot,
        IReadOnlyCollection<Assembly> targetAssemblies,
        IReadOnlyCollection<string> missingAssemblyNames)
    {
        if (string.IsNullOrWhiteSpace(repositoryRoot))
        {
            throw new ArgumentException("Repository root must be provided.", nameof(repositoryRoot));
        }

        RepositoryRoot = repositoryRoot;
        TargetAssemblies = targetAssemblies?.Where(assembly => assembly != null).Distinct().ToArray()
                           ?? throw new ArgumentNullException(nameof(targetAssemblies));
        MissingAssemblyNames = missingAssemblyNames ?? Array.Empty<string>();
    }

    public string RepositoryRoot { get; }

    public IReadOnlyCollection<Assembly> TargetAssemblies { get; }

    public IReadOnlyCollection<string> MissingAssemblyNames { get; }
}
