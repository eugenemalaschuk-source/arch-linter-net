using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Families;
using ArchLinterNet.Core.Discovery;

namespace ArchLinterNet.Core.Execution;

public interface IArchitectureAssemblyResolutionService
{
    ResolutionResult Resolve(
        ArchitectureContractDocument document,
        string repositoryRoot,
        ProjectDiscoveryResult discovery,
        bool resolveAssemblyOutputs,
        string? mode,
        HashSet<string>? selectedContractIds,
        CancellationToken cancellationToken = default);

    // The post-ensure-built pass must not reuse a same-simple-name assembly from the process.
    ResolutionResult ResolvePostBuild( // NOSONAR: preserves the reviewed public adapter contract; each parameter represents an independently meaningful post-build resolution concern.
        ArchitectureContractDocument document,
        string repositoryRoot,
        ProjectDiscoveryResult discovery,
        bool resolveAssemblyOutputs,
        string? mode,
        HashSet<string>? selectedContractIds,
        IReadOnlyDictionary<string, string>? expectedArtifactContentDigests = null,
        CancellationToken cancellationToken = default);
}
