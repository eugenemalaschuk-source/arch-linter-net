namespace ArchLinterNet.Core.Execution;

public sealed partial class ArchitectureAssemblyResolutionService
{
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
