using ArchLinterNet.Core.Discovery;

namespace ArchLinterNet.Core.BuildState;

// Generates the temporary MSBuild driver used for runtime-specific graph builds. The .NET SDK
// rejects `dotnet build <solution> --runtime <RID>` (NETSDK1134), so the runtime is applied to
// selected project roots through this driver while their ProjectReference closure remains intact.
internal static class BuildStateRuntimeGraphBuildProjectFactory
{
    internal static string WriteTemporaryRuntimeGraphBuildProject(BuildStatePreflightRequest request)
    {
        string path = Path.Combine(Path.GetTempPath(), $"archlinternet-ensure-built-{Guid.NewGuid():N}.proj");
        File.WriteAllText(path, CreateRuntimeGraphBuildProjectContent(request));
        return path;
    }

    // Kept separate from the temporary-file boundary so graph-root selection and MSBuild property
    // handoff can be verified without starting a child process.
    internal static string CreateRuntimeGraphBuildProjectContent(BuildStatePreflightRequest request)
    {
        IReadOnlyCollection<ArchitectureDiscoveredProject> selected =
            BuildStateRuntimeBuildPreparation.SelectRelevantProjectsWithTransitiveReferences(request);
        HashSet<string> selectedPaths = selected.Select(project => project.Path).ToHashSet(StringComparer.Ordinal);
        HashSet<string> referencedPaths = selected
            .SelectMany(project => project.ProjectReferences)
            .Select(reference => reference.Path)
            .Where(selectedPaths.Contains)
            .ToHashSet(StringComparer.Ordinal);
        ArchitectureDiscoveredProject[] roots = selected
            .Where(project => !referencedPaths.Contains(project.Path))
            .ToArray();

        // A cyclic project graph has no roots. Keep it buildable enough to return the existing
        // MSBuild diagnostic rather than silently producing an empty driver project.
        IEnumerable<ArchitectureDiscoveredProject> projects = roots.Length > 0 ? roots : selected;
        IEnumerable<string> projectEntries = projects
            .Select(project => BuildStatePathResolution.ResolveAbsoluteProjectPath(request.RepositoryRoot, project.Path))
            .Distinct(StringComparer.Ordinal)
            .Select(absolutePath => $"    <BuildStateProject Include=\"{System.Security.SecurityElement.Escape(absolutePath)}\" />");

        string restoreProperties = EscapeMsBuildProperties(BuildProjectProperties(request, noRestore: false));
        string buildProperties = EscapeMsBuildProperties(BuildProjectProperties(request, noRestore: true));
        string content = "<Project DefaultTargets=\"Build\">" + Environment.NewLine
            + "  <ItemGroup>" + Environment.NewLine
            + string.Join(Environment.NewLine, projectEntries) + Environment.NewLine
            + "  </ItemGroup>" + Environment.NewLine
            + "  <Target Name=\"Restore\">" + Environment.NewLine
            + $"    <MSBuild Projects=\"@(BuildStateProject)\" Targets=\"Restore\" BuildInParallel=\"false\" Properties=\"{restoreProperties}\" />" + Environment.NewLine
            + "  </Target>" + Environment.NewLine
            + "  <Target Name=\"Build\">" + Environment.NewLine
            + $"    <MSBuild Projects=\"@(BuildStateProject)\" Targets=\"Build\" BuildInParallel=\"false\" Properties=\"{buildProperties}\" />" + Environment.NewLine
            + "  </Target>" + Environment.NewLine
            + "</Project>" + Environment.NewLine;
        return content;
    }

    private static string BuildProjectProperties(BuildStatePreflightRequest request, bool noRestore)
    {
        var properties = new List<string> { "RestoreDisableParallel=true" };
        AddProjectProperty(properties, "Configuration", request.RequestedConfiguration);
        AddProjectProperty(properties, "TargetFramework", request.RequestedTargetFramework);
        AddProjectProperty(properties, "Platform", request.RequestedPlatform);
        AddProjectProperty(properties, "RuntimeIdentifier", request.RequestedRuntimeIdentifier);
        if (noRestore)
        {
            properties.Add("Restore=false");
        }

        return string.Join(';', properties);
    }

    private static void AddProjectProperty(List<string> properties, string name, string? value)
    {
        if (value != null)
        {
            properties.Add($"{name}={value}");
        }
    }

    private static string EscapeMsBuildProperties(string properties) =>
        System.Security.SecurityElement.Escape(properties) ?? string.Empty;
}
