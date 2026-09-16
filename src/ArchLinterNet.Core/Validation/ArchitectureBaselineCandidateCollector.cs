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
            RequestedBuildStateShape.From(request),
            request.UsePreparedPostBuildState,
            request.PreparedPostBuildRunner,
            useMetadataFirstEnsureBuilt: false);

        return CollectCandidatesCore(
            request.PolicyPath, request.Mode, request.ConditionSetName, request.ContractIds, buildState, request.CancellationToken);
    }

    internal BaselineCandidateCollection CollectGenerateCandidates(BaselineGenerationRequest request)
    {
        BaselineBuildStateOptions? buildState = BaselineBuildStateOptions.From(
            RequestedBuildStateShape.From(request),
            usePreparedPostBuildState: false,
            preparedPostBuildRunner: null,
            useMetadataFirstEnsureBuilt: false);

        return CollectCandidatesCore(
            request.PolicyPath, request.Mode, request.ConditionSetName, request.ContractIds, buildState, request.CancellationToken);
    }

    internal BaselineCandidateCollection CollectUpdateCandidates(BaselineUpdateRequest request)
    {
        BaselineBuildStateOptions? buildState = BaselineBuildStateOptions.From(
            RequestedBuildStateShape.From(request),
            usePreparedPostBuildState: false,
            preparedPostBuildRunner: null,
            useMetadataFirstEnsureBuilt: false);

        return CollectCandidatesCore(
            request.PolicyPath, request.Mode, request.ConditionSetName, request.ContractIds, buildState, request.CancellationToken);
    }

    internal BaselineCandidateCollection CollectPruneCandidates(BaselinePruneRequest request)
    {
        BaselineBuildStateOptions? buildState = BaselineBuildStateOptions.From(
            RequestedBuildStateShape.From(request),
            usePreparedPostBuildState: false,
            preparedPostBuildRunner: null,
            useMetadataFirstEnsureBuilt: false);

        return CollectCandidatesCore(
            request.PolicyPath, request.Mode, request.ConditionSetName, request.ContractIds, buildState, request.CancellationToken);
    }

    internal BaselineCandidateCollection CollectVerifyCandidates(BaselineVerifyRequest request)
    {
        BaselineBuildStateOptions? buildState = BaselineBuildStateOptions.From(
            RequestedBuildStateShape.From(request),
            usePreparedPostBuildState: false,
            preparedPostBuildRunner: null,
            useMetadataFirstEnsureBuilt: true);

        return CollectCandidatesCore(
            request.PolicyPath, request.Mode, request.ConditionSetName, request.ContractIds, buildState, request.CancellationToken);
    }

    internal BaselineCandidateCollection CollectCandidates(
        string policyPath,
        string mode,
        string? conditionSetName,
        IReadOnlyCollection<string>? contractIds,
        CancellationToken cancellationToken = default)
    {
        return CollectCandidatesCore(
            policyPath, mode, conditionSetName, contractIds, buildState: null, cancellationToken);
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
        BaselineBuildStateOptions? buildState,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateMode(mode);

        ArchitectureContractDocument document = runnerSetupService.LoadDocument(policyPath, null, null, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();

        HashSet<string>? selectedContractIds = ResolveSelectedContractIds(document, mode, contractIds);

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
            SetupOutcome outcome = ResolveSetup(
                document, policyPath, conditionSetName, buildState, selectedContractIds, mode, cancellationToken);

            // Whatever setup the outcome carries -- even a blocked one -- is the setup this call
            // produced and therefore the one this method owns disposing of.
            setup = outcome.Setup;
            if (outcome.Blocked != null)
            {
                return outcome.Blocked;
            }

            IArchitectureContractRunner runner = setup is null
                ? throw new InvalidOperationException("Architecture runner materialization did not produce a runner.")
                : setup.Runner;

            return BuildCandidateCollection(document, mode, runner, selectedContractIds, cancellationToken);
        }
        finally
        {
            setup?.Runner.Session.Context.Dispose();
        }
    }

    private static void ValidateMode(string mode)
    {
        if (mode is not (ModeStrict or ModeAudit or "all"))
        {
            throw new ArgumentException($"Invalid mode: {mode}. Use 'strict', 'audit', or 'all'.", nameof(mode));
        }
    }

    private static HashSet<string>? ResolveSelectedContractIds(
        ArchitectureContractDocument document,
        string mode,
        IReadOnlyCollection<string>? contractIds)
    {
        if (contractIds is not { Count: > 0 })
        {
            return null;
        }

        HashSet<string> selectedContractIds = new(contractIds, StringComparer.OrdinalIgnoreCase);
        HashSet<string> availableIds = CollectAvailableContractIds(document, mode);
        List<string> unknownIds = selectedContractIds.Where(id => !availableIds.Contains(id)).ToList();

        if (unknownIds.Count > 0)
        {
            throw new InvalidOperationException(
                $"Unknown contract IDs: {string.Join(", ", unknownIds)}{Environment.NewLine}" +
                $"Available IDs in {mode} mode: {string.Join(", ", availableIds.OrderBy(id => id))}");
        }

        return selectedContractIds;
    }

    // The three ways CollectCandidatesCore's try block used to materialize a runner (prepared
    // post-build state, metadata-first ensure-built, or ordinary), extracted one branch per method
    // below and dispatched here. Each branch reports back both the setup it produced (so the
    // caller's finally still disposes exactly what used to be disposed, even when blocked) and, if
    // a build-state preflight blocked it, the terminal collection to return.
    private SetupOutcome ResolveSetup(
        ArchitectureContractDocument document,
        string policyPath,
        string? conditionSetName,
        BaselineBuildStateOptions? buildState,
        HashSet<string>? selectedContractIds,
        string mode,
        CancellationToken cancellationToken)
    {
        if (buildState?.UsePreparedPostBuildState == true)
        {
            return ResolvePreparedPostBuildSetup(document, buildState, selectedContractIds, mode, cancellationToken);
        }

        if (buildState?.PreparationMode == BuildPreparationMode.EnsureBuilt
            && buildState.UseMetadataFirstEnsureBuilt)
        {
            return ResolveMetadataFirstEnsureBuiltSetup(
                document, policyPath, conditionSetName, buildState, selectedContractIds, mode, cancellationToken);
        }

        return ResolveOrdinarySetup(
            document, policyPath, conditionSetName, buildState, selectedContractIds, mode, cancellationToken);
    }

    private SetupOutcome ResolvePreparedPostBuildSetup(
        ArchitectureContractDocument document,
        BaselineBuildStateOptions buildState,
        HashSet<string>? selectedContractIds,
        string mode,
        CancellationToken cancellationToken)
    {
        ArchitectureRunnerSetup setup = runnerSetupService.MaterializePreparedRunner(
            document,
            buildState.PreparedPostBuildRunner
                ?? throw new InvalidOperationException("Prepared baseline analysis requires validation's receipt-backed artifact selection."),
            selectedContractIds: selectedContractIds,
            enableUnmatchedIgnoreTracking: true,
            mode: mode == "all" ? null : mode,
            cancellationToken: cancellationToken);

        // Ownership of `setup` only reaches the caller through the returned SetupOutcome. If the
        // preflight below throws (including cancellation) before that outcome is produced, this
        // method must dispose the runner it just materialized itself -- otherwise the caller's
        // try/finally never sees it and it leaks.
        try
        {
            BuildStatePreflightResult preflight = RunBuildStatePreflight(setup.Runner, buildState, cancellationToken);
            return preflight.Blocked
                ? new SetupOutcome(setup, BaselineCandidateCollection.PreflightBlocked(document, preflight.Diagnostics))
                : new SetupOutcome(setup, null);
        }
        catch
        {
            setup.Runner.Session.Context.Dispose();
            throw;
        }
    }

    private SetupOutcome ResolveMetadataFirstEnsureBuiltSetup(
        ArchitectureContractDocument document,
        string policyPath,
        string? conditionSetName,
        BaselineBuildStateOptions buildState,
        HashSet<string>? selectedContractIds,
        string mode,
        CancellationToken cancellationToken)
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
            return new SetupOutcome(null, BaselineCandidateCollection.PreflightBlocked(document, preflight.Diagnostics));
        }

        preparation = PostBuildArtifactEvidenceRefresher.Refresh(document, preparation, preflight, cancellationToken);
        preflight = RunBuildStatePreflight(
            preparation,
            buildState with { PreparationMode = BuildPreparationMode.Ordinary },
            cancellationToken);
        if (preflight.Blocked)
        {
            return new SetupOutcome(null, BaselineCandidateCollection.PreflightBlocked(document, preflight.Diagnostics));
        }

        if (preparation.HasCompleteRootSelection && preparation.SelectedAssemblyArtifactPaths.Count > 0)
        {
            ArchitectureRunnerSetup preparedSetup = runnerSetupService.MaterializePreparedRunner(
                document,
                preparation,
                selectedContractIds: selectedContractIds,
                enableUnmatchedIgnoreTracking: true,
                mode: mode == "all" ? null : mode,
                cancellationToken: cancellationToken);
            return new SetupOutcome(preparedSetup, null);
        }

        // An incomplete metadata selection cannot be materialized. Preserve the existing ordinary
        // resolution fallback, but only after metadata preparation and both preflight decisions
        // have established that no build result is being consumed by the prepared path.
        ArchitectureRunnerSetup fallbackSetup = runnerSetupService.BuildRunner(
            document,
            policyPath,
            conditionSetName,
            selectedContractIds: selectedContractIds,
            enableUnmatchedIgnoreTracking: true,
            mode: mode == "all" ? null : mode,
            cancellationToken: cancellationToken);
        return new SetupOutcome(fallbackSetup, null);
    }

    private SetupOutcome ResolveOrdinarySetup(
        ArchitectureContractDocument document,
        string policyPath,
        string? conditionSetName,
        BaselineBuildStateOptions? buildState,
        HashSet<string>? selectedContractIds,
        string mode,
        CancellationToken cancellationToken)
    {
        ArchitectureRunnerSetup setup = runnerSetupService.BuildRunner(
            document,
            policyPath,
            conditionSetName,
            selectedContractIds: selectedContractIds,
            enableUnmatchedIgnoreTracking: true,
            mode: mode == "all" ? null : mode,
            cancellationToken: cancellationToken);

        if (buildState == null)
        {
            return new SetupOutcome(setup, null);
        }

        // From here on, `setup` is only handed to the caller through the returned SetupOutcome.
        // Any exception (including cancellation) below must dispose whichever runner this method
        // currently holds before propagating -- otherwise it leaks, since the caller's try/finally
        // never receives it.
        try
        {
            BuildStatePreflightResult preflight = RunBuildStatePreflight(setup.Runner, buildState, cancellationToken);
            if (preflight.Blocked)
            {
                return new SetupOutcome(setup, BaselineCandidateCollection.PreflightBlocked(document, preflight.Diagnostics));
            }

            if (buildState.PreparationMode != BuildPreparationMode.EnsureBuilt
                || setup.Runner.Session.Context.ProjectDiscovery is not { DiscoveredProjects.Count: > 0 })
            {
                return new SetupOutcome(setup, null);
            }

            // Baseline diff keeps its established isolated post-build path. Baseline verify takes
            // the metadata-first branch above to avoid locking outputs.
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

            return preflight.Blocked
                ? new SetupOutcome(setup, BaselineCandidateCollection.PreflightBlocked(document, preflight.Diagnostics))
                : new SetupOutcome(setup, null);
        }
        catch
        {
            setup.Runner.Session.Context.Dispose();
            throw;
        }
    }

    private BaselineCandidateCollection BuildCandidateCollection(
        ArchitectureContractDocument document,
        string mode,
        IArchitectureContractRunner runner,
        HashSet<string>? selectedContractIds,
        CancellationToken cancellationToken)
    {
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

    // Whatever ResolveSetup produced: the setup to use/dispose (Setup, possibly still set even
    // when Blocked is non-null -- disposal must match what CollectCandidatesCore used to do before
    // this branching moved into its own methods) and, when a build-state preflight blocked
    // materialization, the terminal collection CollectCandidatesCore should return immediately.
    private readonly record struct SetupOutcome(ArchitectureRunnerSetup? Setup, BaselineCandidateCollection? Blocked);

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

    // The build-state-shaped subset of a baseline request's fields (PreparationMode, NoRestore,
    // and the four Requested* overrides), common to every request type that reaches
    // BaselineBuildStateOptions.From. Grouping them here is what keeps From's own parameter count
    // in bounds; the two From overloads below are how the two different request-record shapes
    // (BaselineBuildStateRequest-derived vs. the standalone diff/verify records) each construct one.
    private readonly record struct RequestedBuildStateShape(
        BuildPreparationMode PreparationMode,
        bool NoRestore,
        string? RequestedConfiguration,
        string? RequestedTargetFramework,
        string? RequestedPlatform,
        string? RequestedRuntimeIdentifier)
    {
        public static RequestedBuildStateShape From(BaselineBuildStateRequest request) => new(
            request.PreparationMode,
            request.NoRestore,
            request.RequestedConfiguration,
            request.RequestedTargetFramework,
            request.RequestedPlatform,
            request.RequestedRuntimeIdentifier);

        public static RequestedBuildStateShape From(BaselineDiffRequest request) => new(
            request.PreparationMode,
            request.NoRestore,
            request.RequestedConfiguration,
            request.RequestedTargetFramework,
            request.RequestedPlatform,
            request.RequestedRuntimeIdentifier);

        public static RequestedBuildStateShape From(BaselineVerifyRequest request) => new(
            request.PreparationMode,
            request.NoRestore,
            request.RequestedConfiguration,
            request.RequestedTargetFramework,
            request.RequestedPlatform,
            request.RequestedRuntimeIdentifier);
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
            RequestedBuildStateShape shape,
            bool usePreparedPostBuildState,
            ArchitectureRunnerPreparation? preparedPostBuildRunner,
            bool useMetadataFirstEnsureBuilt)
        {
            return shape.PreparationMode == BuildPreparationMode.EnsureBuilt
                || shape.NoRestore
                || shape.RequestedConfiguration is not null
                || shape.RequestedTargetFramework is not null
                || shape.RequestedPlatform is not null
                || shape.RequestedRuntimeIdentifier is not null
                || usePreparedPostBuildState
                ? new(
                    shape.PreparationMode,
                    shape.NoRestore,
                    shape.RequestedConfiguration,
                    shape.RequestedTargetFramework,
                    shape.RequestedPlatform,
                    shape.RequestedRuntimeIdentifier,
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
