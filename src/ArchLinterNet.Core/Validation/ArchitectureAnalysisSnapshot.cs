using ArchLinterNet.Core.BuildState;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Execution.Abstractions;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;

namespace ArchLinterNet.Core.Validation;

// One immutable, explicitly owned analysis snapshot for issue #363: policy composition, project
// graph evaluation, assembly load, and build-state preflight all happen once (in
// ArchitectureValidationApplicationService.CreateSnapshot) and are retained here so any number of
// requested modes (strict/audit — coverage rides inside each mode via the strict_coverage/
// audit_coverage families) can be evaluated from the same fact set. See
// docs/internal/analysis-build-state-blueprint.md, "Snapshot ownership".
public sealed class ArchitectureAnalysisSnapshot : IDisposable
{
    private const string ErrorSeverity = "error";

    private readonly ArchitectureContractDocument _document;
    private readonly ArchitectureRunnerSetup _setup;
    private readonly BuildStatePreflightResult _preflight;
    private readonly string _unmatchedConfig;
    private readonly string _policyConsistencyConfig;
    private readonly string _coverageConfig;
    private readonly bool _enforceUnmatchedIgnoredViolationsPolicy;
    private readonly bool _includeAsmdefContracts;
    private readonly IArchitectureContractExecutor _contractExecutor;
    private readonly IArchitectureContractHandlerRegistry _handlerRegistry;
    private readonly Dictionary<string, ValidationOutcome> _evaluatedModes = new(StringComparer.Ordinal);
    private ArchitectureAnalysisSnapshotCounters _counters;
    private bool _disposed;

    internal ArchitectureAnalysisSnapshot(
        ArchitectureContractDocument document,
        ArchitectureRunnerSetup setup,
        BuildStatePreflightResult preflight,
        string unmatchedConfig,
        string policyConsistencyConfig,
        string coverageConfig,
        bool enforceUnmatchedIgnoredViolationsPolicy,
        bool includeAsmdefContracts,
        IArchitectureContractExecutor contractExecutor,
        IArchitectureContractHandlerRegistry handlerRegistry)
    {
        _document = document;
        _setup = setup;
        _preflight = preflight;
        _unmatchedConfig = unmatchedConfig;
        _policyConsistencyConfig = policyConsistencyConfig;
        _coverageConfig = coverageConfig;
        _enforceUnmatchedIgnoredViolationsPolicy = enforceUnmatchedIgnoredViolationsPolicy;
        _includeAsmdefContracts = includeAsmdefContracts;
        _contractExecutor = contractExecutor;
        _handlerRegistry = handlerRegistry;

        _counters = new ArchitectureAnalysisSnapshotCounters
        {
            PolicyCompositions = 1,
            ProjectGraphEvaluations = 1,
            AssemblyLoads = setup.Runner.Session.Context.TargetAssemblies.Count
        };
    }

    public IArchitectureContractRunner Runner => _setup.Runner;

    public string RepositoryRoot => _setup.RepositoryRoot;

    public BuildStatePreflightResult Preflight => _preflight;

    // A blocked preflight is a failed session: no mode may execute contracts against it.
    public bool Failed => _preflight.Blocked;

    public ArchitectureAnalysisSnapshotCounters Counters => _counters;

    public bool IsDisposed => _disposed;

    public ValidationOutcome Evaluate(string mode, ValidationTiming? timing = null)
    {
        if (mode is not ("strict" or "audit"))
        {
            throw new ArgumentException($"Invalid mode: {mode}. Use 'strict' or 'audit'.", nameof(mode));
        }

        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_evaluatedModes.TryGetValue(mode, out ValidationOutcome? cached))
        {
            return cached;
        }

        ValidationOutcome outcome = _preflight.Blocked ? BuildBlockedOutcome() : EvaluateCore(mode, timing);

        _evaluatedModes[mode] = outcome;
        _counters = _counters with { ModesEvaluated = _evaluatedModes.Count };
        return outcome;
    }

    private ValidationOutcome BuildBlockedOutcome()
    {
        return new ValidationOutcome(
            false, Array.Empty<ArchitectureViolation>(), Array.Empty<string>(),
            Array.Empty<ArchitectureViolation>(), _coverageConfig,
            Array.Empty<ArchitectureUnmatchedIgnoredViolation>(), _unmatchedConfig,
            Array.Empty<PolicyConsistencyDiagnostic>(), _policyConsistencyConfig,
            Array.Empty<ArchitectureCoverageSummary>(), Array.Empty<ArchitectureClassificationConflict>(),
            Array.Empty<ArchitectureClassificationMetadataFailure>())
        {
            PreflightDiagnostics = _preflight.Diagnostics,
            PreflightBlocked = true
        };
    }

    private ValidationOutcome EvaluateCore(string mode, ValidationTiming? timing)
    {
        IArchitectureContractRunner runner = _setup.Runner;
        List<ArchitectureViolation> allViolations = new();

        using (timing?.Measure("configuration_check"))
            allViolations.AddRange(runner.CheckConfiguration(strict: mode == "strict"));

        List<PolicyConsistencyDiagnostic> policyConsistencyFindings;
        using (timing?.Measure("policy_consistency_check"))
        {
            policyConsistencyFindings = _policyConsistencyConfig == "off"
                ? new List<PolicyConsistencyDiagnostic>()
                : runner.CheckPolicyConsistency();
        }

        ArchitectureContractExecutionResult execution;
        using (timing?.Measure("contract_checks"))
        {
            execution = _contractExecutor.Execute(
                runner.Session, mode, _handlerRegistry, _includeAsmdefContracts, timing);
        }

        allViolations.AddRange(execution.Violations);

        IReadOnlyCollection<ArchitectureViolation> coverageFindings = _coverageConfig == "off"
            ? Array.Empty<ArchitectureViolation>()
            : execution.CoverageViolations;

        IReadOnlyList<ArchitectureUnmatchedIgnoredViolation> unmatched;
        using (timing?.Measure("post_processing"))
        {
            unmatched = ResolveUnmatchedIgnoredViolations(runner);
        }

        unmatched = FilterUnmatchedForDisabledCoverage(unmatched);
        unmatched = unmatched.Select(_document.Provenance.Enrich).ToList();

        bool hasBlockingUnmatched = _enforceUnmatchedIgnoredViolationsPolicy
            && _unmatchedConfig == ErrorSeverity && unmatched.Count > 0;

        bool hasBlockingPolicyConsistency =
            _policyConsistencyConfig == ErrorSeverity && policyConsistencyFindings.Count > 0;

        bool hasBlockingCoverage = _coverageConfig == ErrorSeverity && coverageFindings.Count > 0;

        bool passed = allViolations.Count == 0 && execution.Cycles.Count == 0
            && !hasBlockingUnmatched && !hasBlockingPolicyConsistency && !hasBlockingCoverage;

        (IReadOnlyList<ArchitectureClassificationConflict> classificationConflicts,
            IReadOnlyList<ArchitectureClassificationMetadataFailure> classificationMetadataFailures) =
                runner.Session.CheckClassificationFacts();
        IReadOnlyList<ArchitectureClassificationRoleFact> classificationRoles = runner.Session.CheckClassificationRoles();
        ArchitectureClassificationPathDeferredNotice? classificationPathDeferred = runner.Session.CheckClassificationPathDeferred();

        return new ValidationOutcome(
            passed, allViolations, execution.Cycles, coverageFindings, _coverageConfig, unmatched, _unmatchedConfig,
            policyConsistencyFindings, _policyConsistencyConfig, execution.CoverageSummaries,
            classificationConflicts, classificationMetadataFailures)
        {
            CycleFindings = execution.CycleFindings,
            ClassificationRoles = classificationRoles,
            ClassificationPathDeferred = classificationPathDeferred,
            PreflightDiagnostics = _preflight.Diagnostics
        };
    }

    private IReadOnlyList<ArchitectureUnmatchedIgnoredViolation> ResolveUnmatchedIgnoredViolations(
        IArchitectureContractRunner runner)
    {
        if (!_enforceUnmatchedIgnoredViolationsPolicy || _unmatchedConfig == "off")
        {
            return Array.Empty<ArchitectureUnmatchedIgnoredViolation>();
        }

        return runner.UnmatchedIgnoredViolations;
    }

    // See ArchitectureValidationApplicationService.FilterUnmatchedForDisabledCoverage for why this
    // filters by contract group rather than by contract ID.
    private IReadOnlyList<ArchitectureUnmatchedIgnoredViolation> FilterUnmatchedForDisabledCoverage(
        IReadOnlyList<ArchitectureUnmatchedIgnoredViolation> unmatched)
    {
        if (_coverageConfig != "off" || unmatched.Count == 0)
        {
            return unmatched;
        }

        return unmatched
            .Where(u => u.ContractGroup is not ("strict_coverage" or "audit_coverage"))
            .ToList();
    }

    public void Dispose()
    {
        _disposed = true;
        _evaluatedModes.Clear();
    }
}
