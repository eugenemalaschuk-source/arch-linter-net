using System.Reflection;
using ArchLinterNet.Core.BuildState;
using ArchLinterNet.Core.Caching;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Execution.Abstractions;
using ArchLinterNet.Core.Execution.Results;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;

namespace ArchLinterNet.Core.Validation;

// One immutable, explicitly owned analysis snapshot for issue #363: policy composition, project
// graph evaluation, assembly load, and build-state preflight all happen once (in
// ArchitectureValidationApplicationService.CreateSnapshot) and are retained here so any number of
// requested modes (strict/audit — coverage rides inside each mode via the strict_coverage/
// audit_coverage families) can be evaluated from the same fact set. See
// docs/internal/analysis-build-state-blueprint.md, "Snapshot ownership".
public sealed partial class ArchitectureAnalysisSnapshot : IDisposable
{
    private const string ErrorSeverity = "error";

    private readonly ArchitectureContractDocument _document;
    private readonly string _repositoryRoot;
    private ArchitectureRunnerSetup? _setup;
    private readonly BuildStatePreflightResult _preflight;
    private readonly string _unmatchedConfig;
    private readonly string _policyConsistencyConfig;
    private readonly string _coverageConfig;
    private readonly DateOnly _waiverEvaluationDate;
    private readonly bool _enforceUnmatchedIgnoredViolationsPolicy;
    private readonly bool _includeAsmdefContracts;
    private readonly IArchitectureContractExecutor _contractExecutor;
    private readonly IArchitectureContractHandlerRegistry _handlerRegistry;
    private readonly IReadOnlyCollection<string>? _requestedContractIds;
    private readonly AnalysisSnapshotCacheContext? _cacheContext;
    private readonly CancellationToken _cancellationToken;
    private AnalysisSessionProfilingCounters? _profilingCounters;
    private readonly Func<ArchitectureRunnerSetup>? _materializeSetup;
    private readonly IReadOnlyList<string> _preparedArtifactPaths;
    private readonly IReadOnlyDictionary<string, string> _preparedArtifactContentDigests;
    private readonly IReadOnlyList<string> _preparedProjectPaths;
    private readonly bool _preparedArtifactClosureComplete;
    private readonly ArchitectureRunnerPreparation? _preparedPostBuildRunner;
    private readonly object _gate = new();
    private readonly Dictionary<string, ValidationOutcome> _evaluatedModes = new(StringComparer.Ordinal);
    private readonly Dictionary<string, AnalysisCachePopulation.PreparedAuthorization> _cacheAuthorizations =
        new(StringComparer.Ordinal);
    private readonly AnalysisCacheLookupStats _cacheStats = new();
    private ArchitectureAnalysisSnapshotCounters _counters;
    private bool _disposed;
    private bool _cancelled;

    internal ArchitectureAnalysisSnapshot(
        ArchitectureContractDocument document,
        ArchitectureRunnerSetup? setup,
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
        AnalysisSnapshotCacheContext? cacheContext = null,
        string? preparedRepositoryRoot = null,
        IReadOnlyList<string>? preparedArtifactPaths = null,
        IReadOnlyDictionary<string, string>? preparedArtifactContentDigests = null,
        IReadOnlyList<string>? preparedProjectPaths = null,
        bool preparedArtifactClosureComplete = true,
        ArchitectureRunnerPreparation? preparedPostBuildRunner = null,
        Func<ArchitectureRunnerSetup>? materializeSetup = null,
        CancellationToken cancellationToken = default,
        DateOnly? waiverEvaluationDate = null)
    {
        _document = document;
        _setup = setup;
        _repositoryRoot = setup?.RepositoryRoot ?? preparedRepositoryRoot
            ?? throw new ArgumentException("A prepared repository root is required when setup is lazy.", nameof(preparedRepositoryRoot));
        _preflight = preflight;
        _unmatchedConfig = unmatchedConfig;
        _policyConsistencyConfig = policyConsistencyConfig;
        _coverageConfig = coverageConfig;
        _waiverEvaluationDate = waiverEvaluationDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
        _enforceUnmatchedIgnoredViolationsPolicy = enforceUnmatchedIgnoredViolationsPolicy;
        _includeAsmdefContracts = includeAsmdefContracts;
        _contractExecutor = contractExecutor;
        _handlerRegistry = handlerRegistry;
        _requestedContractIds = requestedContractIds;
        _cacheContext = cacheContext;
        _cancellationToken = cancellationToken;
        _profilingCounters = setup?.Runner.Session.Context.ProfilingCounters;
        _materializeSetup = materializeSetup;
        _preparedArtifactPaths = preparedArtifactPaths ?? Array.Empty<string>();
        _preparedArtifactContentDigests = preparedArtifactContentDigests
            ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        _preparedProjectPaths = preparedProjectPaths ?? Array.Empty<string>();
        _preparedArtifactClosureComplete = preparedArtifactClosureComplete
            && (setup is null || setup.Runner.Session.Context.SelectedAssemblyArtifactPaths.Count > 0);
        _preparedPostBuildRunner = preparedPostBuildRunner;

        _counters = new ArchitectureAnalysisSnapshotCounters
        {
            PolicyCompositions = policyCompositions,
            ProjectGraphEvaluations = projectGraphEvaluations,
            AssemblyLoads = assemblyLoads,
            DiscoveredProjectCount = setup?.Runner.Session.Context.ProjectDiscovery?.DiscoveredProjects.Count
                ?? _preparedProjectPaths.Count,
            RetainedAssemblyCount = setup?.Runner.Session.Context.TargetAssemblies.Count ?? 0,
            SelectedAssemblyCount = setup is null
                ? _preparedArtifactPaths.Count
                : setup.Runner.Session.Context.TargetAssemblies.Count + setup.Runner.Session.Context.MissingAssemblyNames.Count,
            SnapshotMaterializations = 1,
            MaxParallelism = setup?.Runner.Session.Context.MaxParallelism ?? 0,
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
                foreach ((string family, int count) in _profilingCounters?.GetContractFamilyResultCounts()
                         ?? new Dictionary<string, int>(StringComparer.Ordinal))
                {
                    contractFamilyResultCounts.TryGetValue(family, out int current);
                    contractFamilyResultCounts[family] = current + count;
                }

                return _counters with
                {
                    FactIndexMaterializations = _profilingCounters?.FactIndexMaterializations ?? 0,
                    SourceScanPasses = _profilingCounters?.SourceScanPasses ?? 0,
                    SourceFilesScanned = _profilingCounters?.SourceFilesScanned ?? 0,
                    ContractFamilyResultCounts = contractFamilyResultCounts,
                    CacheLookups = _cacheContext is null ? null : _cacheStats.Snapshot(),
                    ParallelScheduledWorkItems = _profilingCounters?.ParallelScheduledWorkItems ?? 0,
                    ParallelCompletedWorkItems = _profilingCounters?.ParallelCompletedWorkItems ?? 0,
                    ParallelObservedMaxConcurrency = _profilingCounters?.ParallelObservedMaxConcurrency ?? 0,
                    ParallelMergeOperations = _profilingCounters?.ParallelMergeOperations ?? 0,
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

                _cancellationToken.ThrowIfCancellationRequested();
                ValidationOutcome? cachedOutcome = _preflight.Blocked ? null : TryEvaluateFromCache(mode, timing);
                WorkSnapshot? workBefore = cachedOutcome is null && !_preflight.Blocked
                    ? CaptureWorkSnapshot()
                    : null;
                ValidationOutcome outcome = cachedOutcome
                    ?? (_preflight.Blocked ? BuildBlockedOutcome() : EvaluateCore(mode, timing));
                if (_preparedPostBuildRunner is not null)
                {
                    outcome = outcome with { PreparedPostBuildRunner = _preparedPostBuildRunner };
                }

                if (cachedOutcome is null
                    && !outcome.PreflightBlocked
                    && _cacheAuthorizations.Remove(mode, out AnalysisCachePopulation.PreparedAuthorization? authorization))
                {
                    // This opaque plan was captured before contract execution. It is associated
                    // by object identity rather than stored on ValidationOutcome itself, so its
                    // transient cache state cannot change that public record's equality contract.
                    CacheArtifactEvidence artifacts = GetCacheArtifactEvidence();
                    AnalysisCachePopulation.AttachAuthorization(
                        outcome,
                        authorization,
                        artifacts.Paths,
                        artifacts.CapturedIdentities,
                        CreateWorkProvenance(workBefore!.Value));
                }

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
        IArchitectureContractRunner runner = EnsureSetup().Runner;
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
            _profilingCounters?.ResetContractFamilyResultCounts();
            execution = _contractExecutor.Execute(
                runner.Session, mode, _handlerRegistry, _includeAsmdefContracts, timing);
        }

        RecordContractFamilyResultCounts(execution.ContractFamilyResultCounts);
        _profilingCounters?.ResetContractFamilyResultCounts();

        allViolations.AddRange(execution.Violations);

        cancellationToken.ThrowIfCancellationRequested();

        IReadOnlyCollection<ArchitectureViolation> coverageFindings = _coverageConfig == "off"
            ? Array.Empty<ArchitectureViolation>()
            : execution.CoverageViolations;

        IReadOnlyList<ArchitectureUnmatchedIgnoredViolation> rawUnmatched;
        IReadOnlyList<ArchitectureUnmatchedIgnoredViolation> unmatched;
        using (timing?.Measure("post_processing"))
        {
            IReadOnlyList<ArchitectureUnmatchedIgnoredViolation> allUnmatched = runner.UnmatchedIgnoredViolations;
            rawUnmatched = unmatchedStartIndex >= allUnmatched.Count
                ? Array.Empty<ArchitectureUnmatchedIgnoredViolation>()
                : allUnmatched.Skip(unmatchedStartIndex).ToList();
            unmatched = ResolveUnmatchedIgnoredViolations(runner, unmatchedStartIndex);
        }

        unmatched = FilterUnmatchedForDisabledCoverage(unmatched);
        unmatched = unmatched.Select(_document.Provenance.Enrich).ToList();

        bool hasBlockingUnmatched = _enforceUnmatchedIgnoredViolationsPolicy
            && _unmatchedConfig == ErrorSeverity && unmatched.Count > 0;

        bool hasBlockingPolicyConsistency =
            _policyConsistencyConfig == ErrorSeverity && policyConsistencyFindings.Count > 0;

        bool hasBlockingCoverage = _coverageConfig == ErrorSeverity && coverageFindings.Count > 0;

        IReadOnlyList<ArchitectureWaiverLifecycleRecord> waivers = ArchitectureWaiverLifecycleEvaluator.Evaluate(
            _document, mode, rawUnmatched, _waiverEvaluationDate, _requestedContractIds);
        bool hasBlockingWaiver = ArchitectureWaiverProfile.Resolve(_document) == ArchitectureWaiverProfile.Strict
            && waivers.Any(waiver => waiver.State is "expired" or "stale");

        bool passed = allViolations.Count == 0 && execution.Cycles.Count == 0
            && !hasBlockingUnmatched && !hasBlockingPolicyConsistency && !hasBlockingCoverage && !hasBlockingWaiver;

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
            Waivers = waivers,
            SubtractiveMatcherParticipation = runner.Session.SubtractiveMatcherParticipation
                .Skip(subtractiveMatcherStartIndex)
                .ToList()
        };
    }

    // A cache hit reconstructs its outcome without materializing the runner or executing contracts.
    // Metadata planning still precedes lookup; the lazy runner materializes only after a miss.
    private ValidationOutcome? TryEvaluateFromCache(string mode, ValidationTiming? timing)
    {
        if (_cacheContext is not { } cache)
        {
            return null;
        }

        // Do not spend manifest-collection work on a plan that could not independently prove
        // the current artifact closure. This is both fail-closed and observable to profile
        // consumers as the same typed cache rejection used by other incomplete inputs.
        if (!_preparedArtifactClosureComplete)
        {
            lock (_gate)
            {
                _cacheStats.RecordLookup(AnalysisCacheLookupResult.Reject(AnalysisCacheRejectReason.IneligibleBuildInput));
            }

            return null;
        }

        CancellationToken cancellationToken = _setup?.Runner.Session.Context.CancellationToken ?? _cancellationToken;
        cancellationToken.ThrowIfCancellationRequested();

        ArchitectureAnalysisContext? context = _setup?.Runner.Session.Context;
        bool cacheArtifactClosureComplete = context?.MaterializeCacheArtifactReferences(cancellationToken)
            ?? _preparedArtifactClosureComplete;

        // Some contract families (for example project-metadata contracts) use the configured
        // project paths without materializing a Roslyn project.  Those inputs are nevertheless
        // part of the cached result and must be fingerprinted before the first lookup.  Prefer
        // the materialized paths when available, but fall back to explicit analysis.projects.
        IReadOnlyList<string> cacheProjectPaths = GetCacheProjectPaths();
        IReadOnlyList<string> policyImportPaths = GetPolicyImportPaths();
        CacheKeyInputEvidence keyInputs = GetCacheKeyInputEvidence(cache);
        CacheArtifactEvidence artifacts = GetCacheArtifactEvidence();

        AnalysisCacheKey key = new(
            AnalysisCacheKey.ComputePolicyDigest(keyInputs.PolicyInputs, _repositoryRoot),
            AnalysisCacheKey.NormalizeMode(mode),
            cache.ConditionSetName,
            AnalysisCacheKey.ComputeContractIdsDigest(cache.ContractIds),
            AnalysisCacheKey.ComputeWorkspaceDigest(cacheProjectPaths, _repositoryRoot),
            cache.Configuration,
            cache.TargetFramework,
            cache.Platform,
            cache.RuntimeIdentifier,
            AnalysisCacheKey.ComputePreprocessorSymbolsDigest(cache.PreprocessorSymbols),
            keyInputs.BaselineInput?.ContentDigest ?? string.Empty,
            _includeAsmdefContracts,
            _enforceUnmatchedIgnoredViolationsPolicy,
            _waiverEvaluationDate.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture));

        AnalysisCachePopulation.LookupPreparation preparation;
        using (timing?.Measure("cache_lookup"))
        {
            preparation = AnalysisCachePopulation.TryLookupWithCapturedEvidence(
                cache.Location, key, cacheProjectPaths, artifacts.Paths, artifacts.CapturedIdentities,
                keyInputs.AllInputs, _repositoryRoot,
                cache.Configuration, cache.TargetFramework, cache.Platform, cache.RuntimeIdentifier,
                HasUnfingerprintedSourceInputs(mode) || !cacheArtifactClosureComplete || !keyInputs.IsComplete,
                cancellationToken);
        }

        AnalysisCacheLookupResult lookup = preparation.Lookup;

        lock (_gate)
        {
            _cacheStats.RecordLookup(lookup, preparation.IneligibleUnitCount);
        }

        if (lookup.Outcome != AnalysisCacheLookupOutcome.Hit || lookup.Entry is null)
        {
            if (preparation.Authorization is not null)
            {
                _cacheAuthorizations[mode] = preparation.Authorization;
            }

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

        AnalysisCacheWorkProvenanceV1 work = lookup.Entry.WorkProvenance;
        _counters = _counters with
        {
            AvoidedAssemblyLoads = _counters.AvoidedAssemblyLoads + work.AssemblyLoads,
            AvoidedFactIndexMaterializations = _counters.AvoidedFactIndexMaterializations + work.FactIndexMaterializations,
            AvoidedSourceScanPasses = _counters.AvoidedSourceScanPasses + work.SourceScanPasses,
            AvoidedContractExecutions = _counters.AvoidedContractExecutions + work.ContractExecutions,
            AvoidedArtifactBytesLoaded = _counters.AvoidedArtifactBytesLoaded + work.ArtifactBytesLoaded,
        };

        return AnalysisCacheOutcomeMapper.FromCacheOutcome(
            lookup.Entry.Outcome, _repositoryRoot, policyImportPaths, GetResolvedAssemblyPaths(),
            GetDiscoveredProjectPaths(), _document.SourceExpansion);
    }

    private List<string> GetPolicyImportPaths()
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

    private CacheArtifactEvidence GetCacheArtifactEvidence()
    {
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var captures = new Dictionary<string, AnalysisCacheCapturedFileIdentity>(StringComparer.OrdinalIgnoreCase);

        foreach (ArchitectureLoadedAssemblyArtifact artifact in GetLoadedAssemblyArtifacts())
        {
            string assemblyPath = Path.GetFullPath(artifact.AssemblyPath);
            string pdbPath = Path.ChangeExtension(assemblyPath, ".pdb");
            AddCaptured(assemblyPath, artifact.AssemblyContentDigest);
            AddCaptured(pdbPath, artifact.PdbContentDigest);
            paths.Add(BuildReceiptStore.ReceiptPathFor(assemblyPath));
        }

        // Non-isolated assemblies have no stream capture. Retain the old FromPath behavior for
        // those paths; post-build assemblies and their cache-specific reference closure above use
        // their exact in-memory byte identities instead.
        foreach (string path in GetSelectedAssemblyArtifactPaths())
        {
            string assemblyPath = Path.GetFullPath(path);
            paths.Add(assemblyPath);
            paths.Add(Path.ChangeExtension(assemblyPath, ".pdb"));
            paths.Add(BuildReceiptStore.ReceiptPathFor(assemblyPath));
        }

        // Keep the independently planned metadata closure in the authorization even after a
        // miss materializes a narrower CLR-root set. Cache entries validate this current-run
        // plan; they never get to select a replacement closure themselves.
        foreach (string path in _preparedArtifactPaths)
        {
            string assemblyPath = Path.GetFullPath(path);
            paths.Add(assemblyPath);
            paths.Add(Path.ChangeExtension(assemblyPath, ".pdb"));
            paths.Add(BuildReceiptStore.ReceiptPathFor(assemblyPath));
        }

        foreach ((string path, string digest) in _preparedArtifactContentDigests)
        {
            AddCaptured(path, digest);
        }

        return new CacheArtifactEvidence(paths.ToArray(), captures.Values.ToArray());

        void AddCaptured(string path, string contentDigest)
        {
            string fullPath = Path.GetFullPath(path);
            paths.Add(fullPath);
            captures[fullPath] = AnalysisCacheCapturedFileIdentity.FromPath(fullPath, contentDigest);
        }
    }

    private CacheKeyInputEvidence GetCacheKeyInputEvidence(AnalysisSnapshotCacheContext cache)
    {
        ArchitectureLoadedTextIdentity[] policyInputs = _document.Provenance.SourceContentIdentities.ToArray();
        bool complete = policyInputs.Length == _document.Provenance.Sources.Count;
        ArchitectureLoadedTextIdentity? baselineInput = null;

        if (cache.BaselinePath is not null)
        {
            baselineInput = _document.BaselineContentIdentity;
            complete &= baselineInput is not null;
        }

        ArchitectureLoadedTextIdentity[] allInputs = baselineInput is null
            ? policyInputs
            : policyInputs.Append(baselineInput).ToArray();
        return new CacheKeyInputEvidence(policyInputs, baselineInput, allInputs, complete);
    }

    // Project-aware Roslyn method-body analysis lazily evaluates a project's complete source and
    // reference set. Until that dynamic set is captured as exact byte manifests, it is unsafe to
    // authorize a cached outcome from only selected PE/PDB/receipt fingerprints; fail closed.
    // Explicit analysis.source_roots are likewise intentionally cache-ineligible because they can
    // include files outside discovered project manifests. Project discovery also synthesizes
    // source roots for execution; those roots have no policy provenance and are covered by their
    // corresponding project manifests, so they must not make an otherwise metadata-only run
    // ineligible.
    private bool HasUnfingerprintedSourceInputs(string mode) => HasExplicitSourceRoots()
        || _document.Contracts.StrictMethodBody.Count > 0
        || _document.Contracts.AuditMethodBody.Count > 0
        || HasSelectedAsmdefContracts(mode);

    private bool HasSelectedAsmdefContracts(string mode)
    {
        if (!_includeAsmdefContracts || _setup is not { } setup)
        {
            return false;
        }

        ArchitectureContractCatalog catalog = ArchitectureContractCatalog.Build(_document);
        return catalog.ContractsFor(mode, "asmdef").Any(setup.Runner.Session.IsContractSelected);
    }

    private bool HasExplicitSourceRoots() => _document.Provenance.TryGetLocation(
        "/analysis/source_roots", out _);

    private List<string> GetResolvedAssemblyPaths()
    {
        return GetSelectedAssemblyArtifactPaths()
            .Concat(_setup?.Runner.Session.Context.TargetAssemblies
            .Select(SafeAssemblyLocation)
            .Where(path => !string.IsNullOrEmpty(path))
            .Select(path => Path.GetFullPath(path!))
            ?? Array.Empty<string>())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private IReadOnlyList<string> GetSelectedAssemblyArtifactPaths() =>
        _setup?.Runner.Session.Context.SelectedAssemblyArtifactPaths ?? _preparedArtifactPaths;

    private IReadOnlyList<ArchitectureLoadedAssemblyArtifact> GetLoadedAssemblyArtifacts() =>
        _setup?.Runner.Session.Context.LoadedAssemblyArtifacts ?? Array.Empty<ArchitectureLoadedAssemblyArtifact>();

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
        return _setup?.Runner.Session.Context.DiscoveredProjectPaths ?? _preparedProjectPaths;
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

    private sealed record CacheArtifactEvidence(
        IReadOnlyList<string> Paths,
        IReadOnlyList<AnalysisCacheCapturedFileIdentity> CapturedIdentities);

    private sealed record CacheKeyInputEvidence(
        IReadOnlyList<ArchitectureLoadedTextIdentity> PolicyInputs,
        ArchitectureLoadedTextIdentity? BaselineInput,
        IReadOnlyList<ArchitectureLoadedTextIdentity> AllInputs,
        bool IsComplete);

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
            _cacheAuthorizations.Clear();
            ArchitectureRunnerSetup? setup = _setup;
            _setup = null;
            setup?.Runner.Session.Context.Dispose();
        }
    }

    private ArchitectureRunnerSetup EnsureSetup()
    {
        if (_setup is not null)
        {
            return _setup;
        }

        ArchitectureRunnerSetup setup = _materializeSetup?.Invoke()
            ?? throw new ObjectDisposedException(nameof(ArchitectureAnalysisSnapshot));
        _setup = setup;
        _profilingCounters = setup.Runner.Session.Context.ProfilingCounters;
        _counters = _counters with
        {
            AssemblyLoads = _counters.AssemblyLoads + setup.AssemblyLoads,
            RetainedAssemblyCount = setup.Runner.Session.Context.TargetAssemblies.Count,
            SelectedAssemblyCount = setup.Runner.Session.Context.TargetAssemblies.Count
                + setup.Runner.Session.Context.MissingAssemblyNames.Count,
            MaxParallelism = setup.Runner.Session.Context.MaxParallelism,
        };
        return setup;
    }
}
