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
    IReadOnlyList<string> MissingAssemblyNames,
    bool IsMetadataReferenceClosureComplete)
{
    public bool HasCompleteRootSelection => MissingAssemblyNames.Count == 0;

    public bool HasCompleteArtifactSelection => HasCompleteRootSelection && IsMetadataReferenceClosureComplete;
}
