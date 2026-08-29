namespace ArchLinterNet.Core.Validation;

public sealed partial class ArchitectureAnalysisSnapshot
{
    private IReadOnlyList<string> GetCacheProjectPaths()
    {
        IReadOnlyList<string> discoveredPaths = GetDiscoveredProjectPaths();
        if (discoveredPaths.Count > 0 || _document.Analysis.Projects.Count == 0)
        {
            return discoveredPaths;
        }

        return _document.Analysis.Projects
            .Select(path => Path.GetFullPath(Path.Combine(_repositoryRoot, path)))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
