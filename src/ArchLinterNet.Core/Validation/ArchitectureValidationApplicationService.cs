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
    private const string ErrorSeverity = "error";
    private const string ModeStrict = "strict";
    private const string ModeAudit = "audit";

    // Single-mode validation stays a thin wrapper over the snapshot primitive: one snapshot is
    // created for this request, evaluated for its one requested mode, and disposed before
    // returning — so existing callers keep their exact current behavior, results, and performance
    // (see openspec/specs/analysis-snapshot/spec.md, "Single-mode validation remains simple").
    public ValidationOutcome Validate(ValidationRequest request, ValidationTiming? timing = null)
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
            return snapshot.Evaluate(request.Mode, timing);
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
        ComposedPolicy policy;
        using (timing?.Measure("policy_composition"))
            policy = ComposeDocument(request, modeHint, timing);

        ArchitectureRunnerSetup setup;
        using (timing?.Measure("load_and_setup"))
            setup = BuildRunnerFor(policy, request, modeHint, timing);

        int projectGraphEvaluations = 1;
        IArchitectureContractRunner runner = setup.Runner;

        BuildStatePreflightResult preflight;
        using (timing?.Measure("build_state_preflight"))
            preflight = RunBuildStatePreflight(request, runner);

        // --ensure-built may have just written new build output that the runner/session above —
        // built from assembly resolution that ran before this build — cannot see: its
        // ArchitectureAnalysisContext captured whatever resolution found (or failed to find) at
        // that earlier point in time. Rebuilding the runner/session after a successful build
        // re-discovers and re-resolves from the now-current filesystem state, so every mode
        // evaluated against this snapshot actually analyzes the artifacts preflight just verified.
        // Only project discovery/assembly resolution/session construction are redone here — the
        // policy document composed above (policy load, baseline merge, severity validation,
        // contract-ID selection) does not depend on build output and is intentionally reused
        // rather than recomposed, so policy composition still happens exactly once regardless of
        // whether ensure-built triggers this rebuild. This rebuild is part of building the one
        // snapshot, not per-mode work, so it runs at most once regardless of how many modes are
        // later evaluated.
        if (!preflight.Blocked
            && request.PreparationMode == BuildPreparationMode.EnsureBuilt
            && runner.Session.Context.ProjectDiscovery is { DiscoveredProjects.Count: > 0 })
        {
            using (timing?.Measure("post_ensure_built_reload"))
                setup = BuildRunnerFor(policy, request, modeHint, timing);
            projectGraphEvaluations = 2;
            runner = setup.Runner;
        }

        return new ArchitectureAnalysisSnapshot(
            policy.Document,
            setup,
            preflight,
            policy.UnmatchedConfig,
            policy.PolicyConsistencyConfig,
            policy.CoverageConfig,
            request.EnforceUnmatchedIgnoredViolationsPolicy,
            request.IncludeAsmdefContracts,
            contractExecutor,
            handlerRegistry,
            policyCompositions: 1,
            projectGraphEvaluations: projectGraphEvaluations,
            // Only a snapshot meant to serve any/all requested modes (modeHint null) needs the
            // per-mode re-check in Evaluate — a single-mode snapshot (modeHint set) already
            // validated its one mode's contract IDs exactly as before this change, above.
            requestedContractIds: modeHint == null ? request.ContractIds : null);
    }

    // Preflight only runs when project discovery produced a project graph — the fingerprint/
    // receipt model this needs (ArchitectureDiscoveredProject.Path/AssemblyName) has no
    // counterpart when target assemblies are configured directly via analysis.target_assemblies
    // without project discovery.
    private BuildStatePreflightResult RunBuildStatePreflight(AnalysisSnapshotRequest request, IArchitectureContractRunner runner)
    {
        Discovery.ProjectDiscoveryResult? discovery = runner.Session.Context.ProjectDiscovery;
        if (discovery == null || discovery.DiscoveredProjects.Count == 0)
        {
            return new BuildStatePreflightResult(Array.Empty<BuildStatePreflightDiagnostic>());
        }

        BuildStateResolvedAssemblies resolution = new(
            runner.Session.Context.TargetAssemblies,
            runner.Session.Context.MissingAssemblyNames);

        // Assembly resolution is skipped entirely (not merely unsuccessful) when only
        // project-scope coverage contracts are selected — see
        // ArchitectureRunnerSetupService.ShouldResolveAssemblyOutputs. That path deliberately lets
        // the coverage engine classify unresolved projects as "unknown" instead of failing the
        // run, so preflight must not reinterpret "resolution wasn't attempted" as "artifact
        // missing". Resolution having populated neither resolved nor missing names is the signal
        // that it never ran.
        if (resolution.ResolvedAssemblies.Count == 0 && resolution.MissingAssemblyNames.Count == 0)
        {
            return new BuildStatePreflightResult(Array.Empty<BuildStatePreflightDiagnostic>());
        }

        return buildStatePreparationService.Prepare(new BuildStatePreflightRequest(
            runner.Session.Context.RepositoryRoot,
            discovery,
            resolution,
            request.PreparationMode,
            request.NoRestore,
            request.RequestedConfiguration,
            request.RequestedTargetFramework));
    }

    private readonly record struct ComposedPolicy(
        ArchitectureContractDocument Document,
        string UnmatchedConfig,
        string PolicyConsistencyConfig,
        string CoverageConfig,
        HashSet<string>? SelectedContractIds,
        bool EnableUnmatchedIgnoreTracking);

    // Policy load, baseline merge, severity validation, and contract-ID selection depend only on
    // the policy document and the request — never on build output — so this runs exactly once per
    // snapshot even when --ensure-built later triggers a runner/session rebuild (see
    // CreateSnapshotCore). modeHint is null for a snapshot meant to serve any/all requested modes
    // later (see CreateSnapshot) and the specific mode for the single-mode Validate path, so
    // contract-ID selection validates against exactly the same catalog it did before this change
    // for single-mode callers.
    private ComposedPolicy ComposeDocument(AnalysisSnapshotRequest request, string? modeHint, ValidationTiming? timing)
    {
        ArchitectureContractDocument document =
            runnerSetupService.LoadDocument(request.PolicyPath, request.BaselinePath, timing);

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
        ComposedPolicy policy, AnalysisSnapshotRequest request, string? modeHint, ValidationTiming? timing)
    {
        return runnerSetupService.BuildRunner(
            policy.Document,
            request.PolicyPath,
            request.ConditionSetName,
            request.PreprocessorSymbols,
            policy.SelectedContractIds,
            policy.EnableUnmatchedIgnoreTracking,
            timing,
            modeHint);
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
}
