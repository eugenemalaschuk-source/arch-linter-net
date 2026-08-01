using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Abstractions;
using ArchLinterNet.Core.Contracts.Families;
using ArchLinterNet.Core.Discovery;
using ArchLinterNet.Core.Discovery.Abstractions;
using ArchLinterNet.Core.Execution.Abstractions;
using ArchLinterNet.Core.Reporting;
using ArchLinterNet.Core.Resolution;
using ArchLinterNet.Core.Resolution.Abstractions;

namespace ArchLinterNet.Core.Execution;

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
        return LoadDocument(policyPath, baselinePath, timing, default);
    }

    public ArchitectureContractDocument LoadDocument(
        string policyPath,
        string? baselinePath = null,
        ValidationTiming? timing = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArchitectureContractDocument document;
        using (timing?.Measure("yaml_loading", indent: 1))
            document = policyDocumentLoader.Load(policyPath, cancellationToken);

        cancellationToken.ThrowIfCancellationRequested();

        if (baselinePath != null)
        {
            using (timing?.Measure("baseline_loading", indent: 1))
                baselineLoadingService.LoadAndMerge(document, baselinePath);

            cancellationToken.ThrowIfCancellationRequested();
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
        string? mode = null,
        CancellationToken cancellationToken = default)
    {
        return BuildRunnerCore(document, policyPath, conditionSetName, preprocessorSymbols, selectedContractIds,
            enableUnmatchedIgnoreTracking, timing, mode, loadPostBuildArtifacts: false, cancellationToken);
    }

    public ArchitectureRunnerSetup BuildRunnerForPostBuild(
        ArchitectureContractDocument document,
        string policyPath,
        string? conditionSetName = null,
        IReadOnlyList<string>? preprocessorSymbols = null,
        HashSet<string>? selectedContractIds = null,
        bool enableUnmatchedIgnoreTracking = true,
        ValidationTiming? timing = null,
        string? mode = null,
        CancellationToken cancellationToken = default)
    {
        return BuildRunnerCore(document, policyPath, conditionSetName, preprocessorSymbols, selectedContractIds,
            enableUnmatchedIgnoreTracking, timing, mode, loadPostBuildArtifacts: true, cancellationToken);
    }

    private ArchitectureRunnerSetup BuildRunnerCore(
        ArchitectureContractDocument document,
        string policyPath,
        string? conditionSetName,
        IReadOnlyList<string>? preprocessorSymbols,
        HashSet<string>? selectedContractIds,
        bool enableUnmatchedIgnoreTracking,
        ValidationTiming? timing,
        string? mode,
        bool loadPostBuildArtifacts,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

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

        cancellationToken.ThrowIfCancellationRequested();

        ArchitectureContractRunner runner;
        using (timing?.Measure("assembly_resolution", indent: 1))
        {
            bool resolveAssemblyOutputs = ShouldResolveAssemblyOutputs(document, mode, selectedContractIds);
            if (loadPostBuildArtifacts)
            {
                // Explicit target_assemblies normally keep discovery from probing project output.
                // After ensure-built, however, the freshly built project outputs are authoritative
                // for this snapshot and must be available to the isolated resolver below.
                resolveAssemblyOutputs = true;
            }
            ProjectDiscoveryResult discovery = projectDiscoveryService.ResolveAndApply(
                document, repositoryRoot, resolveAssemblyOutputs, cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();

            ResolutionResult resolution = loadPostBuildArtifacts
                ? assemblyResolutionService.ResolvePostBuild(
                    document, repositoryRoot, discovery, resolveAssemblyOutputs, mode, selectedContractIds,
                    cancellationToken)
                : assemblyResolutionService.Resolve(
                    document, repositoryRoot, discovery, resolveAssemblyOutputs, mode, selectedContractIds,
                    cancellationToken);

            ProjectDiscoveryResult? attemptedDiscovery = ReferenceEquals(discovery, ProjectDiscoveryResult.Empty)
                ? null
                : discovery;

            ArchitectureAnalysisContext context = CreateAnalysisContext(
                repositoryRoot, resolution, discovery, attemptedDiscovery, cancellationToken);
            runner = CreateRunner(context, document, selectedContractIds, enableUnmatchedIgnoreTracking, symbols);

            return new ArchitectureRunnerSetup(repositoryRoot, runner) { AssemblyLoads = resolution.AssemblyLoads };
        }
    }

    private static bool ShouldResolveAssemblyOutputs(
        ArchitectureContractDocument document,
        string? mode,
        HashSet<string>? selectedContractIds)
    {
        if (document.Analysis.TargetAssemblies.Count > 0)
        {
            return false;
        }

        return !CanRunWithoutResolvedAssemblies(document, mode, selectedContractIds);
    }

    private static bool CanRunWithoutResolvedAssemblies(
        ArchitectureContractDocument document,
        string? mode,
        HashSet<string>? selectedContractIds)
    {
        ArchitectureContractCatalog catalog = ArchitectureContractCatalog.Build(document);
        IEnumerable<IArchitectureContract> relevantContracts = mode != null
            ? catalog.ContractsFor(mode)
            : catalog.ContractsFor("strict").Concat(catalog.ContractsFor("audit"));

        List<IArchitectureContract> selectedContracts = relevantContracts
            .Where(contract => selectedContractIds == null || selectedContractIds.Count == 0
                || (contract.Id != null && selectedContractIds.Contains(contract.Id)))
            .ToList();

        return selectedContracts.Count > 0
            && selectedContracts.All(static contract => contract is ArchitectureProjectMetadataContract);
    }

    private static ArchitectureAnalysisContext CreateAnalysisContext(
        string repositoryRoot,
        ResolutionResult resolution,
        ProjectDiscoveryResult discovery,
        ProjectDiscoveryResult? attemptedDiscovery,
        CancellationToken cancellationToken)
    {
        return new ArchitectureAnalysisContext(repositoryRoot, resolution.ResolvedAssemblies,
            resolution.MissingAssemblyNames, resolution.AssemblyProbingPaths, discovery.Diagnostics, attemptedDiscovery,
            resolution.IsolatedLoadScope)
        {
            CancellationToken = cancellationToken
        };
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
