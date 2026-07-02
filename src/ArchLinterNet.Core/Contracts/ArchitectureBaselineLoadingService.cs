using ArchLinterNet.Core.IO;

namespace ArchLinterNet.Core.Contracts;

public interface IArchitectureBaselineLoadingService
{
    void LoadAndMerge(ArchitectureContractDocument document, string baselinePath);
}

public sealed class ArchitectureBaselineLoadingService : IArchitectureBaselineLoadingService
{
    private readonly IArchitectureFileSystem _fileSystem;

    public ArchitectureBaselineLoadingService()
        : this(ArchitectureFileSystem.Real)
    {
    }

    public ArchitectureBaselineLoadingService(IArchitectureFileSystem fileSystem)
    {
        _fileSystem = fileSystem;
    }

    public void LoadAndMerge(ArchitectureContractDocument document, string baselinePath)
    {
        ArchitectureBaselineDocument baseline = ArchitectureBaselineLoader.LoadFromPath(baselinePath, _fileSystem);
        ArchitectureBaselineMerger.MergeAndValidate(document, baseline);
    }
}
