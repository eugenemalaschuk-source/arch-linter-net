using ArchLinterNet.Core.Discovery;

namespace ArchLinterNet.Core.Execution;

// Immutable metadata-only input to runner materialization. It deliberately carries paths and
// project discovery facts rather than Assembly instances, so a cache lookup can authorize the
// current run without creating an AssemblyLoadContext.
public sealed record ArchitectureRunnerPreparation(
    string RepositoryRoot,
    IReadOnlyList<string>? PreprocessorSymbols,
    ProjectDiscoveryResult ProjectDiscovery,
    bool ResolveAssemblyOutputs,
    IReadOnlyList<string> SelectedAssemblyArtifactPaths,
    IReadOnlyDictionary<string, string> CapturedArtifactContentDigests,
    IReadOnlyList<string> MissingAssemblyNames,
    bool IsMetadataReferenceClosureComplete)
{
    // When the policy selects a project/solution graph without authored target_assemblies,
    // discovery owns the root identities even if an output is stale and cannot be scanned yet.
    // Keep that metadata separate from the loadable artifact closure so --ensure-built can build
    // those roots without making every discovered project relevant to an explicit target list.
    internal IReadOnlyList<string> GraphDrivenRootAssemblyNames { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> PreparedProjectPaths => ProjectDiscovery.DiscoveredProjects
        .Select(project => Path.GetFullPath(Path.Combine(RepositoryRoot, project.Path)))
        .ToArray();

    public bool HasCompleteRootSelection => MissingAssemblyNames.Count == 0;

    public bool HasCompleteArtifactSelection => HasCompleteRootSelection && IsMetadataReferenceClosureComplete;
}
