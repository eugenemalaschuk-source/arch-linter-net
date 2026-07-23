using ArchLinterNet.Core.Discovery.Abstractions;
using Buildalyzer;
using Buildalyzer.Environment;
using Buildalyzer.IO;

namespace ArchLinterNet.Core.Discovery;

// Real MSBuild-driven FrameworkReference discovery, one design-time build per TFM, mirroring
// ArchitectureProjectRoslynContextResolver's use of Buildalyzer. Condition (both ItemGroup-level and
// item-level) and imports (.props/.targets/Directory.Build.props/SDK targets) are handled entirely
// by MSBuild's own evaluation inside Buildalyzer's design-time build - no manual condition-string
// parsing is required or attempted here.
internal sealed class ArchitectureFrameworkReferenceEvaluator : IArchitectureFrameworkReferenceEvaluator
{
    private const string ImplicitlyDefinedMetadataKey = "IsImplicitlyDefined";
    private const string FrameworkReferenceItemName = "FrameworkReference";

    public ArchitectureFrameworkReferenceEvaluationResult Evaluate(string projectAbsolutePath, string configuration)
    {
        if (!File.Exists(projectAbsolutePath))
        {
            return Failure(projectAbsolutePath, null,
                $"Project file '{projectAbsolutePath}' does not exist.");
        }

        try
        {
            AnalyzerManager manager = new();
            IProjectAnalyzer? analyzer = manager.GetProject(IOPath.Parse(projectAbsolutePath));

            if (analyzer == null)
            {
                return Failure(projectAbsolutePath, null,
                    $"Buildalyzer could not create a project analyzer for '{projectAbsolutePath}'.");
            }

            // Matches analysis.configuration (defaulting to "Debug" the same way project discovery's
            // output-path resolution already does) so a policy targeting Release sees Release-only
            // FrameworkReference declarations (e.g. Condition="'$(Configuration)'=='Release'") instead
            // of always evaluating against MSBuild's own Configuration default.
            analyzer.SetGlobalProperty("Configuration", configuration);

            // Restore = true: empirically, a design-time build without a prior restore fails (no
            // project.assets.json) even for a project that declares no PackageReferences at all -
            // MSBuild's SDK resolution itself depends on the restore-generated assets file. This
            // restore is local/offline in practice (implicit SDK packages are already present in the
            // local NuGet cache alongside the installed SDK), so it does not require network access
            // for a project whose dependencies are already restorable from cache.
            IAnalyzerResults results = analyzer.Build(new EnvironmentOptions { DesignTime = true, Restore = true });

            List<IAnalyzerResult> perTfmResults = results.Results
                .Where(result => !string.IsNullOrEmpty(result.TargetFramework))
                .ToList();

            if (perTfmResults.Count == 0)
            {
                return Failure(projectAbsolutePath, null,
                    $"MSBuild design-time build produced no per-target-framework result for project '{projectAbsolutePath}'. " +
                    "The project may not have been restored, or its target framework(s) may not be installed.");
            }

            List<ArchitectureFrameworkReferenceEvaluationFailure> failures = new();
            List<ArchitectureDiscoveredFrameworkReference> references = new();

            foreach (IAnalyzerResult result in perTfmResults)
            {
                if (!result.Succeeded)
                {
                    failures.Add(new ArchitectureFrameworkReferenceEvaluationFailure(
                        projectAbsolutePath,
                        result.TargetFramework,
                        $"MSBuild design-time build did not succeed for target framework '{result.TargetFramework}'. " +
                        "The project may not have been restored, or this target framework may not be installed."));
                    continue;
                }

                if (!result.Items.TryGetValue(FrameworkReferenceItemName, out IProjectItem[]? items))
                {
                    continue;
                }

                foreach (IProjectItem item in items)
                {
                    bool isImplicit = item.Metadata.TryGetValue(ImplicitlyDefinedMetadataKey, out string? value)
                        && string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);

                    references.Add(new ArchitectureDiscoveredFrameworkReference(
                        item.ItemSpec,
                        result.TargetFramework,
                        !isImplicit,
                        projectAbsolutePath));
                }
            }

            return new ArchitectureFrameworkReferenceEvaluationResult(references, failures);
        }
        catch (Exception ex)
        {
            return Failure(projectAbsolutePath, null,
                $"MSBuild evaluation threw for project '{projectAbsolutePath}': {ex.Message}");
        }
    }

    private static ArchitectureFrameworkReferenceEvaluationResult Failure(
        string projectAbsolutePath, string? targetFramework, string reason)
    {
        return new ArchitectureFrameworkReferenceEvaluationResult(
            Array.Empty<ArchitectureDiscoveredFrameworkReference>(),
            new[] { new ArchitectureFrameworkReferenceEvaluationFailure(projectAbsolutePath, targetFramework, reason) });
    }
}
