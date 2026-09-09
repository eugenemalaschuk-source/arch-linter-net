using ArchLinterNet.Core.BuildState;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Abstractions;
using ArchLinterNet.Core.Contracts.Families;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Execution.Abstractions;
using ArchLinterNet.Core.Execution.Results;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;

namespace ArchLinterNet.Core.Validation;

internal sealed class ArchitectureBaselineCandidateCollector(
    IArchitectureRunnerSetupService runnerSetupService,
    IArchitectureContractHandlerRegistry handlerRegistry,
    IArchitectureContractExecutor contractExecutor,
    IBuildStatePreparationService? buildStatePreparationService)
{
    private const string ModeStrict = "strict";
    private const string ModeAudit = "audit";

    internal BaselineCandidateCollection CollectDiffCandidates(BaselineDiffRequest request)
    {
        BaselineBuildStateOptions? buildState = BaselineBuildStateOptions.From(
            request.PreparationMode,
            request.NoRestore,
            request.RequestedConfiguration,
            request.RequestedTargetFramework,
            request.RequestedPlatform,
            request.RequestedRuntimeIdentifier,
            request.UsePreparedPostBuildState,
            request.PreparedPostBuildRunner,
            useMetadataFirstEnsureBuilt: false);

        return CollectCandidatesCore(
            request.PolicyPath, request.Mode, request.ConditionSetName, request.ContractIds, request.CancellationToken, buildState);
    }

    internal BaselineCandidateCollection CollectGenerateCandidates(BaselineGenerationRequest request)
    {
        BaselineBuildStateOptions? buildState = BaselineBuildStateOptions.From(
            request.PreparationMode,
            request.NoRestore,
            request.RequestedConfiguration,
            request.RequestedTargetFramework,
            request.RequestedPlatform,
            request.RequestedRuntimeIdentifier,
            usePreparedPostBuildState: false,
            preparedPostBuildRunner: null,
            useMetadataFirstEnsureBuilt: false);

        return CollectCandidatesCore(
            request.PolicyPath, request.Mode, request.ConditionSetName, request.ContractIds, request.CancellationToken, buildState);
    }

    internal BaselineCandidateCollection CollectUpdateCandidates(BaselineUpdateRequest request)
    {
        BaselineBuildStateOptions? buildState = BaselineBuildStateOptions.From(
            request.PreparationMode,
            request.NoRestore,
            request.RequestedConfiguration,
            request.RequestedTargetFramework,
            request.RequestedPlatform,
            request.RequestedRuntimeIdentifier,
            usePreparedPostBuildState: false,
            preparedPostBuildRunner: null,
            useMetadataFirstEnsureBuilt: false);

        return CollectCandidatesCore(
            request.PolicyPath, request.Mode, request.ConditionSetName, request.ContractIds, request.CancellationToken, buildState);
    }

    internal BaselineCandidateCollection CollectPruneCandidates(BaselinePruneRequest request)
    {
        BaselineBuildStateOptions? buildState = BaselineBuildStateOptions.From(
            request.PreparationMode,
            request.NoRestore,
            request.RequestedConfiguration,
            request.RequestedTargetFramework,
            request.RequestedPlatform,
            request.RequestedRuntimeIdentifier,
            usePreparedPostBuildState: false,
            preparedPostBuildRunner: null,
            useMetadataFirstEnsureBuilt: false);

        return CollectCandidatesCore(
            request.PolicyPath, request.Mode, request.ConditionSetName, request.ContractIds, request.CancellationToken, buildState);
    }

    internal BaselineCandidateCollection CollectVerifyCandidates(BaselineVerifyRequest request)
    {
        BaselineBuildStateOptions? buildState = BaselineBuildStateOptions.From(
            request.PreparationMode,
            request.NoRestore,
            request.RequestedConfiguration,
            request.RequestedTargetFramework,
            request.RequestedPlatform,
            request.RequestedRuntimeIdentifier,
            usePreparedPostBuildState: false,
            preparedPostBuildRunner: null,
            useMetadataFirstEnsureBuilt: true);

        return CollectCandidatesCore(
            request.PolicyPath, request.Mode, request.ConditionSetName, request.ContractIds, request.CancellationToken, buildState);
    }

    internal BaselineCandidateCollection CollectCandidates(
        string policyPath,
        string mode,
        string? conditionSetName,
        IReadOnlyCollection<string>? contractIds,
        CancellationToken cancellationToken = default)
    {
        return CollectCandidatesCore(
            policyPath, mode, conditionSetName, contractIds, cancellationToken, buildState: null);
    }

    private BuildStatePreflightResult RunBuildStatePreflight(
        IArchitectureContractRunner runner,
        BaselineBuildStateOptions options,
        CancellationToken cancellationToken)
    {
        return BuildStatePreflightRunner.Run(
            runner.Session.Context.RepositoryRoot,
            runner.Session.Context.ProjectDiscovery,
            runner.Session.Context.TargetAssemblies,
            runner.Session.Context.MissingAssemblyNames,
            includeResolvedAssemblyPathsFromDiscovery: true,
            () => buildStatePreparationService
                ?? throw new InvalidOperationException("Build-state preparation is unavailable for baseline verification."),
            options.PreparationMode,
            options.NoRestore,
            options.RequestedConfiguration,
            options.RequestedTargetFramework,
            options.RequestedPlatform,
            options.RequestedRuntimeIdentifier,
            cancellationToken);
    }

    private BuildStatePreflightResult RunBuildStatePreflight(
        ArchitectureRunnerPreparation preparation,
        BaselineBuildStateOptions options,
        CancellationToken cancellationToken)
    {
        BuildStateResolvedAssemblies? resolution = BuildStatePreflightRunner.CreatePreparationResolution(
            preparation, options.PreparationMode);
        if (resolution is null
            || (resolution.ResolvedAssemblyPaths.Count == 0 && resolution.MissingAssemblyNames.Count == 0))
        {
            return new BuildStatePreflightResult(Array.Empty<BuildStatePreflightDiagnostic>());
        }

        IBuildStatePreparationService preparationService = buildStatePreparationService
            ?? throw new InvalidOperationException("Build-state preparation is unavailable for baseline verification.");
        return preparationService.Prepare(new BuildStatePreflightRequest(
            preparation.RepositoryRoot,
            preparation.ProjectDiscovery,
            resolution,
            options.PreparationMode,
            options.NoRestore,
            options.RequestedConfiguration,
            options.RequestedTargetFramework,
            options.RequestedPlatform,
            options.RequestedRuntimeIdentifier,
            cancellationToken));
    }

    private BaselineCandidateCollection CollectCandidatesCore(
        string policyPath,
        string mode,
        string? conditionSetName,
        IReadOnlyCollection<string>? contractIds,
        CancellationToken cancellationToken,
        BaselineBuildStateOptions? buildState)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (mode is not (ModeStrict or ModeAudit or "all"))
        {
            throw new ArgumentException($"Invalid mode: {mode}. Use 'strict', 'audit', or 'all'.", nameof(mode));
        }

        ArchitectureContractDocument document = runnerSetupService.LoadDocument(policyPath, null, null, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();

        HashSet<string>? selectedContractIds = contractIds is { Count: > 0 }
            ? new HashSet<string>(contractIds, StringComparer.OrdinalIgnoreCase)
            : null;

        if (selectedContractIds != null)
        {
            HashSet<string> availableIds = CollectAvailableContractIds(document, mode);
            List<string> unknownIds = selectedContractIds.Where(id => !availableIds.Contains(id)).ToList();

            if (unknownIds.Count > 0)
            {
                throw new InvalidOperationException(
                    $"Unknown contract IDs: {string.Join(", ", unknownIds)}{Environment.NewLine}" +
                    $"Available IDs in {mode} mode: {string.Join(", ", availableIds.OrderBy(id => id))}");
            }
        }

        if (buildState?.UsePreparedPostBuildState == true
            && buildState.RequestedTargetFramework is not null)
        {
            // The preparation carries the exact selected artifact paths. The effective framework
            // is retained here solely for the isolated shared-framework probing path.
            document.Analysis.TargetFramework = buildState.RequestedTargetFramework;
        }

        ArchitectureRunnerSetup? setup = null;

        try
        {
            if (buildState?.UsePreparedPostBuildState == true)
            {
                setup = runnerSetupService.MaterializePreparedRunner(
                    document,
                    buildState.PreparedPostBuildRunner
                        ?? throw new InvalidOperationException("Prepared baseline analysis requires validation's receipt-backed artifact selection."),
                    selectedContractIds: selectedContractIds,
                    enableUnmatchedIgnoreTracking: true,
                    mode: mode == "all" ? null : mode,
                    cancellationToken: cancellationToken);

                BuildStatePreflightResult preflight = RunBuildStatePreflight(setup.Runner, buildState, cancellationToken);
                if (preflight.Blocked)
                {
                    return BaselineCandidateCollection.PreflightBlocked(document, preflight.Diagnostics);
                }
            }
            else if (buildState?.PreparationMode == BuildPreparationMode.EnsureBuilt
                && buildState.UseMetadataFirstEnsureBuilt)
            {
                ArchitectureRunnerPreparation preparation = runnerSetupService.PrepareRunner(
                    document,
                    policyPath,
                    conditionSetName,
                    selectedContractIds: selectedContractIds,
                    mode: mode == "all" ? null : mode,
                    cancellationToken: cancellationToken);

                BuildStatePreflightResult preflight = RunBuildStatePreflight(preparation, buildState, cancellationToken);
                if (preflight.Blocked)
                {
                    return BaselineCandidateCollection.PreflightBlocked(document, preflight.Diagnostics);
                }

                preparation = PostBuildArtifactEvidenceRefresher.Refresh(
                    document, preparation, preflight, cancellationToken);
                preflight = RunBuildStatePreflight(
                    preparation,
                    buildState with { PreparationMode = BuildPreparationMode.Ordinary },
                    cancellationToken);
                if (preflight.Blocked)
                {
                    return BaselineCandidateCollection.PreflightBlocked(document, preflight.Diagnostics);
                }

                if (preparation.HasCompleteRootSelection && preparation.SelectedAssemblyArtifactPaths.Count > 0)
                {
                    setup = runnerSetupService.MaterializePreparedRunner(
                        document,
                        preparation,
                        selectedContractIds: selectedContractIds,
                        enableUnmatchedIgnoreTracking: true,
                        mode: mode == "all" ? null : mode,
                        cancellationToken: cancellationToken);
                }
                else
                {
                    // An incomplete metadata selection cannot be materialized. Preserve the
                    // existing ordinary resolution fallback, but only after metadata preparation
                    // and both preflight decisions have established that no build result is being
                    // consumed by the prepared path.
                    setup = runnerSetupService.BuildRunner(
                        document,
                        policyPath,
                        conditionSetName,
                        selectedContractIds: selectedContractIds,
                        enableUnmatchedIgnoreTracking: true,
                        mode: mode == "all" ? null : mode,
                        cancellationToken: cancellationToken);
                }
            }
            else
            {
                setup = runnerSetupService.BuildRunner(
                    document,
                    policyPath,
                    conditionSetName,
                    selectedContractIds: selectedContractIds,
                    enableUnmatchedIgnoreTracking: true,
                    mode: mode == "all" ? null : mode,
                    cancellationToken: cancellationToken);

                if (buildState != null)
                {
                    BuildStatePreflightResult preflight = RunBuildStatePreflight(setup.Runner, buildState, cancellationToken);
                    if (preflight.Blocked)
                    {
                        return BaselineCandidateCollection.PreflightBlocked(document, preflight.Diagnostics);
                    }

                    if (buildState.PreparationMode == BuildPreparationMode.EnsureBuilt
                        && setup.Runner.Session.Context.ProjectDiscovery is { DiscoveredProjects.Count: > 0 })
                    {
                        // Baseline diff keeps its established isolated post-build path. Baseline
                        // verify takes the metadata-first branch above to avoid locking outputs.
                        ArchitectureRunnerSetup postBuildSetup = runnerSetupService.BuildRunnerForPostBuild(
                            document, policyPath, conditionSetName,
                            selectedContractIds: selectedContractIds,
                            enableUnmatchedIgnoreTracking: true,
                            mode: mode == "all" ? null : mode,
                            cancellationToken: cancellationToken);
                        setup.Runner.Session.Context.Dispose();
                        setup = postBuildSetup;

                        preflight = RunBuildStatePreflight(
                            setup.Runner,
                            buildState with
                            {
                                PreparationMode = BuildPreparationMode.Ordinary,
                                UsePreparedPostBuildState = false,
                            },
                            cancellationToken);
                        if (preflight.Blocked)
                        {
                            return BaselineCandidateCollection.PreflightBlocked(document, preflight.Diagnostics);
                        }
                    }
                }
            }

            IArchitectureContractRunner runner = setup is null
                ? throw new InvalidOperationException("Architecture runner materialization did not produce a runner.")
                : setup.Runner;
            List<ArchitectureViolation> configViolations = mode switch
            {
                ModeStrict => runner.CheckConfiguration(strict: true),
                ModeAudit => runner.CheckConfiguration(strict: false),
                "all" => runner.CheckConfiguration(),
                _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unsupported baseline mode."),
            };

            if (configViolations.Count > 0)
            {
                return new BaselineCandidateCollection(document, null, configViolations, Array.Empty<BuildStatePreflightDiagnostic>());
            }

            bool includeStrict = mode is ModeStrict or "all";
            bool includeAudit = mode is ModeAudit or "all";
            var applicabilityCandidates = new List<ArchitectureBaselineCandidate>();

            if (includeStrict)
            {
                cancellationToken.ThrowIfCancellationRequested();
                ArchitectureContractExecutionResult execution = contractExecutor.Execute(
                    runner.Session, ModeStrict, handlerRegistry, includeAsmdefContracts: false);
                applicabilityCandidates.AddRange(ProjectApplicabilityCandidates(document, ModeStrict, execution));
            }

            if (includeAudit)
            {
                cancellationToken.ThrowIfCancellationRequested();
                ArchitectureContractExecutionResult execution = contractExecutor.Execute(
                    runner.Session, ModeAudit, handlerRegistry, includeAsmdefContracts: false);
                applicabilityCandidates.AddRange(ProjectApplicabilityCandidates(document, ModeAudit, execution));
            }

            cancellationToken.ThrowIfCancellationRequested();

            // Ordinary candidates are recorded by the executor while each contract runs. Read the
            // runner only after both modes have completed, then append the projection-owned
            // applicability candidates to that complete ordinary inventory.
            var baselineCandidates = runner.BaselineCandidates.ToList();
            baselineCandidates.AddRange(applicabilityCandidates);
            return new BaselineCandidateCollection(
                document, baselineCandidates, new List<ArchitectureViolation>(), Array.Empty<BuildStatePreflightDiagnostic>())
            {
                MetricBaselineCandidates = runner.Session.MetricBaselineCandidates,
                HasSelectedRelativeMetricBudgets = HasSelectedRelativeMetricBudgets(document, mode, selectedContractIds),
            };
        }
        finally
        {
            setup?.Runner.Session.Context.Dispose();
        }
    }

    private static IReadOnlyList<ArchitectureBaselineCandidate> ProjectApplicabilityCandidates(
        ArchitectureContractDocument document,
        string mode,
        ArchitectureContractExecutionResult execution)
    {
        // Baseline collection has no ordinary validation outcome from which to obtain the
        // conformance bit. It is irrelevant to finding projection (which is driven solely by the
        // evaluator's insufficiency reasons), so use the non-blocking value and preserve the exact
        // expected/record join and reason ordering used by validation.
        ArchitectureAssessmentCompletionEvidence? completion = ArchitectureApplicabilityEvaluator.Evaluate(
            execution.ApplicabilityExpectedEntries,
            execution.ApplicabilityRecords,
            conformancePassed: true);
        ArchitectureApplicabilityProjection? projection = ArchitectureApplicabilityProjector.Project(completion, mode);
        return ArchitectureApplicabilityBaselineCandidateProjector.Project(document, mode, projection);
    }

    private static HashSet<string> CollectAvailableContractIds(ArchitectureContractDocument document, string mode)
    {
        ArchitectureContractCatalog catalog = ArchitectureContractCatalog.Build(document);

        if (mode == "all")
        {
            HashSet<string> ids = new(catalog.AvailableContractIds(ModeStrict), StringComparer.OrdinalIgnoreCase);
            ids.UnionWith(catalog.AvailableContractIds(ModeAudit));
            return ids;
        }

        return catalog.AvailableContractIds(mode);
    }

    private static bool HasSelectedRelativeMetricBudgets(
        ArchitectureContractDocument document,
        string mode,
        IReadOnlyCollection<string>? selectedContractIds)
    {
        IEnumerable<ArchitectureMetricBudgetContract> budgets = mode switch
        {
            ModeStrict => document.Contracts.StrictMetricBudgets,
            ModeAudit => document.Contracts.AuditMetricBudgets,
            "all" => document.Contracts.StrictMetricBudgets.Concat(document.Contracts.AuditMetricBudgets),
            _ => Array.Empty<ArchitectureMetricBudgetContract>(),
        };
        return budgets.Any(budget => budget.IsRelative
            && (selectedContractIds is not { Count: > 0 }
                || budget.Id is not null && selectedContractIds.Contains(budget.Id, StringComparer.OrdinalIgnoreCase)));
    }

    private sealed record BaselineBuildStateOptions(
        BuildPreparationMode PreparationMode,
        bool NoRestore,
        string? RequestedConfiguration,
        string? RequestedTargetFramework,
        string? RequestedPlatform,
        string? RequestedRuntimeIdentifier,
        bool UsePreparedPostBuildState,
        ArchitectureRunnerPreparation? PreparedPostBuildRunner,
        bool UseMetadataFirstEnsureBuilt)
    {
        public static BaselineBuildStateOptions? From(
            BuildPreparationMode preparationMode,
            bool noRestore,
            string? requestedConfiguration,
            string? requestedTargetFramework,
            string? requestedPlatform,
            string? requestedRuntimeIdentifier,
            bool usePreparedPostBuildState,
            ArchitectureRunnerPreparation? preparedPostBuildRunner,
            bool useMetadataFirstEnsureBuilt)
        {
            return preparationMode == BuildPreparationMode.EnsureBuilt
                || noRestore
                || requestedConfiguration is not null
                || requestedTargetFramework is not null
                || requestedPlatform is not null
                || requestedRuntimeIdentifier is not null
                || usePreparedPostBuildState
                ? new(
                    preparationMode,
                    noRestore,
                    requestedConfiguration,
                    requestedTargetFramework,
                    requestedPlatform,
                    requestedRuntimeIdentifier,
                    usePreparedPostBuildState,
                    preparedPostBuildRunner,
                    useMetadataFirstEnsureBuilt)
                : null;
        }
    }
}

internal sealed record BaselineCandidateCollection(
    ArchitectureContractDocument Document,
    IReadOnlyList<ArchitectureBaselineCandidate>? Candidates,
    List<ArchitectureViolation> ConfigurationViolations,
    IReadOnlyCollection<BuildStatePreflightDiagnostic> PreflightDiagnostics)
{
    public IReadOnlyList<ArchitectureMetricBaselineEntry> MetricBaselineCandidates { get; init; } =
        Array.Empty<ArchitectureMetricBaselineEntry>();

    public bool HasSelectedRelativeMetricBudgets { get; init; }

    public static BaselineCandidateCollection PreflightBlocked(
        ArchitectureContractDocument document,
        IReadOnlyCollection<BuildStatePreflightDiagnostic> diagnostics) =>
        new(document, null, new List<ArchitectureViolation>(), diagnostics);
}
