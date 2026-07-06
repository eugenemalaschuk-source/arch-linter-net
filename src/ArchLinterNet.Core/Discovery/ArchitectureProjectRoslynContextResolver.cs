using ArchLinterNet.Core.Discovery.Abstractions;
using Buildalyzer;
using Buildalyzer.Environment;
using Buildalyzer.IO;

namespace ArchLinterNet.Core.Discovery;

internal sealed class ArchitectureProjectRoslynContextResolver : IArchitectureProjectRoslynContextResolver
{
    public ArchitectureProjectRoslynResolution Resolve(string projectAbsolutePath)
    {
        if (!File.Exists(projectAbsolutePath))
        {
            return ArchitectureProjectRoslynResolution.Failure(
                $"Project file '{projectAbsolutePath}' does not exist.");
        }

        try
        {
            AnalyzerManager manager = new();
            IProjectAnalyzer? analyzer = manager.GetProject(IOPath.Parse(projectAbsolutePath));

            if (analyzer == null)
            {
                return ArchitectureProjectRoslynResolution.Failure(
                    $"Buildalyzer could not create a project analyzer for '{projectAbsolutePath}'.");
            }

            IAnalyzerResults results = analyzer.Build(new EnvironmentOptions { DesignTime = true, Restore = false });

            IAnalyzerResult? result = results.FirstOrDefault(candidate => candidate.Succeeded);

            if (result == null)
            {
                return ArchitectureProjectRoslynResolution.Failure(
                    $"MSBuild design-time build did not succeed for project '{projectAbsolutePath}'. " +
                    "The project may not have been restored, or its target framework may not be installed.");
            }

            return ArchitectureProjectRoslynResolution.Success(new ArchitectureProjectRoslynContext(
                projectAbsolutePath,
                result.SourceFiles.ToArray(),
                result.References.ToArray()));
        }
        catch (Exception ex)
        {
            return ArchitectureProjectRoslynResolution.Failure(
                $"MSBuild evaluation threw for project '{projectAbsolutePath}': {ex.Message}");
        }
    }
}
