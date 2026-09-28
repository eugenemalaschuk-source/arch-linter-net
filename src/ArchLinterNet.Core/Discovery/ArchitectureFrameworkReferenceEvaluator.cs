using System.Text.Json;
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
            using ArchitectureDesignTimeBuildIsolation isolation = ArchitectureDesignTimeBuildIsolation.Create(projectAbsolutePath);
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
            analyzer.SetGlobalProperty("CleanFile", isolation.CleanFileName);

            // A design-time build needs restore outputs even when the project declares no
            // PackageReferences. Restore only when those outputs are absent or unusable: CI runs
            // several independent architecture projections in parallel, and repeated restores of
            // the same project can race while replacing project.assets.json underneath another
            // projection's MSBuild evaluation.
            IAnalyzerResults results = analyzer.Build(new EnvironmentOptions
            {
                DesignTime = true,
                Restore = !HasRestoredTargets(projectAbsolutePath),
            });

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

    internal static bool HasRestoredTargets(string projectAbsolutePath)
    {
        string? projectDirectory = Path.GetDirectoryName(projectAbsolutePath);
        if (projectDirectory == null)
        {
            return false;
        }

        string assetsPath = Path.Combine(projectDirectory, "obj", "project.assets.json");
        if (!File.Exists(assetsPath))
        {
            return false;
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(assetsPath));
            return document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty("targets", out JsonElement targets)
                && targets.ValueKind == JsonValueKind.Object
                && targets.EnumerateObject().Any();
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return false;
        }
    }
}
