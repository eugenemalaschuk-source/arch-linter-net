namespace ArchLinterNet.Core.Model;

// Wraps a failure that occurs after policy composition but before an outcome is available — either
// while creating/evaluating an ArchitectureAnalysisSnapshot. It carries the exact inputs already
// consumed at that point so hosts cannot overwrite one with a --report error document.
public sealed class ArchitectureAnalysisEvaluationException : InvalidOperationException
{
    public ArchitectureAnalysisEvaluationException(
        string message,
        Exception innerException,
        IReadOnlyList<string> policyImportPaths,
        IReadOnlyList<string> resolvedAssemblyPaths,
        IReadOnlyList<string>? discoveredProjectPaths = null)
        : base(message, innerException)
    {
        PolicyImportPaths = policyImportPaths;
        ResolvedAssemblyPaths = resolvedAssemblyPaths;
        DiscoveredProjectPaths = discoveredProjectPaths ?? Array.Empty<string>();
    }

    public IReadOnlyList<string> PolicyImportPaths { get; }

    public IReadOnlyList<string> ResolvedAssemblyPaths { get; }

    public IReadOnlyList<string> DiscoveredProjectPaths { get; }
}
