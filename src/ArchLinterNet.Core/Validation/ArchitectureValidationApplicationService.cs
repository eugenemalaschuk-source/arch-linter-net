using ArchLinterNet.Core.BuildState;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Execution.Abstractions;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;
using ArchLinterNet.Core.Validation.Abstractions;

namespace ArchLinterNet.Core.Validation;

public sealed class ArchitectureValidationApplicationService(
    IArchitectureRunnerSetupService runnerSetupService,
    IArchitectureContractHandlerRegistry handlerRegistry,
    IArchitectureContractExecutor contractExecutor,
    IBuildStatePreparationService buildStatePreparationService)
    : IArchitectureValidationApplicationService
{
    // Exception.Data lets hosts recover completed profile evidence without changing the exact
    // OperationCanceledException type that existing API consumers observe.
    private const string CancellationCountersDataKey = "ArchLinterNet.AnalysisProfile.Counters";
    private const string CancellationInputPathsDataKey = "ArchLinterNet.AnalysisProfile.InputPaths";
    private const string ErrorSeverity = "error";
    private const string ModeStrict = "strict";
    private const string ModeAudit = "audit";

    // Single-mode validation stays a thin wrapper over the snapshot primitive: one snapshot is
    // created for this request, evaluated for its one requested mode, and disposed before
    // returning — so existing callers keep their exact current behavior, results, and performance
    // (see openspec/specs/analysis-snapshot/spec.md, "Single-mode validation remains simple").
    public ValidationOutcome Validate(ValidationRequest request, ValidationTiming? timing = null)
    {
        return ValidateWithCounters(request, timing).Outcome;
    }

    public (ValidationOutcome Outcome, ArchitectureAnalysisSnapshotCounters Counters) ValidateWithCounters(
        ValidationRequest request, ValidationTiming? timing = null)
    {
        if (request.Mode is not (ModeStrict or ModeAudit))
        {
            throw new ArgumentException($"Invalid mode: {request.Mode}. Use 'strict' or 'audit'.", nameof(request));
        }

        using (timing?.Measure("total"))
        {
            // request.Mode is passed as the modeHint: a single-mode Validate call must resolve
            // assemblies exactly as it did before this change (see ArchitectureRunnerSetupService.
            // ShouldResolveAssemblyOutputs and ArchitectureAssemblyResolutionService's mode-aware
            // unresolved-project-coverage bypass), not the looser "any mode may run" union
            // CreateSnapshot uses when it doesn't know which modes will be evaluated.
            using ArchitectureAnalysisSnapshot snapshot = CreateSnapshotCore(
                AnalysisSnapshotRequest.FromValidationRequest(request), request.Mode, timing);
            try
            {
                ValidationOutcome outcome = snapshot.Evaluate(request.Mode, timing);
                return (outcome, snapshot.Counters);
            }
            catch (OperationCanceledException ex)
            {
                AttachCancellationProfileState(ex, snapshot.Counters, snapshot.GetProfileInputPaths());
                throw;
            }
        }
    }

    // Composes policy, evaluates the selected project graph, loads target assemblies, and runs
    // build-state preflight exactly once, returning an immutable snapshot that any number of
    // strict/audit Evaluate calls can be served from without repeating that setup. Since the
    // caller may evaluate either or both modes later, assembly resolution is scoped
    // conservatively for both (mode=null — the same "union of strict and audit" semantics
    // ArchitectureGraphApplicationService/ArchitectureBaselineApplicationService already use for
    // their mode="all" case), matching whichever mode(s) actually get evaluated.
    public ArchitectureAnalysisSnapshot CreateSnapshot(AnalysisSnapshotRequest request, ValidationTiming? timing = null)
    {
        return CreateSnapshotCore(request, modeHint: null, timing);
    }

    private ArchitectureAnalysisSnapshot CreateSnapshotCore(
        AnalysisSnapshotRequest request, string? modeHint, ValidationTiming? timing)
    {
        // Fail fast on an invalid override before any policy/project/assembly work begins — see
        // openspec/specs/bounded-parallel-scanning/spec.md, "Zero or negative values are rejected
        // before scanning begins".
        MaxParallelismResolver.Resolve(request.MaxParallelism);

        SnapshotConstructionState state = new();
        try
        {
            return BuildSnapshot(state, request, modeHint, timing);
        }
        catch (OperationCanceledException ex)
        {
            state.Setup?.Runner.Session.Context.Dispose();
            AttachCancellationProfileState(ex, BuildCancellationCounters(state), BuildCancellationInputPaths(request, state));
            throw;
        }
        catch (Exception ex) when (state.Document is not null
            && ex is not ArchitecturePolicyLoadException and not ArchitecturePolicyValidationException)
        {
            throw CreateEvaluationException(ex, request, state);
        }
    }

    private ArchitectureAnalysisSnapshot BuildSnapshot(
        SnapshotConstructionState state,
        AnalysisSnapshotRequest request,
        string? modeHint,
        ValidationTiming? timing)
    {
        using (timing?.Measure("policy_composition"))
        {
            try
            {
                state.Document = runnerSetupService.LoadDocument(
                    request.PolicyPath, request.BaselinePath, timing, request.CancellationToken);
            }
            catch (ArchitecturePolicyImportException ex)
            {
                throw new ArchitecturePolicyLoadException(ex.Message, ex.Diagnostic, ex.Category.ToString(), ex);
            }

            request.CancellationToken.ThrowIfCancellationRequested();
            state.Policy = ComposeDocument(state.Document, request, modeHint);
            state.PolicyCompositions = 1;
        }

        request.CancellationToken.ThrowIfCancellationRequested();
        if (request.CacheLocation is not null)
        {
            ArchitectureRunnerPreparation preparation;
            using (timing?.Measure("metadata_preparation"))
            {
                preparation = runnerSetupService.PrepareRunner(
                    state.Policy.Document, request.PolicyPath, request.ConditionSetName,
                    request.PreprocessorSymbols, state.Policy.SelectedContractIds, modeHint,
                    request.CancellationToken);
            }

            state.ProjectGraphEvaluations = 1;
            BuildStatePreflightResult preparedPreflight;
            using (timing?.Measure("build_state_preflight"))
                preparedPreflight = RunBuildStatePreflight(request, preparation);

            if (!preparedPreflight.Blocked && request.PreparationMode == BuildPreparationMode.EnsureBuilt)
            {
                // The build/receipt pass above is metadata-only. Rebuild the plan from the
                // current post-build artifacts before hashing or loading anything; a cache entry
                // can therefore never authorize the pre-build bytes.
                using (timing?.Measure("post_ensure_built_metadata_preparation"))
                {
                    preparation = runnerSetupService.PrepareRunner(
                        state.Policy.Document, request.PolicyPath, request.ConditionSetName,
                        request.PreprocessorSymbols, state.Policy.SelectedContractIds, modeHint,
                        request.CancellationToken);
                }

                state.ProjectGraphEvaluations++;
                using (timing?.Measure("post_ensure_built_preflight"))
                    preparedPreflight = RunBuildStatePreflight(
                        request with { PreparationMode = BuildPreparationMode.Ordinary }, preparation);
            }

            state.Preparation = preparation;

            return new ArchitectureAnalysisSnapshot(
                state.Policy.Document,
                setup: null,
                preparedPreflight,
                state.Policy.UnmatchedConfig,
                state.Policy.PolicyConsistencyConfig,
                state.Policy.CoverageConfig,
                request.EnforceUnmatchedIgnoredViolationsPolicy,
                request.IncludeAsmdefContracts,
                contractExecutor,
                handlerRegistry,
                policyCompositions: state.PolicyCompositions,
                projectGraphEvaluations: state.ProjectGraphEvaluations,
                assemblyLoads: 0,
                requestedContractIds: modeHint == null ? request.ContractIds : null,
                cacheContext: BuildCacheContext(request),
                preparedRepositoryRoot: preparation.RepositoryRoot,
                preparedArtifactPaths: preparation.SelectedAssemblyArtifactPaths,
                preparedArtifactContentDigests: preparation.CapturedArtifactContentDigests,
                preparedProjectPaths: preparation.PreparedProjectPaths,
                preparedArtifactClosureComplete: preparation.HasCompleteArtifactSelection,
                materializeSetup: () => preparation.HasCompleteRootSelection
                    ? runnerSetupService.MaterializePreparedRunner(
                        state.Policy.Document, preparation, state.Policy.SelectedContractIds,
                        state.Policy.EnableUnmatchedIgnoreTracking, timing, modeHint,
                        request.CancellationToken, request.MaxParallelism)
                    : BuildRunnerFor(state.Policy, request, modeHint, timing),
                cancellationToken: request.CancellationToken);
        }

        using (timing?.Measure("load_and_setup"))
            state.Setup = BuildRunnerFor(state.Policy, request, modeHint, timing);

        state.ProjectGraphEvaluations = 1;
        state.AssemblyLoads = state.Setup.AssemblyLoads;
        IArchitectureContractRunner runner = state.Setup.Runner;

        request.CancellationToken.ThrowIfCancellationRequested();
        BuildStatePreflightResult preflight;
        using (timing?.Measure("build_state_preflight"))
            preflight = RunBuildStatePreflight(request, runner);

        request.CancellationToken.ThrowIfCancellationRequested();
        if (!preflight.Blocked
            && request.PreparationMode == BuildPreparationMode.EnsureBuilt
            && runner.Session.Context.ProjectDiscovery is { DiscoveredProjects.Count: > 0 })
        {
            ArchitectureRunnerSetup postBuildSetup;
            using (timing?.Measure("post_ensure_built_reload"))
                postBuildSetup = BuildRunnerFor(state.Policy, request, modeHint, timing, loadPostBuildArtifacts: true);
            state.Setup = postBuildSetup;
            state.ProjectGraphEvaluations++;
            state.AssemblyLoads += postBuildSetup.AssemblyLoads;
        }

        request.CancellationToken.ThrowIfCancellationRequested();
        return new ArchitectureAnalysisSnapshot(
            state.Policy.Document,
            state.Setup,
            preflight,
            state.Policy.UnmatchedConfig,
            state.Policy.PolicyConsistencyConfig,
            state.Policy.CoverageConfig,
            request.EnforceUnmatchedIgnoredViolationsPolicy,
            request.IncludeAsmdefContracts,
            contractExecutor,
            handlerRegistry,
            policyCompositions: state.PolicyCompositions,
            projectGraphEvaluations: state.ProjectGraphEvaluations,
            assemblyLoads: state.AssemblyLoads,
            requestedContractIds: modeHint == null ? request.ContractIds : null,
            cacheContext: BuildCacheContext(request),
            cancellationToken: request.CancellationToken);
    }

    // Null whenever the caller did not configure a cache location (ValidationRequest.CacheLocation /
    // AnalysisSnapshotRequest.CacheLocation stay null by default) — Evaluate() then never attempts a
    // cache lookup and behaves exactly as before this option existed.
    private static AnalysisSnapshotCacheContext? BuildCacheContext(AnalysisSnapshotRequest request)
    {
        return request.CacheLocation is not { } location
            ? null
            : new AnalysisSnapshotCacheContext(
                location,
                request.ConditionSetName,
                request.ContractIds ?? Array.Empty<string>(),
                request.RequestedConfiguration,
                request.RequestedTargetFramework,
                request.RequestedPlatform,
                request.RequestedRuntimeIdentifier,
                request.PreprocessorSymbols,
                request.BaselinePath);
    }

    private static ArchitectureAnalysisSnapshotCounters BuildCancellationCounters(SnapshotConstructionState state)
    {
        ArchitectureRunnerSetup? setup = state.Setup;
        return new ArchitectureAnalysisSnapshotCounters
        {
            PolicyCompositions = state.PolicyCompositions,
            ProjectGraphEvaluations = state.ProjectGraphEvaluations,
            AssemblyLoads = state.AssemblyLoads,
            DiscoveredProjectCount = setup?.Runner.Session.Context.ProjectDiscovery?.DiscoveredProjects.Count ?? 0,
            RetainedAssemblyCount = setup?.Runner.Session.Context.TargetAssemblies.Count ?? 0,
            SelectedAssemblyCount = (setup?.Runner.Session.Context.TargetAssemblies.Count ?? 0)
                + (setup?.Runner.Session.Context.MissingAssemblyNames.Count ?? 0),
        };
    }

    private static string[] BuildCancellationInputPaths(
        AnalysisSnapshotRequest request, SnapshotConstructionState state)
    {
        string repositoryRoot = state.Setup?.RepositoryRoot
            ?? Path.GetDirectoryName(Path.GetFullPath(request.PolicyPath))
            ?? Environment.CurrentDirectory;
        IEnumerable<string> policyInputPaths = state.Document?.Provenance.Sources
            .Select(source => Path.GetFullPath(Path.Combine(repositoryRoot, source.SourcePath)))
            ?? Array.Empty<string>();
        IEnumerable<string> setupInputPaths = state.Setup is null
            ? Array.Empty<string>()
            : state.Setup.Runner.Session.Context.TargetAssemblies
                .Select(SafeAssemblyLocation)
                .Where(path => !string.IsNullOrEmpty(path))
                .Select(path => Path.GetFullPath(path!))
                .SelectMany(path => new[] { path, BuildReceiptStore.ReceiptPathFor(path) })
                .Concat(state.Setup.Runner.Session.Context.DiscoveredProjectPaths);
        return policyInputPaths
            .Append(Path.GetFullPath(request.PolicyPath))
            .Concat(request.BaselinePath is null ? Array.Empty<string>() : [Path.GetFullPath(request.BaselinePath)])
            .Concat(setupInputPaths)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static ArchitectureAnalysisEvaluationException CreateEvaluationException(
        Exception exception, AnalysisSnapshotRequest request, SnapshotConstructionState state)
    {
        string repositoryRoot = state.Setup?.RepositoryRoot
            ?? Path.GetDirectoryName(Path.GetFullPath(request.PolicyPath))
            ?? Environment.CurrentDirectory;
        HashSet<string> policyInputPaths = new(StringComparer.OrdinalIgnoreCase)
        {
            Path.GetFullPath(request.PolicyPath),
        };
        if (request.BaselinePath is not null)
        {
            policyInputPaths.Add(Path.GetFullPath(request.BaselinePath));
        }
        foreach (ArchitecturePolicySourceDescriptor source in state.Document!.Provenance.Sources)
        {
            policyInputPaths.Add(Path.GetFullPath(Path.Combine(repositoryRoot, source.SourcePath)));
        }
        IReadOnlyList<string> resolvedAssemblyPaths = state.Setup?.Runner.Session.Context.TargetAssemblies
            .Select(assembly => assembly.Location)
            .Where(path => !string.IsNullOrEmpty(path))
            .Select(Path.GetFullPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray()
            ?? Array.Empty<string>();
        IReadOnlyList<string> discoveredProjectPaths =
            state.Setup?.Runner.Session.Context.DiscoveredProjectPaths ?? Array.Empty<string>();

        return new ArchitectureAnalysisEvaluationException(
            exception.Message, exception, policyInputPaths.ToArray(), resolvedAssemblyPaths, discoveredProjectPaths);
    }

    // Preflight only runs when project discovery produced a project graph — the fingerprint/
    // receipt model this needs (ArchitectureDiscoveredProject.Path/AssemblyName) has no
    // counterpart when target assemblies are configured directly via analysis.target_assemblies
    // without project discovery.
    // Assembly resolution is skipped entirely (not merely unsuccessful) when only project-scope
    // coverage contracts are selected — see ArchitectureRunnerSetupService.ShouldResolveAssemblyOutputs.
    // That path deliberately lets the coverage engine classify unresolved projects as "unknown"
    // instead of failing the run, so preflight must not reinterpret "resolution wasn't attempted"
    // as "artifact missing". Resolution having populated neither resolved nor missing names is the
    // signal that it never ran — BuildStatePreflightRunner.Run encodes that same short-circuit.
    private BuildStatePreflightResult RunBuildStatePreflight(AnalysisSnapshotRequest request, IArchitectureContractRunner runner)
    {
        return BuildStatePreflightRunner.Run(
            runner.Session.Context.RepositoryRoot,
            runner.Session.Context.ProjectDiscovery,
            runner.Session.Context.TargetAssemblies,
            runner.Session.Context.MissingAssemblyNames,
            includeResolvedAssemblyPathsFromDiscovery: false,
            () => buildStatePreparationService,
            request.PreparationMode,
            request.NoRestore,
            request.RequestedConfiguration,
            request.RequestedTargetFramework,
            request.RequestedPlatform,
            request.RequestedRuntimeIdentifier,
            request.CancellationToken);
    }

    private BuildStatePreflightResult RunBuildStatePreflight(
        AnalysisSnapshotRequest request, ArchitectureRunnerPreparation preparation)
    {
        if (preparation.ProjectDiscovery.DiscoveredProjects.Count == 0)
        {
            return new BuildStatePreflightResult(Array.Empty<BuildStatePreflightDiagnostic>());
        }

        Dictionary<string, string> paths = preparation.ProjectDiscovery.ResolvedAssemblyPaths
            .Where(pair => preparation.SelectedAssemblyArtifactPaths.Contains(pair.Value, StringComparer.OrdinalIgnoreCase))
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        BuildStateResolvedAssemblies resolution = new(
            Array.Empty<System.Reflection.Assembly>(), preparation.MissingAssemblyNames)
        {
            ResolvedAssemblyPaths = paths
        };

        if (resolution.ResolvedAssemblyPaths.Count == 0 && resolution.MissingAssemblyNames.Count == 0)
        {
            return new BuildStatePreflightResult(Array.Empty<BuildStatePreflightDiagnostic>());
        }

        return buildStatePreparationService.Prepare(new BuildStatePreflightRequest(
            preparation.RepositoryRoot,
            preparation.ProjectDiscovery,
            resolution,
            request.PreparationMode,
            request.NoRestore,
            request.RequestedConfiguration,
            request.RequestedTargetFramework,
            request.RequestedPlatform,
            request.RequestedRuntimeIdentifier,
            request.CancellationToken));
    }

    private readonly record struct ComposedPolicy(
        ArchitectureContractDocument Document,
        string UnmatchedConfig,
        string PolicyConsistencyConfig,
        string CoverageConfig,
        HashSet<string>? SelectedContractIds,
        bool EnableUnmatchedIgnoreTracking);

    private sealed class SnapshotConstructionState
    {
        public ArchitectureContractDocument? Document { get; set; }
        public ArchitectureRunnerSetup? Setup { get; set; }
        public ArchitectureRunnerPreparation? Preparation { get; set; }
        public ComposedPolicy Policy { get; set; }
        public int PolicyCompositions { get; set; }
        public int ProjectGraphEvaluations { get; set; }
        public int AssemblyLoads { get; set; }
    }

    // Policy load, baseline merge, severity validation, and contract-ID selection depend only on
    // the policy document and the request — never on build output — so this runs exactly once per
    // snapshot even when --ensure-built later triggers a runner/session rebuild (see
    // CreateSnapshotCore). modeHint is null for a snapshot meant to serve any/all requested modes
    // later (see CreateSnapshot) and the specific mode for the single-mode Validate path, so
    // contract-ID selection validates against exactly the same catalog it did before this change
    // for single-mode callers.
    private ComposedPolicy ComposeDocument(
        ArchitectureContractDocument document, AnalysisSnapshotRequest request, string? modeHint)
    {
        string unmatchedConfig = document.Analysis.UnmatchedIgnoredViolations;
        if (request.EnforceUnmatchedIgnoredViolationsPolicy)
        {
            EnsureValidSeverityConfig(unmatchedConfig, "analysis.unmatched_ignored_violations");
        }

        string policyConsistencyConfig = document.Analysis.PolicyConsistency;
        EnsureValidSeverityConfig(policyConsistencyConfig, "analysis.policy_consistency");

        // Coverage contracts themselves are rejected earlier, in IArchitectureRunnerSetupService.LoadDocument
        // (the engine isn't implemented yet; see #97-#103). Validating the severity value here keeps
        // analysis.coverage held to the same "fail fast on malformed config" standard as the other
        // severity settings even though no coverage check currently reads it.
        string coverageConfig = document.Analysis.Coverage;
        EnsureValidSeverityConfig(coverageConfig, "analysis.coverage");

        HashSet<string>? selectedIds = ResolveSelectedContractIds(document, request, modeHint);

        bool enableUnmatchedIgnoreTracking = !request.EnforceUnmatchedIgnoredViolationsPolicy
            || unmatchedConfig != "off";

        return new ComposedPolicy(
            document, unmatchedConfig, policyConsistencyConfig, coverageConfig, selectedIds, enableUnmatchedIgnoreTracking);
    }

    // Project discovery, assembly resolution, and session construction — the part of setup that
    // build output (from --ensure-built) can change — reusing the same ComposedPolicy across two
    // calls means the policy document itself is never recomposed.
    private ArchitectureRunnerSetup BuildRunnerFor(
        ComposedPolicy policy,
        AnalysisSnapshotRequest request,
        string? modeHint,
        ValidationTiming? timing,
        bool loadPostBuildArtifacts = false)
    {
        return loadPostBuildArtifacts
            ? runnerSetupService.BuildRunnerForPostBuild(
                policy.Document, request.PolicyPath, request.ConditionSetName, request.PreprocessorSymbols,
                policy.SelectedContractIds, policy.EnableUnmatchedIgnoreTracking, timing, modeHint,
                request.CancellationToken, request.MaxParallelism)
            : runnerSetupService.BuildRunner(
                policy.Document, request.PolicyPath, request.ConditionSetName, request.PreprocessorSymbols,
                policy.SelectedContractIds, policy.EnableUnmatchedIgnoreTracking, timing, modeHint,
                request.CancellationToken, request.MaxParallelism);
    }

    private static void EnsureValidSeverityConfig(string value, string settingName)
    {
        if (value is not (ErrorSeverity or "warn" or "off"))
        {
            throw new InvalidOperationException($"Invalid {settingName}: {value}. Use 'error', 'warn', or 'off'.");
        }
    }

    // A snapshot built for a specific mode (modeHint set, the single-mode Validate path) validates
    // a requested contract-ID filter against exactly that mode's catalog, unchanged from before
    // this change. A snapshot meant to serve any/all requested modes (modeHint null) validates
    // against the union of strict and audit contract IDs — the same union
    // ArchitectureGraphApplicationService and ArchitectureBaselineApplicationService already use
    // for their mode="all" case.
    private static HashSet<string>? ResolveSelectedContractIds(
        ArchitectureContractDocument document, AnalysisSnapshotRequest request, string? modeHint)
    {
        if (request.ContractIds is not { Count: > 0 })
        {
            return null;
        }

        HashSet<string> selectedIds = new(request.ContractIds, StringComparer.OrdinalIgnoreCase);
        HashSet<string> availableIds = CollectAvailableContractIds(document, modeHint);
        List<string> unknownIds = selectedIds.Where(id => !availableIds.Contains(id)).ToList();

        if (unknownIds.Count > 0)
        {
            string availableIdsLabel = modeHint != null ? $"Available IDs in {modeHint} mode" : "Available IDs";
            throw new InvalidOperationException(
                $"Unknown contract IDs: {string.Join(", ", unknownIds)}{Environment.NewLine}" +
                $"{availableIdsLabel}: {string.Join(", ", availableIds.OrderBy(id => id))}");
        }

        return selectedIds;
    }

    private static HashSet<string> CollectAvailableContractIds(ArchitectureContractDocument document, string? modeHint)
    {
        ArchitectureContractCatalog catalog = ArchitectureContractCatalog.Build(document);
        if (modeHint != null)
        {
            return catalog.AvailableContractIds(modeHint);
        }

        HashSet<string> ids = new(catalog.AvailableContractIds(ModeStrict), StringComparer.OrdinalIgnoreCase);
        ids.UnionWith(catalog.AvailableContractIds(ModeAudit));
        return ids;
    }

    private static string? SafeAssemblyLocation(System.Reflection.Assembly assembly)
    {
        try
        {
            return assembly.Location;
        }
        catch (NotSupportedException)
        {
            return null;
        }
    }

    private static void AttachCancellationProfileState(
        OperationCanceledException exception,
        ArchitectureAnalysisSnapshotCounters counters,
        IReadOnlyList<string> inputPaths)
    {
        exception.Data[CancellationCountersDataKey] = counters;
        exception.Data[CancellationInputPathsDataKey] = inputPaths;
    }
}
