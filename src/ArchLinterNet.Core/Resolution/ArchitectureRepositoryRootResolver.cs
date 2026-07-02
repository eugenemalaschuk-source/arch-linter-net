using ArchLinterNet.Core.IO;

namespace ArchLinterNet.Core.Resolution;

public interface IArchitectureRepositoryRootResolver
{
    string ResolveFrom(string policyPath);
}

public sealed class ArchitectureRepositoryRootResolver : IArchitectureRepositoryRootResolver
{
    private readonly IArchitectureFileSystem _fileSystem;

    public ArchitectureRepositoryRootResolver()
        : this(ArchitectureFileSystem.Real)
    {
    }

    public ArchitectureRepositoryRootResolver(IArchitectureFileSystem fileSystem)
    {
        _fileSystem = fileSystem;
    }

    public string ResolveFrom(string policyPath)
    {
        return ArchitectureRepositoryRootLocator.ResolveFrom(policyPath, _fileSystem);
    }
}
