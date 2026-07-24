namespace ArchLinterNet.Core.BuildState;

// ArchitectureDiscoveredProject.Path and ArchitectureDiscoveredProjectReference.Path are both
// repository-relative (see ArchitectureProjectDiscoveryService.BuildDiscoveredProject, which
// rewrites the project-file-parser's absolute paths to repo-relative ones before returning
// discovery results). Every place in BuildState that needs a real filesystem path must resolve
// against the request's repository root through this helper — resolving via Path.GetFullPath
// alone is silently wrong whenever the process's current working directory differs from the
// repository root.
internal static class BuildStatePathResolution
{
    public static string ResolveAbsoluteProjectPath(string repositoryRoot, string projectPath)
    {
        return Path.IsPathRooted(projectPath)
            ? Path.GetFullPath(projectPath)
            : Path.GetFullPath(Path.Combine(repositoryRoot, projectPath));
    }
}
