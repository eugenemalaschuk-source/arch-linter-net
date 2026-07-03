using ArchLinterNet.Core.Contracts;

namespace ArchLinterNet.Core.Contracts.Abstractions;

public interface IArchitectureBaselineLoadingService
{
    void LoadAndMerge(ArchitectureContractDocument document, string baselinePath);
}
