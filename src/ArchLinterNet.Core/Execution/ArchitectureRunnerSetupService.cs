using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Discovery;
using ArchLinterNet.Core.Reporting;
using ArchLinterNet.Core.Resolution;

namespace ArchLinterNet.Core.Execution;

public sealed record ArchitectureRunnerSetup(string RepositoryRoot, ArchitectureContractRunner Runner);

public interface IArchitectureRunnerSetupService
{
    ArchitectureContractDocument LoadDocument(
        string policyPath,
        string? baselinePath = null,
        ValidationTiming? timing = null);

    ArchitectureRunnerSetup BuildRunner(
        ArchitectureContractDocument document,
        string policyPath,
        string? conditionSetName = null,
        IReadOnlyList<string>? preprocessorSymbols = null,
        HashSet<string>? selectedContractIds = null,
        bool enableUnmatchedIgnoreTracking = true,
        ValidationTiming? timing = null,
        string? mode = null);
}

public sealed class ArchitectureRunnerSetupService(
    IArchitecturePolicyDocumentLoader policyDocumentLoader,
    IArchitectureBaselineLoadingService baselineLoadingService,
    IArchitectureRepositoryRootResolver repositoryRootResolver,
    IConditionSetResolutionService conditionSetResolutionService,
    IArchitectureProjectDiscoveryService projectDiscoveryService,
    IArchitectureAssemblyResolutionService assemblyResolutionService) : IArchitectureRunnerSetupService
{
    public ArchitectureContractDocument LoadDocument(
        string policyPath,
        string? baselinePath = null,
        ValidationTiming? timing = null)
    {
        ArchitectureContractDocument document;
        using (timing?.Measure("yaml_loading", indent: 1))
            document = policyDocumentLoader.Load(policyPath);

        if (baselinePath != null)
        {
            using (timing?.Measure("baseline_loading", indent: 1))
                baselineLoadingService.LoadAndMerge(document, baselinePath);
        }

        return document;
    }

    public ArchitectureRunnerSetup BuildRunner(
        ArchitectureContractDocument document,
        string policyPath,
        string? conditionSetName = null,
        IReadOnlyList<string>? preprocessorSymbols = null,
        HashSet<string>? selectedContractIds = null,
        bool enableUnmatchedIgnoreTracking = true,
        ValidationTiming? timing = null,
        string? mode = null)
    {
        string repositoryRoot;
        using (timing?.Measure("root_resolution", indent: 1))
            repositoryRoot = repositoryRootResolver.ResolveFrom(policyPath);

        IReadOnlyList<string>? symbols = preprocessorSymbols;
        using (timing?.Measure("condition_set_resolution", indent: 1))
        {
            if (symbols == null &&
                !conditionSetResolutionService.TryResolve(document, conditionSetName, out symbols, out string? resolveError))
            {
                throw new InvalidOperationException(resolveError);
            }
        }

        ArchitectureContractRunner runner;
        using (timing?.Measure("assembly_resolution", indent: 1))
        {
            bool resolveAssemblyOutputs = document.Analysis.TargetAssemblies.Count == 0;
            ProjectDiscoveryResult discovery = projectDiscoveryService.ResolveAndApply(
                document, repositoryRoot, resolveAssemblyOutputs);

            ResolutionResult resolution = assemblyResolutionService.Resolve(
                document, repositoryRoot, discovery, resolveAssemblyOutputs, mode, selectedContractIds);

            ProjectDiscoveryResult? attemptedDiscovery = ReferenceEquals(discovery, ProjectDiscoveryResult.Empty)
                ? null
                : discovery;

            ArchitectureAnalysisContext context = CreateAnalysisContext(repositoryRoot, resolution, discovery, attemptedDiscovery);
            runner = CreateRunner(context, document, selectedContractIds, enableUnmatchedIgnoreTracking, symbols);
        }

        return new ArchitectureRunnerSetup(repositoryRoot, runner);
    }

    private static ArchitectureAnalysisContext CreateAnalysisContext(
        string repositoryRoot,
        ResolutionResult resolution,
        ProjectDiscoveryResult discovery,
        ProjectDiscoveryResult? attemptedDiscovery)
    {
        return new ArchitectureAnalysisContext(repositoryRoot, resolution.ResolvedAssemblies,
            resolution.MissingAssemblyNames, resolution.AssemblyProbingPaths, discovery.Diagnostics, attemptedDiscovery);
    }

    private static ArchitectureContractRunner CreateRunner(
        ArchitectureAnalysisContext context,
        ArchitectureContractDocument document,
        HashSet<string>? selectedContractIds,
        bool enableUnmatchedIgnoreTracking,
        IReadOnlyList<string>? preprocessorSymbols)
    {
        return new ArchitectureContractRunner(context, document, selectedContractIds,
            enableUnmatchedIgnoreTracking, preprocessorSymbols: preprocessorSymbols);
    }
}
