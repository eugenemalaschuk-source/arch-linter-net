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

    public ValidationOutcome Validate(ValidationRequest request, ValidationTiming? timing = null)
    {
        if (request.Mode is not ("strict" or "audit"))
        {
            throw new ArgumentException($"Invalid mode: {request.Mode}. Use 'strict' or 'audit'.", nameof(request));
        }

        using (timing?.Measure("total"))
        {
            LoadAndSetupOutcome loadAndSetup = LoadAndSetup(request, timing);
            string unmatchedConfig = loadAndSetup.UnmatchedConfig;
            string policyConsistencyConfig = loadAndSetup.PolicyConsistencyConfig;
            string coverageConfig = loadAndSetup.CoverageConfig;

            IArchitectureContractRunner runner = loadAndSetup.Setup.Runner;

            BuildStatePreflightResult preflight;
            using (timing?.Measure("build_state_preflight"))
                preflight = RunBuildStatePreflight(request, runner);

            if (preflight.Blocked)
            {
                return new ValidationOutcome(
                    false, Array.Empty<ArchitectureViolation>(), Array.Empty<string>(),
                    Array.Empty<ArchitectureViolation>(), coverageConfig,
                    Array.Empty<ArchitectureUnmatchedIgnoredViolation>(), unmatchedConfig,
                    Array.Empty<PolicyConsistencyDiagnostic>(), policyConsistencyConfig,
                    Array.Empty<ArchitectureCoverageSummary>(), Array.Empty<ArchitectureClassificationConflict>(),
                    Array.Empty<ArchitectureClassificationMetadataFailure>())
                {
                    PreflightDiagnostics = preflight.Diagnostics,
                    PreflightBlocked = true
                };
            }

            // --ensure-built may have just written new build output that the runner/session
            // above — constructed during LoadAndSetup, before this build ran — cannot see: its
            // ArchitectureAnalysisContext captured whatever assembly resolution found (or failed
            // to find) at that earlier point in time. Re-running setup after a successful build
            // re-discovers and re-resolves from the now-current filesystem state, so contract
            // execution below actually analyzes the artifact preflight just verified rather than
            // silently continuing to analyze stale or missing state from before the build.
            if (request.PreparationMode == BuildPreparationMode.EnsureBuilt
                && runner.Session.Context.ProjectDiscovery is { DiscoveredProjects.Count: > 0 })
            {
                using (timing?.Measure("post_ensure_built_reload"))
                {
                    loadAndSetup = LoadAndSetup(request, timing);
                    unmatchedConfig = loadAndSetup.UnmatchedConfig;
                    policyConsistencyConfig = loadAndSetup.PolicyConsistencyConfig;
                    coverageConfig = loadAndSetup.CoverageConfig;
                    runner = loadAndSetup.Setup.Runner;
                }
            }

            List<ArchitectureViolation> allViolations = new();

            using (timing?.Measure("configuration_check"))
                allViolations.AddRange(runner.CheckConfiguration(strict: request.Mode == "strict"));

            List<PolicyConsistencyDiagnostic> policyConsistencyFindings;
            using (timing?.Measure("policy_consistency_check"))
            {
                policyConsistencyFindings = policyConsistencyConfig == "off"
                    ? new List<PolicyConsistencyDiagnostic>()
                    : runner.CheckPolicyConsistency();
            }

            ArchitectureContractExecutionResult execution;
            using (timing?.Measure("contract_checks"))
            {
                execution = contractExecutor.Execute(
                    runner.Session, request.Mode, handlerRegistry, request.IncludeAsmdefContracts, timing);
            }

            allViolations.AddRange(execution.Violations);

            IReadOnlyCollection<ArchitectureViolation> coverageFindings = coverageConfig == "off"
                ? Array.Empty<ArchitectureViolation>()
                : execution.CoverageViolations;

            IReadOnlyList<ArchitectureUnmatchedIgnoredViolation> unmatched;
            using (timing?.Measure("post_processing"))
            {
                unmatched = ResolveUnmatchedIgnoredViolations(runner, request, unmatchedConfig);
            }

            unmatched = FilterUnmatchedForDisabledCoverage(unmatched, coverageConfig);
            unmatched = unmatched.Select(loadAndSetup.Document.Provenance.Enrich).ToList();

            bool hasBlockingUnmatched = request.EnforceUnmatchedIgnoredViolationsPolicy
                && unmatchedConfig == ErrorSeverity && unmatched.Count > 0;

            bool hasBlockingPolicyConsistency =
                policyConsistencyConfig == ErrorSeverity && policyConsistencyFindings.Count > 0;

            bool hasBlockingCoverage = coverageConfig == ErrorSeverity && coverageFindings.Count > 0;

            bool passed = allViolations.Count == 0 && execution.Cycles.Count == 0
                && !hasBlockingUnmatched && !hasBlockingPolicyConsistency && !hasBlockingCoverage;

            (IReadOnlyList<ArchitectureClassificationConflict> classificationConflicts,
                IReadOnlyList<ArchitectureClassificationMetadataFailure> classificationMetadataFailures) = runner.Session.CheckClassificationFacts();
            IReadOnlyList<ArchitectureClassificationRoleFact> classificationRoles = runner.Session.CheckClassificationRoles();
            ArchitectureClassificationPathDeferredNotice? classificationPathDeferred = runner.Session.CheckClassificationPathDeferred();

            return new ValidationOutcome(
                passed, allViolations, execution.Cycles, coverageFindings, coverageConfig, unmatched, unmatchedConfig,
                policyConsistencyFindings, policyConsistencyConfig, execution.CoverageSummaries,
                classificationConflicts, classificationMetadataFailures)
            {
                CycleFindings = execution.CycleFindings,
                ClassificationRoles = classificationRoles,
                ClassificationPathDeferred = classificationPathDeferred,
                PreflightDiagnostics = preflight.Diagnostics
            };
        }
    }

    // Preflight only runs when project discovery produced a project graph — the fingerprint/
    // receipt model this needs (ArchitectureDiscoveredProject.Path/AssemblyName) has no
    // counterpart when target assemblies are configured directly via analysis.target_assemblies
    // without project discovery.
    private BuildStatePreflightResult RunBuildStatePreflight(ValidationRequest request, IArchitectureContractRunner runner)
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

    private readonly record struct LoadAndSetupOutcome(
        ArchitectureContractDocument Document,
        string UnmatchedConfig,
        string PolicyConsistencyConfig,
        string CoverageConfig,
        ArchitectureRunnerSetup Setup);

    private LoadAndSetupOutcome LoadAndSetup(ValidationRequest request, ValidationTiming? timing)
    {
        using (timing?.Measure("load_and_setup"))
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

            HashSet<string>? selectedIds = ResolveSelectedContractIds(document, request);

            bool enableUnmatchedIgnoreTracking = !request.EnforceUnmatchedIgnoredViolationsPolicy
                || unmatchedConfig != "off";

            ArchitectureRunnerSetup setup = runnerSetupService.BuildRunner(
                document,
                request.PolicyPath,
                request.ConditionSetName,
                request.PreprocessorSymbols,
                selectedIds,
                enableUnmatchedIgnoreTracking,
                timing,
                request.Mode);

            return new LoadAndSetupOutcome(document, unmatchedConfig, policyConsistencyConfig, coverageConfig, setup);
        }
    }

    private static void EnsureValidSeverityConfig(string value, string settingName)
    {
        if (value is not (ErrorSeverity or "warn" or "off"))
        {
            throw new InvalidOperationException($"Invalid {settingName}: {value}. Use 'error', 'warn', or 'off'.");
        }
    }

    private static HashSet<string>? ResolveSelectedContractIds(
        ArchitectureContractDocument document, ValidationRequest request)
    {
        if (request.ContractIds is not { Count: > 0 })
        {
            return null;
        }

        HashSet<string> selectedIds = new(request.ContractIds, StringComparer.OrdinalIgnoreCase);
        HashSet<string> availableIds = CollectAvailableContractIds(document, request.Mode);
        List<string> unknownIds = selectedIds.Where(id => !availableIds.Contains(id)).ToList();

        if (unknownIds.Count > 0)
        {
            throw new InvalidOperationException(
                $"Unknown contract IDs: {string.Join(", ", unknownIds)}{Environment.NewLine}" +
                $"Available IDs in {request.Mode} mode: {string.Join(", ", availableIds.OrderBy(id => id))}");
        }

        return selectedIds;
    }

    private static IReadOnlyList<ArchitectureUnmatchedIgnoredViolation> ResolveUnmatchedIgnoredViolations(
        IArchitectureContractRunner runner, ValidationRequest request, string unmatchedConfig)
    {
        if (!request.EnforceUnmatchedIgnoredViolationsPolicy || unmatchedConfig == "off")
        {
            return Array.Empty<ArchitectureUnmatchedIgnoredViolation>();
        }

        return runner.UnmatchedIgnoredViolations;
    }

    // When coverage gating is disabled, a stale entry in a strict_coverage/audit_coverage
    // baseline must not surface or block — otherwise turning coverage off would not fully
    // disable the coverage family, only its non-stale findings. Filtered by contract
    // group rather than contract ID: IDs are not guaranteed unique across families (the
    // policy-consistency duplicate-id check does not span coverage contracts), so an
    // id-only filter could mistakenly suppress a stale ignore on an unrelated strict/audit
    // contract that happens to share an ID with a coverage contract.
    private static IReadOnlyList<ArchitectureUnmatchedIgnoredViolation> FilterUnmatchedForDisabledCoverage(
        IReadOnlyList<ArchitectureUnmatchedIgnoredViolation> unmatched, string coverageConfig)
    {
        if (coverageConfig != "off" || unmatched.Count == 0)
        {
            return unmatched;
        }

        return unmatched
            .Where(u => u.ContractGroup is not ("strict_coverage" or "audit_coverage"))
            .ToList();
    }

    private static HashSet<string> CollectAvailableContractIds(ArchitectureContractDocument document, string mode)
    {
        return ArchitectureContractCatalog.Build(document).AvailableContractIds(mode);
    }
}
