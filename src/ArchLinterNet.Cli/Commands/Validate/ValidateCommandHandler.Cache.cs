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
// discovered projects are all #406 VerifiedCacheEligible. The manifest collector proves only
// statically inspectable inputs; any unverified input remains cache-ineligible.
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
            WriteImmediateError(options, $"Cannot use --cache '{options.CacheDestination}': {ex.Message}");
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

    // Populates only after a completed, non-cancelled run. A preflight-blocked outcome reaches
    // this boundary only to record its typed IncompleteOriginalRun rejection; it can never be
    // published. One entry per requested mode (see finding #4: a combined
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

        state.Lookups = counters.CacheLookups;

        AnalysisCachePopulation.Outcome populationOutcome;
        try
        {
            populationOutcome = AnalysisCachePopulation.TryPopulateCompletedOutcome(outcome, _cancellationToken);
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

        if (!populationOutcome.PopulationAttempted)
        {
            return;
        }

        state.AttemptedPopulation = true;

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

    private static AnalysisProfileCacheCounters BuildCacheProfileCounters(
        ValidateCommandOptions options, CacheExecutionState state, ArchitectureAnalysisSnapshotCounters? counters)
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
            // Finding #8: the scalar Rejects total must aggregate both sides — population-side
            // rejects (state.Rejects, via RecordCacheReject) and read-side/lookup rejects
            // (lookups.Rejects) — the same way rejectReasonCounts above already merges both. A
            // corrupt lookup alone (Lookups=1, one Corrupt reason count, zero population attempts)
            // must report Rejects=1, not 0.
            Rejects = state.Rejects + (lookups?.Rejects ?? 0),
            BytesRead = lookups?.BytesRead ?? 0,
            BytesWritten = state.BytesWritten,
            IneligibleUnitCount = state.IneligibleUnitCount + (lookups?.IneligibleUnitCount ?? 0),
            CorruptionEvents = AnalysisCacheCorruptionClassifier.CountCorruptionEvents(rejectReasonCounts),
            CancelledBeforePublish = state.CancelledBeforePublish,
            AvoidedAssemblyLoads = counters?.AvoidedAssemblyLoads ?? 0,
            AvoidedFactIndexMaterializations = counters?.AvoidedFactIndexMaterializations ?? 0,
            AvoidedSourceScanPasses = counters?.AvoidedSourceScanPasses ?? 0,
            AvoidedContractExecutions = counters?.AvoidedContractExecutions ?? 0,
            AvoidedArtifactBytesLoaded = counters?.AvoidedArtifactBytesLoaded ?? 0,
            Mode = ResolveCacheOptions(options).ModeCategory,
            RejectReasonCounts = rejectReasonCounts,
        };
    }
}
