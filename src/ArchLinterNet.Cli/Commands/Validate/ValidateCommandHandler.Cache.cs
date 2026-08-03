using ArchLinterNet.Core.Caching;
using ArchLinterNet.Core.Profiling;
using ArchLinterNet.Core.Validation;

namespace ArchLinterNet.Cli.Commands.Validate;

// Issue #365's --cache option. See openspec/specs/analysis-cache/spec.md.
//
// This wires configuration (disabled/auto/explicit-path resolution, safety validation), the
// cache-hit short-circuit (ArchitectureAnalysisSnapshot.Evaluate consults the cache directly — see
// ResolveCacheOptions/BuildValidationRequest passing CacheLocation through), and population
// (writing one verified entry per requested mode after a completed, non-cancelled run whose
// discovered projects are all #406 VerifiedCacheEligible — never true for this repository's own
// MSBuild evaluation today, since EvaluatedBuildInputManifestCollector always reports
// CacheIneligible/"evaluated-msbuild-evidence-incomplete"; see design.md).
internal sealed partial class ValidateCommandHandler
{
    private const string CacheDestinationAuto = "auto";

    private sealed class CacheExecutionState
    {
        public int Writes { get; set; }

        public int Rejects { get; set; }

        public long BytesWritten { get; set; }

        public int IneligibleUnitCount { get; set; }

        public int CancelledBeforePublish { get; set; }

        public Dictionary<string, int> RejectReasonCounts { get; } = new(StringComparer.Ordinal);

        public bool AttemptedPopulation { get; set; }

        // Aggregated from ArchitectureAnalysisSnapshot.Counters.CacheLookups across every mode this
        // invocation evaluated — real Lookups/Hits/Misses/BytesRead, not left at 0.
        public AnalysisCacheLookupStats? Lookups { get; set; }
    }

    private static AnalysisCacheOptions ResolveCacheOptions(ValidateCommandOptions options)
    {
        return options.CacheDestination switch
        {
            null => AnalysisCacheOptions.Disabled,
            CacheDestinationAuto => AnalysisCacheOptions.Auto,
            _ => AnalysisCacheOptions.AtPath(options.CacheDestination),
        };
    }

    // Mirrors PreValidateProfileDestination: an unsafe/unresolvable --cache location is reported
    // as a runtime error before any analysis work begins, rather than silently disabling the
    // cache or failing later mid-populate.
    private bool PreValidateCacheDestination(ValidateCommandOptions options, out AnalysisCacheLocation? location)
    {
        location = null;
        if (options.CacheDestination is null)
        {
            return true;
        }

        try
        {
            location = AnalysisCacheLocationResolver.Resolve(ResolveCacheOptions(options));
            return true;
        }
        catch (AnalysisCacheLocationRejectedException ex)
        {
            _console.Error.WriteLine($"Cannot use --cache '{options.CacheDestination}': {ex.Message}");
            return false;
        }
    }

    // Resolved once per invocation and threaded into every ValidationRequest/AnalysisSnapshotRequest
    // this command builds, so ArchitectureAnalysisSnapshot.Evaluate can attempt the real cache-hit
    // short-circuit for each requested mode. Null whenever --cache was not passed, or whenever the
    // configured location is unsafe (already reported as a runtime error before execution started).
    private AnalysisCacheLocation? ResolveCacheLocationForExecution(ValidateCommandOptions options)
    {
        if (options.CacheDestination is null)
        {
            return null;
        }

        try
        {
            return AnalysisCacheLocationResolver.Resolve(ResolveCacheOptions(options));
        }
        catch (AnalysisCacheLocationRejectedException)
        {
            return null;
        }
    }

    // Populates only after a completed, non-cancelled run — a preflight-blocked or cancelled
    // attempt never reaches here. One entry per requested mode (see finding #4: a combined
    // "strict,audit" request never collapses more than one mode's outcome under one key). Gated
    // internally on #406 VerifiedCacheEligible for every discovered project, so nothing is ever
    // persisted from an unproven build-input manifest.
    private void TryPopulateCache(
        ValidateCommandOptions options,
        string mode,
        ValidationOutcome outcome,
        ArchitectureAnalysisSnapshotCounters counters,
        CacheExecutionState state)
    {
        if (options.CacheDestination is null)
        {
            return;
        }

        AnalysisCacheLocation? location;
        try
        {
            location = AnalysisCacheLocationResolver.Resolve(ResolveCacheOptions(options));
        }
        catch (AnalysisCacheLocationRejectedException)
        {
            // Already reported as a runtime error by PreValidateCacheDestination before
            // execution started; nothing left to populate.
            RecordCacheReject(state, AnalysisCacheRejectReason.PathUnsafe);
            return;
        }

        state.AttemptedPopulation = true;
        state.Lookups = counters.CacheLookups;
        string repositoryRoot = string.IsNullOrEmpty(outcome.RepositoryRoot)
            ? Path.GetDirectoryName(Path.GetFullPath(options.PolicyPath)) ?? Environment.CurrentDirectory
            : outcome.RepositoryRoot;

        AnalysisCachePopulation.Outcome populationOutcome;
        try
        {
            AnalysisCacheKey key = BuildCacheKey(options, mode, outcome, repositoryRoot, _cancellationToken);
            AnalysisCacheOutcomeV1 cacheOutcome = AnalysisCacheOutcomeMapper.ToCacheOutcome(outcome);

            populationOutcome = AnalysisCachePopulation.TryPopulate(
                location,
                key,
                outcome.DiscoveredProjectPaths,
                repositoryRoot,
                options.Configuration,
                options.TargetFramework,
                options.Platform,
                options.RuntimeIdentifier,
                cacheOutcome,
                _cancellationToken);
        }
        catch (OperationCanceledException)
        {
            state.CancelledBeforePublish++;
            RecordCacheReject(state, AnalysisCacheRejectReason.Cancelled);
            return;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // A population attempt is a best-effort side operation for an already-completed run —
            // an unreadable policy/project file while deriving the cache key must never turn a
            // successful validation into an unexplained execution failure (see
            // openspec/specs/analysis-cache/spec.md, "Miss and reject outcomes are typed and fail
            // safe"). Nothing could be proven eligible, so this is recorded the same way an
            // ineligible build input would be.
            RecordCacheReject(state, AnalysisCacheRejectReason.IneligibleBuildInput);
            return;
        }

        state.IneligibleUnitCount += populationOutcome.IneligibleProjectCount;

        if (populationOutcome.RejectReason is { } reason)
        {
            if (reason == AnalysisCacheRejectReason.Cancelled)
            {
                state.CancelledBeforePublish++;
            }

            RecordCacheReject(state, reason);
            return;
        }

        state.Writes++;
        state.BytesWritten += populationOutcome.BytesWritten;
    }

    private static void RecordCacheReject(CacheExecutionState state, AnalysisCacheRejectReason reason)
    {
        state.Rejects++;
        string key = reason.ToString();
        state.RejectReasonCounts.TryGetValue(key, out int existing);
        state.RejectReasonCounts[key] = existing + 1;
    }

    private static AnalysisCacheKey BuildCacheKey(
        ValidateCommandOptions options, string mode, ValidationOutcome outcome, string repositoryRoot, CancellationToken cancellationToken)
    {
        IReadOnlyList<string> policyFiles = outcome.PolicyImportPaths.Count > 0
            ? outcome.PolicyImportPaths
            : new[] { options.PolicyPath };

        return new AnalysisCacheKey(
            AnalysisCacheKey.ComputePolicyDigest(policyFiles, repositoryRoot, cancellationToken),
            AnalysisCacheKey.NormalizeMode(mode),
            options.ConditionSetName,
            AnalysisCacheKey.ComputeContractIdsDigest(options.ContractIds),
            AnalysisCacheKey.ComputeWorkspaceDigest(outcome.DiscoveredProjectPaths, repositoryRoot),
            options.Configuration,
            options.TargetFramework,
            options.Platform,
            options.RuntimeIdentifier);
    }

    private static AnalysisProfileCacheCounters BuildCacheProfileCounters(ValidateCommandOptions options, CacheExecutionState state)
    {
        AnalysisCacheLookupStats? lookups = state.Lookups;
        Dictionary<string, int> rejectReasonCounts = new(state.RejectReasonCounts, StringComparer.Ordinal);
        if (lookups is not null)
        {
            foreach ((string reasonKey, int count) in lookups.RejectReasonCounts)
            {
                rejectReasonCounts.TryGetValue(reasonKey, out int existing);
                rejectReasonCounts[reasonKey] = existing + count;
            }
        }

        return new AnalysisProfileCacheCounters
        {
            Status = options.CacheDestination is null
                ? AnalysisProfileReservedFieldStatus.NotApplicable
                : AnalysisProfileReservedFieldStatus.Active,
            Lookups = lookups?.Lookups ?? 0,
            Hits = lookups?.Hits ?? 0,
            Misses = lookups?.Misses ?? 0,
            Writes = state.Writes,
            Rejects = state.Rejects,
            BytesRead = lookups?.BytesRead ?? 0,
            BytesWritten = state.BytesWritten,
            IneligibleUnitCount = state.IneligibleUnitCount,
            CorruptionEvents = CountCorruptionEvents(rejectReasonCounts),
            CancelledBeforePublish = state.CancelledBeforePublish,
            Mode = ResolveCacheOptions(options).ModeCategory,
            RejectReasonCounts = rejectReasonCounts,
        };
    }

    private static readonly string[] _corruptionReasonKeys =
    {
        nameof(AnalysisCacheRejectReason.Corrupt),
        nameof(AnalysisCacheRejectReason.Truncated),
        nameof(AnalysisCacheRejectReason.IntegrityMismatch),
        nameof(AnalysisCacheRejectReason.ForeignSchema),
    };

    private static int CountCorruptionEvents(IReadOnlyDictionary<string, int> rejectReasonCounts)
    {
        int total = 0;
        foreach (string key in _corruptionReasonKeys)
        {
            if (rejectReasonCounts.TryGetValue(key, out int count))
            {
                total += count;
            }
        }

        return total;
    }
}
