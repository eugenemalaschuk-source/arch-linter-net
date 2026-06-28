using ArchLinterNet.Core.Contracts;

namespace ArchLinterNet.Core.Discovery;

public static class ArchitectureProjectDiscovery
{
    public static ProjectDiscoveryResult ResolveFromDocument(
        ArchitectureContractDocument document,
        string repositoryRoot,
        bool resolveAssemblyOutputs = true)
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
                    List<string> filtered = FilterProjects(discovered, repositoryRoot, analysis).ToList();

                    if (filtered.Count == 0)
                    {
                        diagnostics.Add(new ArchitectureProjectDiscoveryDiagnostic(
                            "no C# projects discovered", analysis.Solution,
                            $"Solution file '{analysis.Solution}' was parsed successfully but no .csproj entries were discovered (after project_include/project_exclude filtering, if configured)."));
                    }

                    projectPaths.AddRange(filtered);
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
        List<ArchitectureDiscoveredProject> discoveredProjects = new();

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

            // A project is a valid source root as soon as it parses, regardless of whether its
            // build output can be (or needs to be) resolved for assembly seeding.
            sourceRoots.Add(GetRelativeDirectory(repositoryRoot, projectPath));
            discoveredProjects.Add(new ArchitectureDiscoveredProject(
                GetRelativePath(repositoryRoot, projectPath), projectFile.AssemblyName, projectFile.TargetFrameworks));

            if (!resolveAssemblyOutputs)
            {
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
        }

        return new ProjectDiscoveryResult(
            targetAssemblyNames.Distinct(StringComparer.Ordinal).ToArray(),
            assemblySearchPaths.Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
            sourceRoots.Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
            diagnostics)
        {
            DiscoveredProjects = discoveredProjects
        };
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
            if (!File.Exists(Path.Combine(candidatePath, $"{projectFile.AssemblyName}.dll")))
            {
                diagnostic = new ArchitectureProjectDiscoveryDiagnostic(
                    "missing project build output", projectFile.AbsolutePath,
                    $"Project '{projectFile.AbsolutePath}' has no build output for analysis.target_framework '{analysis.TargetFramework}' (checked '{candidatePath}').");
                return false;
            }

            return FinishResolve(projectFile, projectDirectory, candidatePath, out outputDirectory, out diagnostic);
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
            return FinishResolve(projectFile, projectDirectory, withOutput[0].Directory, out outputDirectory, out diagnostic);
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

    private static bool FinishResolve(
        DiscoveredProjectFile projectFile,
        string projectDirectory,
        string candidateOutputDirectory,
        out string outputDirectory,
        out ArchitectureProjectDiscoveryDiagnostic? diagnostic)
    {
        outputDirectory = string.Empty;
        diagnostic = null;
        string dllPath = Path.Combine(candidateOutputDirectory, $"{projectFile.AssemblyName}.dll");
        DateTime dllWriteTimeUtc = File.GetLastWriteTimeUtc(dllPath);
        DateTime latestSourceWriteTimeUtc = GetLatestSourceWriteTimeUtc(projectDirectory, projectFile.AbsolutePath);

        if (dllWriteTimeUtc < latestSourceWriteTimeUtc)
        {
            diagnostic = new ArchitectureProjectDiscoveryDiagnostic(
                "stale project build output", projectFile.AbsolutePath,
                $"Project '{projectFile.AbsolutePath}' build output '{dllPath}' (last built {dllWriteTimeUtc:O}) is older than its sources (last changed {latestSourceWriteTimeUtc:O}). Rebuild the project before validating.");
            return false;
        }

        outputDirectory = candidateOutputDirectory;
        return true;
    }

    private static DateTime GetLatestSourceWriteTimeUtc(string projectDirectory, string projectFilePath)
    {
        DateTime latest = File.GetLastWriteTimeUtc(projectFilePath);

        if (!Directory.Exists(projectDirectory))
        {
            return latest;
        }

        foreach (string file in Directory.EnumerateFiles(projectDirectory, "*.cs", SearchOption.AllDirectories))
        {
            string relativePath = Path.GetRelativePath(projectDirectory, file);
            string[] segments = relativePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            if (segments.Any(segment =>
                    string.Equals(segment, "bin", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(segment, "obj", StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            DateTime writeTime = File.GetLastWriteTimeUtc(file);
            if (writeTime > latest)
            {
                latest = writeTime;
            }
        }

        return latest;
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
