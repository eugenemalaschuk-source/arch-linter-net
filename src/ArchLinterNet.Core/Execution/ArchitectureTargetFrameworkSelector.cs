namespace ArchLinterNet.Core.Execution;

// Derives the target framework(s) actually selected for this run's target assemblies from their
// already-resolved build output paths. This selector does not resolve artifacts or inspect the
// filesystem; it only interprets the paths selected by ArchitectureAssemblyResolutionService.
internal static class ArchitectureTargetFrameworkSelector
{
    internal static IReadOnlyCollection<string> Select(
        IReadOnlyDictionary<string, string>? resolvedAssemblyPaths,
        IReadOnlyCollection<string> targetAssemblyNames)
    {
        if (resolvedAssemblyPaths is null || resolvedAssemblyPaths.Count == 0)
        {
            return Array.Empty<string>();
        }

        HashSet<string> frameworks = new(StringComparer.OrdinalIgnoreCase);
        foreach (string name in targetAssemblyNames)
        {
            if (!resolvedAssemblyPaths.TryGetValue(name, out string? path))
            {
                continue;
            }

            string? framework = ExtractTargetFrameworkFromBuildOutputPath(path);
            if (!string.IsNullOrWhiteSpace(framework))
            {
                frameworks.Add(framework);
            }
        }

        return frameworks;
    }

    private static string? ExtractTargetFrameworkFromBuildOutputPath(string assemblyPath)
    {
        string[] segments = assemblyPath.Split(
            new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar },
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        int binIndex = Array.FindLastIndex(
            segments,
            segment => string.Equals(segment, "bin", StringComparison.OrdinalIgnoreCase));
        if (binIndex >= 0 && binIndex + 2 < segments.Length)
        {
            return segments[binIndex + 2];
        }

        string? directory = Path.GetDirectoryName(assemblyPath);
        return string.IsNullOrEmpty(directory) ? null : Path.GetFileName(directory);
    }
}
