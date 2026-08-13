using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Families;
using ArchLinterNet.Core.Discovery;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Resolution;
using ArchLinterNet.Core.Scanning;

namespace ArchLinterNet.Core.Execution.Checkers;

internal static class MethodBodyChecker
{
    public static List<ArchitectureViolation> Check(
        ArchitectureMethodBodyContract contract,
        ArchitectureCheckerContext context,
        ArchitectureContractExecutionContext executionContext)
    {
        ArchitectureLayer sourceLayer =
            ArchitectureLayerResolver.ResolveLayer(context.Document, contract.Name, contract.Source);

        string[]? sourceRoots = context.Document.Analysis.SourceRoots.Count > 0
            ? context.Document.Analysis.SourceRoots.ToArray()
            : null;

        (IReadOnlyList<string>? explicitReferenceAssemblyPaths, string? sourceAssemblyHint, ArchitectureViolation? fallbackDiagnostic) =
            ResolveProjectAwareReferenceAssemblyPaths(contract, context, sourceLayer, sourceRoots);

        IReadOnlyList<ArchitectureViolation> roslynViolations = new ArchitectureSourceScanner()
            .FindMethodBodyViolations(context.AnalysisContext.RepositoryRoot, sourceLayer.Namespace,
                contract.ForbiddenCalls, executionContext, sourceRoots: sourceRoots,
                sourceLayer: sourceLayer, preprocessorSymbols: context.PreprocessorSymbols,
                explicitReferenceAssemblyPaths: explicitReferenceAssemblyPaths,
                sourceAssemblyHint: sourceAssemblyHint, cancellationToken: context.AnalysisContext.CancellationToken)
            .ToList();

        IReadOnlyList<ArchitectureViolation> ilViolations = new ArchitectureIlMethodBodyScanner().FindMethodBodyViolations(
            context.AnalysisContext.TargetAssemblies,
            sourceLayer.Namespace,
            contract.ForbiddenCalls,
            executionContext,
            sourceLayer: sourceLayer,
            cancellationToken: context.AnalysisContext.CancellationToken)
            .ToList();

        List<ArchitectureViolation> violations = ArchitectureNamespaceViolationFinder.MergeMethodBodyViolations(
            contract.Name, contract.Id, roslynViolations, ilViolations);

        if (fallbackDiagnostic != null)
        {
            violations.Add(fallbackDiagnostic);
        }

        return violations;
    }

    // Attempts project-aware reference resolution for a method-body contract's owning discovered
    // project. Returns (null, null) when project discovery isn't configured at all, so behavior for
    // repositories that never opted into analysis.solution/analysis.projects is completely
    // unchanged. Returns a non-null diagnostic only when discovery IS configured but project-aware
    // resolution couldn't be used (no/ambiguous owning project, or Buildalyzer evaluation failed),
    // so the degraded-accuracy fallback is visible rather than silent.
    private static (IReadOnlyList<string>? ReferenceAssemblyPaths, string? SourceAssemblyName, ArchitectureViolation? FallbackDiagnostic)
        ResolveProjectAwareReferenceAssemblyPaths(
            ArchitectureMethodBodyContract contract,
            ArchitectureCheckerContext context,
            ArchitectureLayer sourceLayer,
            string[]? sourceRoots)
    {
        ProjectDiscoveryResult? discovery = context.AnalysisContext.ProjectDiscovery;
        if (discovery == null || discovery.DiscoveredProjects.Count == 0)
        {
            return (null, null, null);
        }

        context.AnalysisContext.CancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<string> matchedFiles = ArchitectureSourceScanner.FindMatchingSourceFiles(
            context.AnalysisContext.RepositoryRoot, sourceLayer, sourceRoots, fileSystem: null,
            context.AnalysisContext.CancellationToken);

        if (matchedFiles.Count == 0)
        {
            return (null, null, null);
        }

        ArchitectureDiscoveredProject? owningProject =
            ResolveOwningProject(context, discovery.DiscoveredProjects, matchedFiles);

        if (owningProject == null)
        {
            return (null, null, BuildFallbackDiagnostic(contract,
                "no single discovered project owns this contract's source files (files span zero or multiple discovered project directories)"));
        }

        string projectAbsolutePath = Path.GetFullPath(
            Path.Combine(context.AnalysisContext.RepositoryRoot, owningProject.Path));

        // The MSBuild design-time build inside Resolve is one opaque Buildalyzer call — like the
        // Roslyn compilation build in ArchitectureSourceScanner, not individually interruptible —
        // so it is checked immediately before and after instead of not at all.
        context.AnalysisContext.CancellationToken.ThrowIfCancellationRequested();
        ArchitectureProjectRoslynResolution resolution = new ArchitectureProjectRoslynContextResolver()
            .Resolve(projectAbsolutePath, context.AnalysisContext.CancellationToken);
        context.AnalysisContext.CancellationToken.ThrowIfCancellationRequested();

        if (!resolution.Succeeded)
        {
            return (null, owningProject.AssemblyName, BuildFallbackDiagnostic(contract,
                $"project '{owningProject.Path}' could not be evaluated for project-aware Roslyn analysis: {resolution.FailureReason}"));
        }

        return (resolution.Context!.ReferenceAssemblyPaths, owningProject.AssemblyName, null);
    }

    private static ArchitectureViolation BuildFallbackDiagnostic(ArchitectureMethodBodyContract contract, string reason)
    {
        return new ArchitectureViolation(
            contract.Name,
            contract.Id,
            contract.Source,
            "project-aware analysis fallback",
            new[]
            {
                $"Method-body contract '{contract.Name}' fell back to lightweight Roslyn compilation because {reason}. " +
                "Cross-project/package symbol resolution may be less accurate for this check."
            });
    }

    // A discovered project "owns" a matched source file when that project's directory is the
    // nearest (longest-prefix) ancestor directory among all discovered projects. Project-aware
    // resolution is only attempted when every matched file resolves to exactly the same owning
    // project — spanning zero or multiple projects falls back rather than guessing.
    private static ArchitectureDiscoveredProject? ResolveOwningProject(
        ArchitectureCheckerContext context,
        IReadOnlyCollection<ArchitectureDiscoveredProject> discoveredProjects,
        IReadOnlyList<string> matchedFiles)
    {
        // Materializing project directories is real per-project work (full-path resolution and
        // directory normalization), so cancellation is checked per project here — not only at the
        // prepass's surrounding boundaries.
        List<(ArchitectureDiscoveredProject Project, string Directory)> projectDirectories = new(discoveredProjects.Count);
        foreach (ArchitectureDiscoveredProject project in discoveredProjects)
        {
            context.AnalysisContext.CancellationToken.ThrowIfCancellationRequested();
            projectDirectories.Add((project, NormalizeDirectory(Path.GetFullPath(Path.Combine(
                context.AnalysisContext.RepositoryRoot, Path.GetDirectoryName(project.Path) ?? string.Empty)))));
        }

        HashSet<string> owningProjectPaths = new(StringComparer.OrdinalIgnoreCase);
        ArchitectureDiscoveredProject? owner = null;

        foreach (string filePath in matchedFiles)
        {
            // Per-file check: cancellation stops project-aware scanning at the nearest file
            // boundary instead of only after every file has been matched against every project.
            context.AnalysisContext.CancellationToken.ThrowIfCancellationRequested();
            string fileDirectory = NormalizeDirectory(Path.GetDirectoryName(Path.GetFullPath(filePath)) ?? string.Empty);

            ArchitectureDiscoveredProject? bestMatch = null;
            int bestLength = -1;

            foreach ((ArchitectureDiscoveredProject candidate, string candidateDirectory) in projectDirectories)
            {
                // Per-candidate-project check: the longest-prefix scan is a matchedFiles ×
                // discoveredProjects product, so a large graph must stop at the next candidate
                // boundary too, not only between files.
                context.AnalysisContext.CancellationToken.ThrowIfCancellationRequested();
                if (fileDirectory.StartsWith(candidateDirectory, StringComparison.OrdinalIgnoreCase)
                    && candidateDirectory.Length > bestLength)
                {
                    bestMatch = candidate;
                    bestLength = candidateDirectory.Length;
                }
            }

            if (bestMatch == null)
            {
                return null;
            }

            owningProjectPaths.Add(bestMatch.Path);
            owner = bestMatch;
        }

        return owningProjectPaths.Count == 1 ? owner : null;
    }

    private static string NormalizeDirectory(string path)
    {
        return path.Replace('\\', '/').TrimEnd('/') + "/";
    }
}
