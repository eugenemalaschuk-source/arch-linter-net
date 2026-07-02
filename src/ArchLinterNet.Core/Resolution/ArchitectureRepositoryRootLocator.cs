using ArchLinterNet.Core.IO;

namespace ArchLinterNet.Core.Resolution;

public static class ArchitectureRepositoryRootLocator
{
    private static readonly Lazy<string> _root = new(() => ResolveInternal(ArchitectureFileSystem.Real, ArchitectureEnvironment.Real));

    public static string Resolve(IArchitectureFileSystem? fileSystem = null, IArchitectureEnvironment? environment = null)
    {
        return fileSystem == null && environment == null
            ? _root.Value
            : ResolveInternal(fileSystem ?? ArchitectureFileSystem.Real, environment ?? ArchitectureEnvironment.Real);
    }

    public static string ResolveFrom(string policyPath, IArchitectureFileSystem? fileSystem = null)
    {
        fileSystem ??= ArchitectureFileSystem.Real;

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
