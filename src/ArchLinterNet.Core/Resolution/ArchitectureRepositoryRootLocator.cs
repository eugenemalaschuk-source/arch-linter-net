namespace ArchLinterNet.Core.Resolution;

public static class ArchitectureRepositoryRootLocator
{
    private static readonly Lazy<string> _root = new(ResolveInternal);

    public static string Resolve()
    {
        return _root.Value;
    }

    public static string ResolveFrom(string policyPath)
    {
        string fullPath = Path.GetFullPath(policyPath);
        string? policyDir = Path.GetDirectoryName(fullPath);
        if (string.IsNullOrEmpty(policyDir))
        {
            return Directory.GetCurrentDirectory();
        }

        if (string.Equals(Path.GetFileName(policyDir), "architecture", StringComparison.OrdinalIgnoreCase))
        {
            return Path.GetDirectoryName(policyDir) ?? policyDir;
        }

        return policyDir;
    }

    private static string ResolveInternal()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);

        while (directory != null)
        {
            string candidate = Path.Combine(directory.FullName, "architecture", "dependencies.arch.yml");

            if (File.Exists(candidate))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not resolve repository root for architecture contracts.");
    }
}
