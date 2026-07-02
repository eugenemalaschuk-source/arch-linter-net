using ArchLinterNet.Core.IO;

namespace ArchLinterNet.Core.Resolution;

public static class ArchitectureRepositoryRootLocator
{
    private static readonly Lazy<string> _root = new(() => ResolveInternal(ArchitectureFileSystem.Real, ArchitectureEnvironment.Real));

    public static string Resolve()
    {
        return _root.Value;
    }

    public static string Resolve(IArchitectureFileSystem fileSystem, IArchitectureEnvironment environment)
    {
        return ResolveInternal(fileSystem, environment);
    }

    public static string ResolveFrom(string policyPath)
    {
        return ResolveFrom(policyPath, ArchitectureFileSystem.Real);
    }

    public static string ResolveFrom(string policyPath, IArchitectureFileSystem fileSystem)
    {
        string fullPath = Path.GetFullPath(policyPath);
        string? policyDir = Path.GetDirectoryName(fullPath);
        if (string.IsNullOrEmpty(policyDir))
        {
            return fileSystem.GetCurrentDirectory();
        }

        if (string.Equals(Path.GetFileName(policyDir), "architecture", StringComparison.OrdinalIgnoreCase))
        {
            return Path.GetDirectoryName(policyDir) ?? policyDir;
        }

        return policyDir;
    }

    private static string ResolveInternal(IArchitectureFileSystem fileSystem, IArchitectureEnvironment environment)
    {
        DirectoryInfo? directory = new(environment.BaseDirectory);

        while (directory != null)
        {
            string candidate = Path.Combine(directory.FullName, "architecture", "dependencies.arch.yml");

            if (fileSystem.FileExists(candidate))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not resolve repository root for architecture contracts.");
    }
}
