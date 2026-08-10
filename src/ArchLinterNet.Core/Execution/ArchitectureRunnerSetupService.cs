using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using ArchLinterNet.Core.BuildState;
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
    // Public-API workflows capture the exact project output that build-state receipts verify.
    // This internal runner mode lets their ordinary (fresh-process) reads include discovered
    // project output paths even when analysis.target_assemblies is authored explicitly.
    internal const string PublicApiResolutionMode = "public-api";

    public ArchitectureContractDocument LoadDocument(
        string policyPath,
        string? baselinePath = null,
        ValidationTiming? timing = null)
    {
        return LoadDocument(policyPath, baselinePath, timing, default);
    }

    public ArchitectureContractDocument LoadDocument(
        string policyPath,
        string? baselinePath,
        ValidationTiming? timing,
        CancellationToken cancellationToken)
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
        CancellationToken cancellationToken = default,
        int? maxParallelism = null)
    {
        return BuildRunnerCore(document, policyPath, conditionSetName, preprocessorSymbols, selectedContractIds,
            enableUnmatchedIgnoreTracking, timing, mode, loadPostBuildArtifacts: false, cancellationToken, maxParallelism);
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
        CancellationToken cancellationToken = default,
        int? maxParallelism = null)
    {
        return BuildRunnerCore(document, policyPath, conditionSetName, preprocessorSymbols, selectedContractIds,
            enableUnmatchedIgnoreTracking, timing, mode, loadPostBuildArtifacts: true, cancellationToken, maxParallelism);
    }

    public ArchitectureRunnerPreparation PrepareRunner(
        ArchitectureContractDocument document,
        string policyPath,
        string? conditionSetName = null,
        IReadOnlyList<string>? preprocessorSymbols = null,
        HashSet<string>? selectedContractIds = null,
        string? mode = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string repositoryRoot = repositoryRootResolver.ResolveFrom(policyPath);
        IReadOnlyList<string>? symbols = ResolveSymbols(document, conditionSetName, preprocessorSymbols);
        // Preparation must independently select the current project's output artifacts even when
        // an authored target_assemblies list would let ordinary execution use probing. This is
        // metadata-only and does not alter normal cache-disabled resolution precedence.
        bool resolveAssemblyOutputs = true;
        ProjectDiscoveryResult discovery = projectDiscoveryService.ResolveAndApply(
            document, repositoryRoot, resolveAssemblyOutputs, cancellationToken);
        ArchitectureSourceSetExpander.BindProjectSets(document, discovery);

        IReadOnlyList<string> targetNames = document.Analysis.TargetAssemblies
            .Where(static name => !string.IsNullOrWhiteSpace(name))
            .Select(static name => name.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        List<string> selectedPaths = new();
        List<string> missing = new();
        foreach (string name in targetNames)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (discovery.ResolvedAssemblyPaths.TryGetValue(name, out string? path) && File.Exists(path))
            {
                selectedPaths.Add(Path.GetFullPath(path));
            }
            else
            {
                // An ambient/default-context match would require CLR loading and is not valid
                // cache authorization evidence. Treat it as incomplete instead.
                missing.Add(name);
            }
        }

        (IReadOnlyList<string> closure, bool closureComplete) = BuildMetadataReferenceClosure(
            selectedPaths, discovery, cancellationToken);
        IReadOnlyDictionary<string, string> capturedDigests = CaptureArtifactDigests(closure, cancellationToken);
        return new ArchitectureRunnerPreparation(
            repositoryRoot, symbols, discovery, resolveAssemblyOutputs,
            closure, capturedDigests, missing, closureComplete);
    }

    public ArchitectureRunnerSetup MaterializePreparedRunner(
        ArchitectureContractDocument document,
        ArchitectureRunnerPreparation preparation,
        HashSet<string>? selectedContractIds = null,
        bool enableUnmatchedIgnoreTracking = true,
        ValidationTiming? timing = null,
        string? mode = null,
        CancellationToken cancellationToken = default,
        int? maxParallelism = null)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!preparation.HasCompleteRootSelection)
        {
            throw new InvalidOperationException("Prepared artifact selection is incomplete and cannot be materialized.");
        }

        VerifyPreparedArtifacts(preparation, cancellationToken);

        ResolutionResult resolution;
        using (timing?.Measure("assembly_resolution", indent: 1))
        {
            resolution = assemblyResolutionService.ResolvePostBuild(
                document, preparation.RepositoryRoot, preparation.ProjectDiscovery,
                preparation.ResolveAssemblyOutputs, mode, selectedContractIds, cancellationToken,
                preparation.CapturedArtifactContentDigests);
        }

        ArchitectureAnalysisContext context = CreateAnalysisContext(
            preparation.RepositoryRoot, resolution, preparation.ProjectDiscovery,
            ReferenceEquals(preparation.ProjectDiscovery, ProjectDiscoveryResult.Empty) ? null : preparation.ProjectDiscovery,
            cancellationToken, maxParallelism);
        ArchitectureContractRunner runner = CreateRunner(
            context, document, selectedContractIds, enableUnmatchedIgnoreTracking, preparation.PreprocessorSymbols);
        return new ArchitectureRunnerSetup(preparation.RepositoryRoot, runner) { AssemblyLoads = resolution.AssemblyLoads };
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
        CancellationToken cancellationToken,
        int? maxParallelism)
    {
        cancellationToken.ThrowIfCancellationRequested();

        string repositoryRoot;
        using (timing?.Measure("root_resolution", indent: 1))
            repositoryRoot = repositoryRootResolver.ResolveFrom(policyPath);

        IReadOnlyList<string>? symbols;
        using (timing?.Measure("condition_set_resolution", indent: 1))
            symbols = ResolveSymbols(document, conditionSetName, preprocessorSymbols);

        cancellationToken.ThrowIfCancellationRequested();

        ArchitectureContractRunner runner;
        using (timing?.Measure("assembly_resolution", indent: 1))
        {
            bool resolveAssemblyOutputs = string.Equals(mode, PublicApiResolutionMode, StringComparison.Ordinal)
                || ShouldResolveAssemblyOutputs(document, mode, selectedContractIds);
            if (loadPostBuildArtifacts)
            {
                // Explicit target_assemblies normally keep discovery from probing project output.
                // After ensure-built, however, the freshly built project outputs are authoritative
                // for this snapshot and must be available to the isolated resolver below.
                resolveAssemblyOutputs = true;
            }
            ProjectDiscoveryResult discovery = projectDiscoveryService.ResolveAndApply(
                document, repositoryRoot, resolveAssemblyOutputs, cancellationToken);
            ArchitectureSourceSetExpander.BindProjectSets(document, discovery);

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
                repositoryRoot, resolution, discovery, attemptedDiscovery, cancellationToken, maxParallelism);
            runner = CreateRunner(context, document, selectedContractIds, enableUnmatchedIgnoreTracking, symbols);

            return new ArchitectureRunnerSetup(repositoryRoot, runner) { AssemblyLoads = resolution.AssemblyLoads };
        }
    }

    private IReadOnlyList<string>? ResolveSymbols(
        ArchitectureContractDocument document, string? conditionSetName, IReadOnlyList<string>? preprocessorSymbols)
    {
        if (preprocessorSymbols != null)
        {
            return preprocessorSymbols;
        }

        if (!conditionSetResolutionService.TryResolve(document, conditionSetName, out IReadOnlyList<string>? symbols,
                out string? resolveError))
        {
            throw new InvalidOperationException(resolveError);
        }

        return symbols;
    }

    private static (IReadOnlyList<string> Paths, bool Complete) BuildMetadataReferenceClosure(
        IReadOnlyCollection<string> roots, ProjectDiscoveryResult discovery, CancellationToken cancellationToken)
    {
        Dictionary<string, string> candidates = new(StringComparer.OrdinalIgnoreCase);
        foreach (string path in roots.Concat(discovery.ResolvedAssemblyPaths.Values))
        {
            if (File.Exists(path))
            {
                candidates[Path.GetFileNameWithoutExtension(path)] = Path.GetFullPath(path);
            }
        }

        if (AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") is string trustedPlatformAssemblies)
        {
            foreach (string path in trustedPlatformAssemblies.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
            {
                if (File.Exists(path))
                {
                    candidates.TryAdd(Path.GetFileNameWithoutExtension(path), Path.GetFullPath(path));
                }
            }
        }

        Queue<string> pending = new(roots.Select(Path.GetFullPath));
        HashSet<string> closure = new(StringComparer.OrdinalIgnoreCase);
        // A project-only metadata contract has no exact PE/PDB root inventory. Do not make it
        // reusable merely because its reference walk is vacuously empty.
        bool complete = roots.Count > 0;
        while (pending.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string path = pending.Dequeue();
            if (!closure.Add(path))
            {
                continue;
            }

            try
            {
                using FileStream stream = File.OpenRead(path);
                using PEReader reader = new(stream, PEStreamOptions.LeaveOpen);
                if (!reader.HasMetadata)
                {
                    complete = false;
                    continue;
                }

                MetadataReader metadata = reader.GetMetadataReader();
                foreach (AssemblyReferenceHandle handle in metadata.AssemblyReferences)
                {
                    string name = metadata.GetString(metadata.GetAssemblyReference(handle).Name);
                    if (string.IsNullOrWhiteSpace(name) || !candidates.TryGetValue(name, out string? referencePath))
                    {
                        complete = false;
                        continue;
                    }

                    pending.Enqueue(referencePath);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or BadImageFormatException)
            {
                complete = false;
            }
        }

        return (closure.OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToArray(), complete);
    }

    private static IReadOnlyDictionary<string, string> CaptureArtifactDigests(
        IReadOnlyList<string> artifactPaths,
        CancellationToken cancellationToken)
    {
        Dictionary<string, string> digests = new(StringComparer.OrdinalIgnoreCase);
        foreach (string artifactPath in artifactPaths)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Add(artifactPath);
            Add(Path.ChangeExtension(artifactPath, ".pdb"));
            Add(BuildReceiptStore.ReceiptPathFor(artifactPath));
        }

        return digests;

        void Add(string path)
        {
            string fullPath = Path.GetFullPath(path);
            digests[fullPath] = File.Exists(fullPath)
                ? BuildStateCanonicalHasher.ComputeContentDigest(fullPath, cancellationToken)
                : "missing";
        }
    }

    private static void VerifyPreparedArtifacts(
        ArchitectureRunnerPreparation preparation,
        CancellationToken cancellationToken)
    {
        foreach ((string path, string preparedDigest) in preparation.CapturedArtifactContentDigests)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string currentDigest = File.Exists(path)
                ? BuildStateCanonicalHasher.ComputeContentDigest(path, cancellationToken)
                : "missing";
            if (!string.Equals(preparedDigest, currentDigest, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Prepared artifact '{path}' changed after cache authorization; reprepare is required.");
            }
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
        CancellationToken cancellationToken,
        int? maxParallelism)
    {
        return new ArchitectureAnalysisContext(repositoryRoot, resolution.ResolvedAssemblies,
            resolution.MissingAssemblyNames, resolution.AssemblyProbingPaths, discovery.Diagnostics, attemptedDiscovery,
            resolution.IsolatedLoadScope, resolution.SelectedAssemblyArtifactPaths)
        {
            CancellationToken = cancellationToken,
            MaxParallelism = MaxParallelismResolver.Resolve(maxParallelism),
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
