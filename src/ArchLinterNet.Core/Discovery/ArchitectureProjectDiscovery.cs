using ArchLinterNet.Core.Contracts;

namespace ArchLinterNet.Core.Discovery;

public static class ArchitectureProjectDiscovery
{
    public static ProjectDiscoveryResult ResolveFromDocument(ArchitectureContractDocument document, string repositoryRoot)
    {
        ArchitectureAnalysisConfiguration analysis = document.Analysis;
        bool hasSolution = !string.IsNullOrWhiteSpace(analysis.Solution);
        bool hasExplicitProjects = analysis.Projects.Count > 0;

        if (!hasSolution && !hasExplicitProjects)
        {
            return ProjectDiscoveryResult.Empty;
        }

        List<ArchitectureProjectDiscoveryDiagnostic> diagnostics = new();
        List<string> projectPaths = new();

        if (hasSolution)
        {
            string solutionPath = ResolvePath(analysis.Solution, repositoryRoot);

            if (!File.Exists(solutionPath))
            {
                diagnostics.Add(new ArchitectureProjectDiscoveryDiagnostic(
                    "missing solution file", analysis.Solution,
                    $"Solution file '{analysis.Solution}' (resolved to '{solutionPath}') does not exist."));
            }
            else
            {
                try
                {
                    IReadOnlyList<string> discovered = ArchitectureSolutionParser.ParseProjectPaths(solutionPath);
                    projectPaths.AddRange(FilterProjects(discovered, repositoryRoot, analysis));
                }
                catch (Exception ex)
                {
                    diagnostics.Add(new ArchitectureProjectDiscoveryDiagnostic(
                        "unparsable solution file", analysis.Solution,
                        $"Solution file '{analysis.Solution}' could not be parsed: {ex.Message}"));
                }
            }
        }

        foreach (string explicitProject in analysis.Projects)
        {
            projectPaths.Add(ResolvePath(explicitProject, repositoryRoot));
        }

        List<string> targetAssemblyNames = new();
        List<string> assemblySearchPaths = new();
        List<string> sourceRoots = new();

        foreach (string projectPath in projectPaths.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!File.Exists(projectPath))
            {
                diagnostics.Add(new ArchitectureProjectDiscoveryDiagnostic(
                    "missing project file", projectPath, $"Project file '{projectPath}' does not exist."));
                continue;
            }

            DiscoveredProjectFile projectFile;
            try
            {
                projectFile = ArchitectureProjectFileParser.Parse(projectPath);
            }
            catch (Exception ex)
            {
                diagnostics.Add(new ArchitectureProjectDiscoveryDiagnostic(
                    "unparsable project file", projectPath, $"Project file '{projectPath}' could not be parsed: {ex.Message}"));
                continue;
            }

            if (projectFile.TargetFrameworks.Count == 0)
            {
                diagnostics.Add(new ArchitectureProjectDiscoveryDiagnostic(
                    "missing target framework", projectPath,
                    $"Project '{projectPath}' declares no <TargetFramework> or <TargetFrameworks>."));
                continue;
            }

            if (!TryResolveOutput(projectFile, analysis, out string outputDirectory, out ArchitectureProjectDiscoveryDiagnostic? diagnostic))
            {
                diagnostics.Add(diagnostic!);
                continue;
            }

            targetAssemblyNames.Add(projectFile.AssemblyName);
            assemblySearchPaths.Add(outputDirectory);
            sourceRoots.Add(GetRelativeDirectory(repositoryRoot, projectPath));
        }

        return new ProjectDiscoveryResult(
            targetAssemblyNames.Distinct(StringComparer.Ordinal).ToArray(),
            assemblySearchPaths.Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
            sourceRoots.Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
            diagnostics);
    }

    private static bool TryResolveOutput(
        DiscoveredProjectFile projectFile,
        ArchitectureAnalysisConfiguration analysis,
        out string outputDirectory,
        out ArchitectureProjectDiscoveryDiagnostic? diagnostic)
    {
        outputDirectory = string.Empty;
        diagnostic = null;
        string projectDirectory = Path.GetDirectoryName(projectFile.AbsolutePath) ?? string.Empty;
        string configuration = string.IsNullOrWhiteSpace(analysis.Configuration) ? "Debug" : analysis.Configuration;

        if (!string.IsNullOrWhiteSpace(analysis.TargetFramework))
        {
            string candidatePath = BuildOutputDirectory(projectDirectory, configuration, analysis.TargetFramework);
            if (File.Exists(Path.Combine(candidatePath, $"{projectFile.AssemblyName}.dll")))
            {
                outputDirectory = candidatePath;
                return true;
            }

            diagnostic = new ArchitectureProjectDiscoveryDiagnostic(
                "missing project build output", projectFile.AbsolutePath,
                $"Project '{projectFile.AbsolutePath}' has no build output for analysis.target_framework '{analysis.TargetFramework}' (checked '{candidatePath}').");
            return false;
        }

        List<(string Framework, string Directory, bool HasOutput)> candidates = projectFile.TargetFrameworks
            .Select(framework =>
            {
                string directory = BuildOutputDirectory(projectDirectory, configuration, framework);
                bool hasOutput = File.Exists(Path.Combine(directory, $"{projectFile.AssemblyName}.dll"));
                return (framework, directory, hasOutput);
            })
            .ToList();

        List<(string Framework, string Directory, bool HasOutput)> withOutput =
            candidates.Where(candidate => candidate.HasOutput).ToList();

        if (withOutput.Count == 1)
        {
            outputDirectory = withOutput[0].Directory;
            return true;
        }

        string checkedList = string.Join(", ", candidates.Select(candidate =>
            $"{candidate.Framework} ({(candidate.HasOutput ? "found" : "missing")})"));

        if (withOutput.Count == 0)
        {
            diagnostic = new ArchitectureProjectDiscoveryDiagnostic(
                "missing project build output", projectFile.AbsolutePath,
                $"Project '{projectFile.AbsolutePath}' has no build output for any candidate target framework. Checked: {checkedList}.");
            return false;
        }

        diagnostic = new ArchitectureProjectDiscoveryDiagnostic(
            "ambiguous project build output", projectFile.AbsolutePath,
            $"Project '{projectFile.AbsolutePath}' is multi-targeted and has build output for more than one target framework. Checked: {checkedList}. Set analysis.target_framework to disambiguate.");
        return false;
    }

    private static string BuildOutputDirectory(string projectDirectory, string configuration, string targetFramework)
    {
        return Path.Combine(projectDirectory, "bin", configuration, targetFramework);
    }

    private static IEnumerable<string> FilterProjects(
        IReadOnlyList<string> discoveredPaths,
        string repositoryRoot,
        ArchitectureAnalysisConfiguration analysis)
    {
        foreach (string path in discoveredPaths)
        {
            string relativePath = GetRelativePath(repositoryRoot, path);

            if (analysis.ProjectInclude.Count > 0 &&
                !analysis.ProjectInclude.Any(pattern => ProjectPathGlob.IsMatch(relativePath, pattern)))
            {
                continue;
            }

            if (analysis.ProjectExclude.Any(pattern => ProjectPathGlob.IsMatch(relativePath, pattern)))
            {
                continue;
            }

            yield return path;
        }
    }

    private static string ResolvePath(string rawPath, string repositoryRoot)
    {
        return Path.IsPathRooted(rawPath) ? rawPath : Path.GetFullPath(Path.Combine(repositoryRoot, rawPath));
    }

    private static string GetRelativePath(string repositoryRoot, string fullPath)
    {
        return Path.GetRelativePath(repositoryRoot, fullPath).Replace('\\', '/');
    }

    private static string GetRelativeDirectory(string repositoryRoot, string projectPath)
    {
        string directory = Path.GetDirectoryName(projectPath) ?? projectPath;
        return GetRelativePath(repositoryRoot, directory);
    }
}
