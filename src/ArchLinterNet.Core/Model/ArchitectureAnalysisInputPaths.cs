namespace ArchLinterNet.Core.Model;

/// <summary>
/// Exact repository inputs consumed while materializing an architecture analysis snapshot.
/// Hosts use this provenance to keep a subsequently published artifact from replacing an input
/// that the same execution trusted.
/// </summary>
internal sealed record ArchitectureAnalysisInputPaths
{
    internal static ArchitectureAnalysisInputPaths Empty { get; } = new();

    internal IReadOnlyList<string> PolicyImportPaths { get; init; } = Array.Empty<string>();

    internal IReadOnlyList<string> ResolvedAssemblyPaths { get; init; } = Array.Empty<string>();

    internal IReadOnlyList<string> DiscoveredProjectPaths { get; init; } = Array.Empty<string>();

    internal IReadOnlyList<string> ConsumedInputPaths { get; init; } = Array.Empty<string>();

    internal static ArchitectureAnalysisInputPaths Create(
        IReadOnlyList<string> policyImportPaths,
        IReadOnlyList<string> resolvedAssemblyPaths,
        IReadOnlyList<string> discoveredProjectPaths,
        IReadOnlyList<string> consumedInputPaths)
    {
        return new ArchitectureAnalysisInputPaths
        {
            PolicyImportPaths = policyImportPaths,
            ResolvedAssemblyPaths = resolvedAssemblyPaths,
            DiscoveredProjectPaths = discoveredProjectPaths,
            ConsumedInputPaths = consumedInputPaths,
        };
    }
}
