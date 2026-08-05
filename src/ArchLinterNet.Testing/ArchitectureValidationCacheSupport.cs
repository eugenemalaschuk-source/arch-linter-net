using ArchLinterNet.Core.Caching;
using ArchLinterNet.Core.Profiling;
using ArchLinterNet.Core.Validation;

namespace ArchLinterNet.Testing;

// Shared analysis-cache/v1 population + profile-counter logic for the Testing host — one
// implementation both ArchitectureValidationBuilder.Validate() (independent per-mode runs) and
// ArchitectureValidationSnapshotSession.Evaluate() (one shared snapshot serving any number of
// modes) call, instead of two independently maintained copies. Mirrors
// ValidateCommandHandler.Cache.cs's TryPopulateCache/BuildCacheProfileCounters for the CLI host.
// See finding #7: CreateSnapshot()'s session previously never populated the cache after a miss at
// all.
internal static class ArchitectureValidationCacheSupport
{
    // Everything a population/lookup-counter attempt needs that is fixed for the lifetime of one
    // ArchitectureValidationBuilder — captured once per Validate()/CreateSnapshot() call so both
    // call sites pass identical inputs into AnalysisCacheKey/AnalysisCachePopulation.
    public readonly record struct CacheContext(
        AnalysisCacheOptions CacheOptions,
        string PolicyPath,
        string? ConditionSetName,
        IReadOnlyCollection<string>? ContractIds,
        string? RequestedConfiguration,
        string? RequestedTargetFramework,
        string? RequestedPlatform,
        string? RequestedRuntimeIdentifier,
        CancellationToken CancellationToken,
        // Finding #2: folded into the AnalysisCacheKey below so a run differing only in the
        // baseline's content or WithUnmatchedIgnoredViolationsPolicy() never reuses another run's
        // outcome. BaselinePath is content-digested (ComputeBaselineDigest), never hashed by path.
        // PreprocessorSymbols/IncludeAsmdefContracts are not builder-exposed options today (no
        // WithPreprocessorSymbols()/asmdef toggle on ArchitectureValidationBuilder) — they stay at
        // their AnalysisSnapshotRequest/ValidationRequest defaults (null, true) for every Testing
        // API run, matching what ArchitectureAnalysisSnapshot.TryEvaluateFromCache's own lookup-side
        // key computes for the same defaults.
        string? BaselinePath = null,
        bool EnforceUnmatchedIgnoredViolationsPolicy = false);

    public static AnalysisCachePopulation.Outcome TryPopulateCache(CacheContext context, string mode, ValidationOutcome outcome)
    {
        if (context.CacheOptions is null || context.CacheOptions.Mode == AnalysisCacheMode.Disabled)
        {
            return AnalysisCachePopulation.Outcome.Skipped;
        }

        try
        {
            // The snapshot performs the actual lookup with this same resolved location. Retain
            // the Testing API's former typed PathUnsafe profile result when the location was
            // rejected before snapshot construction, rather than treating that configuration
            // failure as a successful skipped population.
            _ = AnalysisCacheLocationResolver.Resolve(context.CacheOptions);
        }
        catch (AnalysisCacheLocationRejectedException)
        {
            return new AnalysisCachePopulation.Outcome(AnalysisCacheRejectReason.PathUnsafe, 0, 0, 0);
        }

        try
        {
            return AnalysisCachePopulation.TryPopulateCompletedOutcome(outcome, context.CancellationToken);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Same fail-safe rationale as ValidateCommandHandler.Cache.cs's TryPopulateCache: a
            // best-effort population side operation must never turn a completed validation into an
            // unexplained exception.
            return new AnalysisCachePopulation.Outcome(AnalysisCacheRejectReason.IneligibleBuildInput, 0, 0, 0);
        }
    }

    public static AnalysisProfileCacheCounters BuildCacheProfileCounters(
        CacheContext context, ArchitectureAnalysisSnapshotCounters counters, AnalysisCachePopulation.Outcome populationOutcome)
    {
        if (context.CacheOptions is null || context.CacheOptions.Mode == AnalysisCacheMode.Disabled)
        {
            return new AnalysisProfileCacheCounters();
        }

        AnalysisCacheLookupStats? lookups = counters.CacheLookups;
        Dictionary<string, int> rejectReasonCounts = lookups is null
            ? new Dictionary<string, int>(StringComparer.Ordinal)
            : new Dictionary<string, int>(lookups.RejectReasonCounts, StringComparer.Ordinal);
        if (populationOutcome.PopulationAttempted && populationOutcome.RejectReason is { } reason)
        {
            string key = reason.ToString();
            rejectReasonCounts.TryGetValue(key, out int existing);
            rejectReasonCounts[key] = existing + 1;
        }

        // Finding #8: Rejects aggregates both population-side rejects (this call's own populate
        // attempt) and read-side/lookup rejects (lookups.Rejects) — the scalar total must always
        // match the sum of rejectReasonCounts above, which already merges both sides.
        int populationRejects = populationOutcome.PopulationAttempted && populationOutcome.RejectReason is not null ? 1 : 0;

        return new AnalysisProfileCacheCounters
        {
            Status = AnalysisProfileReservedFieldStatus.Active,
            Lookups = lookups?.Lookups ?? 0,
            Hits = lookups?.Hits ?? 0,
            Misses = lookups?.Misses ?? 0,
            Writes = populationOutcome.PopulationAttempted && populationOutcome.RejectReason is null ? 1 : 0,
            Rejects = populationRejects + (lookups?.Rejects ?? 0),
            BytesRead = lookups?.BytesRead ?? 0,
            BytesWritten = populationOutcome.BytesWritten,
            IneligibleUnitCount = populationOutcome.IneligibleProjectCount + (lookups?.IneligibleUnitCount ?? 0),
            CorruptionEvents = AnalysisCacheCorruptionClassifier.CountCorruptionEvents(rejectReasonCounts),
            CancelledBeforePublish = populationOutcome.PopulationAttempted
                && populationOutcome.RejectReason == AnalysisCacheRejectReason.Cancelled ? 1 : 0,
            AvoidedAssemblyLoads = counters.AvoidedAssemblyLoads,
            AvoidedFactIndexMaterializations = counters.AvoidedFactIndexMaterializations,
            AvoidedSourceScanPasses = counters.AvoidedSourceScanPasses,
            AvoidedContractExecutions = counters.AvoidedContractExecutions,
            AvoidedArtifactBytesLoaded = counters.AvoidedArtifactBytesLoaded,
            Mode = context.CacheOptions.ModeCategory,
            RejectReasonCounts = rejectReasonCounts,
        };
    }
}
