using System.Reflection;
using ArchLinterNet.Core.BuildState;
using ArchLinterNet.Core.Caching;
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
    private readonly AnalysisSnapshotCacheContext? _cacheContext;
    private readonly AnalysisSessionProfilingCounters _profilingCounters;
    private readonly object _gate = new();
    private readonly Dictionary<string, ValidationOutcome> _evaluatedModes = new(StringComparer.Ordinal);
    private readonly AnalysisCacheLookupStats _cacheStats = new();
    private ArchitectureAnalysisSnapshotCounters _counters;
    private bool _disposed;
    private bool _cancelled;

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
        IReadOnlyCollection<string>? requestedContractIds = null,
        AnalysisSnapshotCacheContext? cacheContext = null)
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
        _cacheContext = cacheContext;
        _profilingCounters = setup.Runner.Session.Context.ProfilingCounters;

        _counters = new ArchitectureAnalysisSnapshotCounters
        {
            PolicyCompositions = policyCompositions,
            ProjectGraphEvaluations = projectGraphEvaluations,
            AssemblyLoads = assemblyLoads,
            DiscoveredProjectCount = setup.Runner.Session.Context.ProjectDiscovery?.DiscoveredProjects.Count ?? 0,
            RetainedAssemblyCount = setup.Runner.Session.Context.TargetAssemblies.Count,
            SelectedAssemblyCount = setup.Runner.Session.Context.TargetAssemblies.Count
                + setup.Runner.Session.Context.MissingAssemblyNames.Count,
            SnapshotMaterializations = 1,
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
                Dictionary<string, int> contractFamilyResultCounts =
                    new(_counters.ContractFamilyResultCounts, StringComparer.Ordinal);
                foreach ((string family, int count) in _profilingCounters.GetContractFamilyResultCounts())
                {
                    contractFamilyResultCounts.TryGetValue(family, out int current);
                    contractFamilyResultCounts[family] = current + count;
                }

                return _counters with
                {
                    FactIndexMaterializations = _profilingCounters.FactIndexMaterializations,
                    SourceScanPasses = _profilingCounters.SourceScanPasses,
                    SourceFilesScanned = _profilingCounters.SourceFilesScanned,
                    ContractFamilyResultCounts = contractFamilyResultCounts,
                    CacheLookups = _cacheContext is null ? null : _cacheStats.Snapshot(),
                };
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

    // Real analysis-cache/v1 lookup instrumentation for whatever this snapshot's Evaluate calls
    // actually did — see AnalysisProfileCacheCounters, which ValidateCommandHandler.Profile.cs and
    // ArchitectureValidationBuilder now source Lookups/Hits/Misses/BytesRead from instead of leaving
    // them at 0.
    public AnalysisCacheLookupStats CacheStats
    {
        get
        {
            lock (_gate)
            {
                return _cacheStats.Snapshot();
            }
        }
    }

    // Set once cancellation is observed during any Evaluate() call on this snapshot. A cancelled
    // snapshot is never reusable — see openspec/specs/analysis-build-state-fingerprints/spec.md,
    // "CLI and Testing share ownership semantics" (a cancelled snapshot's reuse is rejected).
    public bool Cancelled
    {
        get
        {
            lock (_gate)
            {
                return _cancelled;
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
            if (_cancelled)
            {
                throw new OperationCanceledException(
                    "This snapshot observed cancellation during a prior Evaluate() call and cannot be reused.");
            }

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

                ValidationOutcome? cachedOutcome = _preflight.Blocked ? null : TryEvaluateFromCache(mode, timing);
                ValidationOutcome outcome = cachedOutcome
                    ?? (_preflight.Blocked ? BuildBlockedOutcome() : EvaluateCore(mode, timing));

                _evaluatedModes[mode] = outcome;
                _counters = _counters with { ModesEvaluated = _evaluatedModes.Count };
                return outcome;
            }
            catch (OperationCanceledException)
            {
                // Mark the snapshot cancelled before rethrowing so no later Evaluate() call (for
                // this or another mode) can proceed against a session that stopped mid-evaluation —
                // see the Cancelled entry guard above. Rethrown raw, not wrapped, for the same
                // reason ArchitecturePolicyValidationException is excluded below.
                _cancelled = true;
                throw;
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
            RepositoryRoot = _repositoryRoot,
            PreflightDiagnostics = _preflight.Diagnostics,
            PreflightBlocked = true,
            PolicyImportPaths = GetPolicyImportPaths(),
            ResolvedAssemblyPaths = GetResolvedAssemblyPaths(),
            DiscoveredProjectPaths = GetDiscoveredProjectPaths(),
            SourceExpansion = _document.SourceExpansion
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

        // Same rationale as unmatchedStartIndex above: SubtractiveMatcherParticipation is one
        // mutable list shared across every mode evaluated on this snapshot's session.
        int subtractiveMatcherStartIndex = runner.Session.SubtractiveMatcherParticipation.Count;

        CancellationToken cancellationToken = runner.Session.Context.CancellationToken;
        cancellationToken.ThrowIfCancellationRequested();

        using (timing?.Measure("configuration_check"))
            allViolations.AddRange(runner.CheckConfiguration(strict: mode == "strict"));

        cancellationToken.ThrowIfCancellationRequested();

        List<PolicyConsistencyDiagnostic> policyConsistencyFindings;
        using (timing?.Measure("policy_consistency_check"))
        {
            policyConsistencyFindings = _policyConsistencyConfig == "off"
                ? new List<PolicyConsistencyDiagnostic>()
                : runner.CheckPolicyConsistency();
        }

        cancellationToken.ThrowIfCancellationRequested();

        ArchitectureContractExecutionResult execution;
        using (timing?.Measure("contract_checks"))
        {
            _profilingCounters.ResetContractFamilyResultCounts();
            execution = _contractExecutor.Execute(
                runner.Session, mode, _handlerRegistry, _includeAsmdefContracts, timing);
        }

        RecordContractFamilyResultCounts(execution.ContractFamilyResultCounts);
        _profilingCounters.ResetContractFamilyResultCounts();

        allViolations.AddRange(execution.Violations);

        cancellationToken.ThrowIfCancellationRequested();

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

        // Classification post-processing can materialize additional facts. A signal observed
        // there must win over constructing and returning an apparently complete outcome.
        cancellationToken.ThrowIfCancellationRequested();

        return new ValidationOutcome(
            passed, allViolations, execution.Cycles, coverageFindings, _coverageConfig, unmatched, _unmatchedConfig,
            policyConsistencyFindings, _policyConsistencyConfig, execution.CoverageSummaries,
            classificationConflicts, classificationMetadataFailures)
        {
            RepositoryRoot = _repositoryRoot,
            CycleFindings = execution.CycleFindings,
            ClassificationRoles = classificationRoles,
            ClassificationPathDeferred = classificationPathDeferred,
            PreflightDiagnostics = _preflight.Diagnostics,
            PolicyImportPaths = GetPolicyImportPaths(),
            ResolvedAssemblyPaths = GetResolvedAssemblyPaths(),
            DiscoveredProjectPaths = GetDiscoveredProjectPaths(),
            SourceExpansion = _document.SourceExpansion,
            SubtractiveMatcherParticipation = runner.Session.SubtractiveMatcherParticipation
                .Skip(subtractiveMatcherStartIndex)
                .ToList()
        };
    }

    // The real cache-hit short-circuit (issue #365's deferred follow-up, now implemented): when
    // this run configured a cache location, a hit here reconstructs a ValidationOutcome directly
    // from the persisted AnalysisCacheOutcomeV1 and skips EvaluateCore entirely — configuration_check,
    // policy_consistency_check, contract_checks (contract execution, including source scanning and
    // coverage/classification computation) and post_processing never run for this mode. Policy
    // composition, project discovery, and assembly loading already happened in
    // ArchitectureValidationApplicationService.BuildSnapshot before this snapshot existed — they are
    // not skipped, since #406 per-project manifest recomputation (the actual reuse-authorization
    // proof) requires the discovered project set to already be known, and every requested mode
    // shares that one snapshot's setup regardless of hit/miss. Returns null on Miss/Reject so the
    // caller falls back to the real pipeline exactly as before this change.
    // Finding #3: the session cancellation token is threaded through the entire lookup —
    // ComputePolicyDigest (which hashes every policy file's content and can be a real I/O-bound
    // operation for a large import graph) and TryLookup (which recomputes a #406 manifest per
    // discovered project) both observe it, and a hit is never accepted once cancellation has been
    // requested. Falling back to null here (rather than accepting a stale/racy hit) means Evaluate's
    // caller proceeds into EvaluateCore, whose own ThrowIfCancellationRequested calls immediately
    // surface the real OperationCanceledException — cancellation here never silently turns into an
    // unexplained cache-derived result, it just defers to the same cancellation path recomputation
    // already uses.
    private ValidationOutcome? TryEvaluateFromCache(string mode, ValidationTiming? timing)
    {
        if (_cacheContext is not { } cache)
        {
            return null;
        }

        CancellationToken cancellationToken = _setup?.Runner.Session.Context.CancellationToken ?? default;

        IReadOnlyList<string> discoveredProjectPaths = GetDiscoveredProjectPaths();
        IReadOnlyList<string> policyImportPaths = GetPolicyImportPaths();

        AnalysisCacheKey key = new(
            AnalysisCacheKey.ComputePolicyDigest(policyImportPaths, _repositoryRoot, cancellationToken),
            AnalysisCacheKey.NormalizeMode(mode),
            cache.ConditionSetName,
            AnalysisCacheKey.ComputeContractIdsDigest(cache.ContractIds),
            AnalysisCacheKey.ComputeWorkspaceDigest(discoveredProjectPaths, _repositoryRoot),
            cache.Configuration,
            cache.TargetFramework,
            cache.Platform,
            cache.RuntimeIdentifier,
            AnalysisCacheKey.ComputePreprocessorSymbolsDigest(cache.PreprocessorSymbols),
            AnalysisCacheKey.ComputeBaselineDigest(cache.BaselinePath, cancellationToken),
            _includeAsmdefContracts,
            _enforceUnmatchedIgnoredViolationsPolicy);

        AnalysisCacheLookupResult lookup;
        using (timing?.Measure("cache_lookup"))
        {
            lookup = AnalysisCachePopulation.TryLookup(
                cache.Location, key, discoveredProjectPaths, _repositoryRoot,
                cache.Configuration, cache.TargetFramework, cache.Platform, cache.RuntimeIdentifier, cancellationToken);
        }

        lock (_gate)
        {
            _cacheStats.RecordLookup(lookup);
        }

        if (lookup.Outcome != AnalysisCacheLookupOutcome.Hit || lookup.Entry is null)
        {
            return null;
        }

        // A hit is never accepted once cancellation has been observed — this is checked after the
        // lookup completes (not merely before it starts) because ComputePolicyDigest/TryLookup
        // above can themselves take real time, and this run's own cancellation could have been
        // requested during either of them.
        if (cancellationToken.IsCancellationRequested)
        {
            return null;
        }

        return AnalysisCacheOutcomeMapper.FromCacheOutcome(
            lookup.Entry.Outcome, _repositoryRoot, policyImportPaths, GetResolvedAssemblyPaths(),
            discoveredProjectPaths, _document.SourceExpansion);
    }

    private IReadOnlyList<string> GetPolicyImportPaths()
    {
        return _document.Provenance.Sources
            .Select(source => Path.GetFullPath(Path.Combine(_repositoryRoot, source.SourcePath)))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    // This is deliberately a snapshot copy, not the internal mutable counter record. Hosts use
    // it when cancellation interrupts evaluation before a ValidationOutcome can expose inputs.
    public IReadOnlyList<string> GetProfileInputPaths() => GetPolicyImportPaths()
        .Concat(GetResolvedAssemblyPaths()
            .SelectMany(path => new[] { path, BuildReceiptStore.ReceiptPathFor(path) }))
        .Concat(GetDiscoveredProjectPaths())
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();

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

    private void RecordContractFamilyResultCounts(IReadOnlyDictionary<string, int> resultCounts)
    {
        lock (_gate)
        {
            Dictionary<string, int> totals = new(_counters.ContractFamilyResultCounts, StringComparer.Ordinal);
            foreach ((string family, int count) in resultCounts)
            {
                totals.TryGetValue(family, out int current);
                totals[family] = current + count;
            }

            _counters = _counters with { ContractFamilyResultCounts = totals };
        }
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
