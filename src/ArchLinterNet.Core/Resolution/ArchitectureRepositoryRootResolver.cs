using ArchLinterNet.Core.IO;
using ArchLinterNet.Core.IO.Abstractions;
using ArchLinterNet.Core.Resolution.Abstractions;

namespace ArchLinterNet.Core.Resolution;

public sealed class ArchitectureRepositoryRootResolver : IArchitectureRepositoryRootResolver
{
    private readonly IArchitectureFileSystem _fileSystem;
    private readonly IArchitectureEnvironment _environment;

    public ArchitectureRepositoryRootResolver()
        : this(ArchitectureFileSystem.Real, ArchitectureEnvironment.Real)
    {
    }

    public ArchitectureRepositoryRootResolver(IArchitectureFileSystem fileSystem, IArchitectureEnvironment environment)
    {
        _fileSystem = fileSystem;
        _environment = environment;
    }

    public string Resolve()
    {
        DirectoryInfo? directory = new(_environment.BaseDirectory);

        while (directory != null)
        {
            string candidate = Path.Combine(directory.FullName, "architecture", "dependencies.arch.yml");

            if (_fileSystem.FileExists(candidate))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not resolve repository root for architecture contracts.");
    }

    public string ResolveFrom(string policyPath)
    {
        string fullPath = Path.GetFullPath(policyPath);
        string? policyDir = Path.GetDirectoryName(fullPath);
        if (string.IsNullOrEmpty(policyDir))
        {
            return _fileSystem.GetCurrentDirectory();
        }

        if (string.Equals(Path.GetFileName(policyDir), "architecture", StringComparison.OrdinalIgnoreCase))
        {
            return Path.GetDirectoryName(policyDir) ?? policyDir;
        }

        return policyDir;
    }
}
