namespace ArchLinterNet.Core.Contracts;

public interface IArchitectureBaselineLoadingService
{
    void LoadAndMerge(ArchitectureContractDocument document, string baselinePath);
}

public sealed class ArchitectureBaselineLoadingService : IArchitectureBaselineLoadingService
{
    public void LoadAndMerge(ArchitectureContractDocument document, string baselinePath)
    {
        ArchitectureBaselineDocument baseline = ArchitectureBaselineLoader.LoadFromPath(baselinePath);
        ArchitectureBaselineMerger.MergeAndValidate(document, baseline);
    }
}
