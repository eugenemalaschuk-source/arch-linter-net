using System.Reflection;
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
    private readonly string _repositoryRoot;
    private ArchitectureRunnerSetup? _setup;
    private readonly BuildStatePreflightResult _preflight;
    private readonly string _unmatchedConfig;
    private readonly string _policyConsistencyConfig;
    private readonly string _coverageConfig;
    private readonly bool _enforceUnmatchedIgnoredViolationsPolicy;
    private readonly bool _includeAsmdefContracts;
    private readonly IArchitectureContractExecutor _contractExecutor;
    private readonly IArchitectureContractHandlerRegistry _handlerRegistry;
    private readonly IReadOnlyCollection<string>? _requestedContractIds;
    private readonly object _gate = new();
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
        IArchitectureContractHandlerRegistry handlerRegistry,
        int policyCompositions,
        int projectGraphEvaluations,
        int assemblyLoads,
        IReadOnlyCollection<string>? requestedContractIds = null)
    {
        _document = document;
        _setup = setup;
        _repositoryRoot = setup.RepositoryRoot;
        _preflight = preflight;
        _unmatchedConfig = unmatchedConfig;
        _policyConsistencyConfig = policyConsistencyConfig;
        _coverageConfig = coverageConfig;
        _enforceUnmatchedIgnoredViolationsPolicy = enforceUnmatchedIgnoredViolationsPolicy;
        _includeAsmdefContracts = includeAsmdefContracts;
        _contractExecutor = contractExecutor;
        _handlerRegistry = handlerRegistry;
        _requestedContractIds = requestedContractIds;

        _counters = new ArchitectureAnalysisSnapshotCounters
        {
            PolicyCompositions = policyCompositions,
            ProjectGraphEvaluations = projectGraphEvaluations,
            AssemblyLoads = assemblyLoads
        };
    }

    public string RepositoryRoot => _repositoryRoot;

    public BuildStatePreflightResult Preflight => _preflight;

    // A blocked preflight is a failed session: no mode may execute contracts against it.
    public bool Failed => _preflight.Blocked;

    public ArchitectureAnalysisSnapshotCounters Counters
    {
        get
        {
            lock (_gate)
            {
                return _counters;
            }
        }
    }

    public bool IsDisposed
    {
        get
        {
            lock (_gate)
            {
                return _disposed;
            }
        }
    }

    public ValidationOutcome Evaluate(string mode, ValidationTiming? timing = null)
    {
        if (mode is not ("strict" or "audit"))
        {
            throw new ArgumentException($"Invalid mode: {mode}. Use 'strict' or 'audit'.", nameof(mode));
        }

        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            if (_evaluatedModes.TryGetValue(mode, out ValidationOutcome? cached))
            {
                return cached;
            }

            try
            {
                // A snapshot meant to serve any/all requested modes validates a --contract-id filter
                // against the union of strict and audit IDs at construction time (see
                // ArchitectureValidationApplicationService.ResolveSelectedContractIds) — that only rejects
                // an ID unknown to every mode. An ID valid in one mode but not this one would otherwise
                // silently match nothing when this mode's contracts execute, instead of failing the same
                // way an independent single-mode Validate call for this mode would. Re-validating here,
                // per mode, keeps combined execution semantically equivalent to separate runs.
                EnsureRequestedContractIdsAreKnownForMode(mode);

                ValidationOutcome outcome = _preflight.Blocked ? BuildBlockedOutcome() : EvaluateCore(mode, timing);

                _evaluatedModes[mode] = outcome;
                _counters = _counters with { ModesEvaluated = _evaluatedModes.Count };
                return outcome;
            }
            catch (Exception ex) when (ex is not ArchitecturePolicyValidationException)
            {
                // ArchitecturePolicyValidationException is excluded — it's already seam-safe
                // (ArchLinterNet.Core.Model) and already carries its own Diagnostic, which hosts
                // pattern-match on directly for structured formatting; wrapping it here would only
                // hide that shape behind .InnerException for no benefit.
                //
                // Policy composition and assembly resolution already succeeded by this point (this
                // snapshot was built from them) — attach that already-known provenance to whatever
                // else fails during evaluation itself (contract execution, expression evaluation) so
                // a host reporting the exception via a file sink can avoid overwriting one of those
                // inputs with the error document, the same way a policy-load failure's own
                // diagnostic already protects its inputs. ArchitectureAnalysisEvaluationException
                // derives from InvalidOperationException, so callers matching on that (or on
                // .Message) keep working unchanged.
                throw new ArchitectureAnalysisEvaluationException(
                    ex.Message, ex, GetPolicyImportPaths(), GetResolvedAssemblyPaths(), GetDiscoveredProjectPaths());
            }
        }
    }

    private void EnsureRequestedContractIdsAreKnownForMode(string mode)
    {
        if (_requestedContractIds is not { Count: > 0 })
        {
            return;
        }

        ArchitectureContractCatalog catalog = ArchitectureContractCatalog.Build(_document);
        HashSet<string> availableIds = catalog.AvailableContractIds(mode);
        List<string> unknownIds = _requestedContractIds
            .Where(id => !availableIds.Contains(id))
            .ToList();

        if (unknownIds.Count > 0)
        {
            throw new InvalidOperationException(
                $"Unknown contract IDs: {string.Join(", ", unknownIds)}{Environment.NewLine}" +
                $"Available IDs in {mode} mode: {string.Join(", ", availableIds.OrderBy(id => id))}");
        }
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
            PreflightBlocked = true,
            PolicyImportPaths = GetPolicyImportPaths(),
            ResolvedAssemblyPaths = GetResolvedAssemblyPaths(),
            DiscoveredProjectPaths = GetDiscoveredProjectPaths()
        };
    }

    private ValidationOutcome EvaluateCore(string mode, ValidationTiming? timing)
    {
        IArchitectureContractRunner runner = _setup?.Runner
            ?? throw new ObjectDisposedException(nameof(ArchitectureAnalysisSnapshot));
        List<ArchitectureViolation> allViolations = new();

        // ArchitectureAnalysisSession.UnmatchedIgnoredViolations is one mutable list that every
        // contract check across every mode appends to as it runs against the session shared by
        // this snapshot — it is never cleared between modes. Recording the count here and slicing
        // from it after this mode's checks run (see ResolveUnmatchedIgnoredViolations) isolates
        // each mode's reported unmatched-ignore diagnostics to what that mode's own checks added,
        // regardless of evaluation order or how many other modes were evaluated on this snapshot
        // before or after.
        int unmatchedStartIndex = runner.UnmatchedIgnoredViolations.Count;

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
            unmatched = ResolveUnmatchedIgnoredViolations(runner, unmatchedStartIndex);
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
            PreflightDiagnostics = _preflight.Diagnostics,
            PolicyImportPaths = GetPolicyImportPaths(),
            ResolvedAssemblyPaths = GetResolvedAssemblyPaths(),
            DiscoveredProjectPaths = GetDiscoveredProjectPaths()
        };
    }

    private IReadOnlyList<string> GetPolicyImportPaths()
    {
        return _document.Provenance.Sources
            .Select(source => Path.GetFullPath(Path.Combine(_repositoryRoot, source.SourcePath)))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private IReadOnlyList<string> GetResolvedAssemblyPaths()
    {
        IArchitectureContractRunner? runner = _setup?.Runner;
        if (runner is null)
        {
            return Array.Empty<string>();
        }

        return runner.Session.Context.TargetAssemblies
            .Select(SafeAssemblyLocation)
            .Where(path => !string.IsNullOrEmpty(path))
            .Select(path => Path.GetFullPath(path!))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private IReadOnlyList<string> GetDiscoveredProjectPaths()
    {
        return _setup?.Runner.Session.Context.DiscoveredProjectPaths ?? Array.Empty<string>();
    }

    private static string? SafeAssemblyLocation(Assembly assembly)
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

    private IReadOnlyList<ArchitectureUnmatchedIgnoredViolation> ResolveUnmatchedIgnoredViolations(
        IArchitectureContractRunner runner, int unmatchedStartIndex)
    {
        if (!_enforceUnmatchedIgnoredViolationsPolicy || _unmatchedConfig == "off")
        {
            return Array.Empty<ArchitectureUnmatchedIgnoredViolation>();
        }

        IReadOnlyList<ArchitectureUnmatchedIgnoredViolation> all = runner.UnmatchedIgnoredViolations;
        return unmatchedStartIndex >= all.Count
            ? Array.Empty<ArchitectureUnmatchedIgnoredViolation>()
            : all.Skip(unmatchedStartIndex).ToList();
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
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _evaluatedModes.Clear();
            ArchitectureRunnerSetup? setup = _setup;
            _setup = null;
            setup?.Runner.Session.Context.Dispose();
        }
    }
}
