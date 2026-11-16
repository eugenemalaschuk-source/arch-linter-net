using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Families;
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

        ArchitectureMethodBodyPreparation preparation = context.MethodBodyPreparationCache.Prepare(
            context.AnalysisContext.RepositoryRoot,
            sourceLayer,
            sourceRoots,
            context.PreprocessorSymbols,
            context.AnalysisContext.ProjectDiscovery,
            context.ResolvedBuildConfiguration,
            context.AnalysisContext.CancellationToken,
            context.BenchmarkWorkObserver);

        IReadOnlyList<ArchitectureViolation> roslynViolations = ArchitectureSourceScanner.FindMethodBodyViolationsWithObserver(
            new ArchitectureMethodBodyScanRequest
            {
                RepositoryRoot = context.AnalysisContext.RepositoryRoot,
                SourceNamespacePrefix = sourceLayer.Namespace,
                ForbiddenCallPatterns = contract.ForbiddenCalls,
                ExecutionContext = executionContext,
                SourceRoots = sourceRoots,
                SourceLayer = sourceLayer,
                PreprocessorSymbols = context.PreprocessorSymbols,
                PreparedSourceFilePaths = preparation.SourceFilePaths,
                PreparedCompilation = preparation.Compilation,
                SourceAssemblyHint = preparation.SourceAssemblyHint,
                CancellationToken = context.AnalysisContext.CancellationToken,
                BenchmarkWorkObserver = context.BenchmarkWorkObserver,
            })
            .ToList();

        IReadOnlyList<ArchitectureViolation> ilViolations = new ArchitectureIlMethodBodyScanner().FindMethodBodyViolations(
            context.AnalysisContext.TargetAssemblies,
            sourceLayer.Namespace,
            contract.ForbiddenCalls,
            executionContext,
            sourceLayer: sourceLayer,
            cancellationToken: context.AnalysisContext.CancellationToken,
            benchmarkWorkObserver: context.BenchmarkWorkObserver)
            .ToList();

        List<ArchitectureViolation> violations = ArchitectureNamespaceViolationFinder.MergeMethodBodyViolations(
            contract.Name, contract.Id, roslynViolations, ilViolations);

        if (preparation.FallbackReason is { } fallbackReason)
        {
            violations.Add(BuildFallbackDiagnostic(contract, fallbackReason));
        }

        return violations;
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

}
