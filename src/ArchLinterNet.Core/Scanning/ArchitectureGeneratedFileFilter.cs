namespace ArchLinterNet.Core.Scanning;

internal static class ArchitectureGeneratedFileFilter
{
    private static readonly string[] _excludedDirectorySegments = ["bin", "obj", "Library", "Temp", "PackageCache"];

    private static readonly string[] _excludedFilenameSuffixes = [".g.cs", ".g.i.cs", ".designer.cs"];

    public static bool IsExcluded(string path)
    {
        string normalized = path.Replace('\\', '/');
        string[] segments = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);

        if (segments.Any(segment => _excludedDirectorySegments.Contains(segment, StringComparer.OrdinalIgnoreCase)))
        {
            return true;
        }

        string fileName = segments.Length > 0 ? segments[^1] : normalized;
        return _excludedFilenameSuffixes.Any(suffix => fileName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase));
    }
}
