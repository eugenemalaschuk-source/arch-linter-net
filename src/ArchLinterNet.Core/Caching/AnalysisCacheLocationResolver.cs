namespace ArchLinterNet.Core.Caching;

// Resolves --cache/WithCache() options to a safety-validated on-disk location. Never invoked with
// policy/fragment/baseline/snapshot/receipt/cache content — the location is caller-supplied
// configuration only. See openspec/specs/analysis-cache/spec.md, "Cache location defaults are
// opt-in and never authored by content" and "Cache location resolution rejects unsafe paths".
public static class AnalysisCacheLocationResolver
{
    private const string ProductName = "ArchLinterNet";

    public static AnalysisCacheLocation? Resolve(AnalysisCacheOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return options.Mode switch
        {
            AnalysisCacheMode.Disabled => null,
            AnalysisCacheMode.Auto => new AnalysisCacheLocation(ResolveAutoRoot(), AnalysisCacheMode.Auto),
            AnalysisCacheMode.ExplicitPath =>
                new AnalysisCacheLocation(ResolveExplicitRoot(options.ExplicitPath), AnalysisCacheMode.ExplicitPath),
            _ => throw new ArgumentOutOfRangeException(nameof(options), options.Mode, "Unknown cache mode."),
        };
    }

    // Platform user-cache namespace: %LOCALAPPDATA%\ArchLinterNet\0.5.1\analysis-cache\v1 on
    // Windows; $XDG_CACHE_HOME (or ~/.cache) /ArchLinterNet/0.5.1/analysis-cache/v1 elsewhere.
    private static string ResolveAutoRoot()
    {
        string baseDirectory = OperatingSystem.IsWindows()
            ? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
            : ResolveXdgCacheHome();

        return Path.Combine(baseDirectory, ProductName, AnalysisCacheEnvelope.ProductSchemaVersion, "analysis-cache", "v1");
    }

    private static string ResolveXdgCacheHome()
    {
        string? xdg = Environment.GetEnvironmentVariable("XDG_CACHE_HOME");
        if (!string.IsNullOrWhiteSpace(xdg))
        {
            return xdg;
        }

        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(home, ".cache");
    }

    private static string ResolveExplicitRoot(string? explicitPath)
    {
        if (string.IsNullOrWhiteSpace(explicitPath))
        {
            throw new AnalysisCacheLocationRejectedException("--cache path must not be empty.");
        }

        string full = Path.GetFullPath(explicitPath);

        if (IsFileSystemRoot(full))
        {
            throw new AnalysisCacheLocationRejectedException(
                $"--cache path '{explicitPath}' resolves to a filesystem root and is rejected.");
        }

        if (File.Exists(full))
        {
            throw new AnalysisCacheLocationRejectedException(
                $"--cache path '{explicitPath}' is an existing file, not a directory.");
        }

        if (Directory.Exists(full))
        {
            DirectoryInfo info = new(full);
            if (info.LinkTarget != null || (info.Attributes & FileAttributes.ReparsePoint) != 0)
            {
                throw new AnalysisCacheLocationRejectedException(
                    $"--cache path '{explicitPath}' is a symlink/reparse point and is rejected.");
            }
        }

        return full;
    }

    private static bool IsFileSystemRoot(string fullPath)
    {
        string? root = Path.GetPathRoot(fullPath);
        if (string.IsNullOrEmpty(root))
        {
            return false;
        }

        return string.Equals(
            root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            StringComparison.OrdinalIgnoreCase);
    }
}
