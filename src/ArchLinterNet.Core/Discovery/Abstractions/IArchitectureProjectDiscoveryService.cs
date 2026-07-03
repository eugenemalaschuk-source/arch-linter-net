using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Discovery;

namespace ArchLinterNet.Core.Discovery.Abstractions;

public interface IArchitectureProjectDiscoveryService
{
    ProjectDiscoveryResult ResolveAndApply(
        ArchitectureContractDocument document, string repositoryRoot, bool resolveAssemblyOutputs);
}
