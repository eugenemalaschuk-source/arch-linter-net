namespace ArchLinterNet.Core.Resolution;

public static class ArchitectureRepositoryRootLocator
{
    private static readonly Lazy<string> _root = new(ResolveInternal);

    public static string Resolve()
    {
        return _root.Value;
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
