using ArchLinterNet.Cli.Abstractions;
using ArchLinterNet.Cli.Commands;
using ArchLinterNet.Core.Caching;
using ArchLinterNet.Core.Profiling;
using ArchLinterNet.Core.Validation;

namespace ArchLinterNet.Cli.Commands.Validate.Application;

// Owns cache option resolution, population, and the cache counters projected into --profile.
// The coordinator receives the one command cancellation token and never creates another
// validation or analysis lifecycle.
internal sealed class ValidateCacheCoordinator
{
    private const string CacheDestinationAuto = "auto";

    private readonly ICliConsole _console;
    private readonly CancellationToken _cancellationToken;

    public ValidateCacheCoordinator(ICliConsole console, CancellationToken cancellationToken)
    {
        _console = console;
        _cancellationToken = cancellationToken;
    }

    internal static AnalysisCacheOptions ResolveCacheOptions(ValidateCommandOptions options)
    {
        return options.CacheDestination switch
        {
            null => AnalysisCacheOptions.Disabled,
            CacheDestinationAuto => AnalysisCacheOptions.Auto,
            _ => AnalysisCacheOptions.AtPath(options.CacheDestination),
        };
    }

    // Mirrors profile destination validation: an unsafe/unresolvable --cache location is
    // reported as a runtime error before any analysis work begins.
    internal bool PreValidateCacheDestination(ValidateCommandOptions options)
    {
        if (options.CacheDestination is null)
        {
            return true;
        }

        try
        {
            _ = AnalysisCacheLocationResolver.Resolve(ResolveCacheOptions(options));
            return true;
        }
        catch (AnalysisCacheLocationRejectedException ex)
        {
            CliErrorOutputWriter.Write(
                _console, options.Format, "invalid-arguments",
                $"Cannot use --cache '{options.CacheDestination}': {ex.Message}");
            return false;
        }
    }

    // Resolved once per invocation and threaded into every ValidationRequest/AnalysisSnapshotRequest.
    internal static AnalysisCacheLocation? ResolveCacheLocationForExecution(ValidateCommandOptions options)
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

    // Populates only after a completed, non-cancelled run. Population itself gates on
    // VerifiedCacheEligible for every discovered project, so nothing is persisted from an
    // unproven build-input manifest.
    internal void TryPopulateCache(
        ValidateCommandOptions options,
        ValidationOutcome outcome,
        ArchitectureAnalysisSnapshotCounters counters,
        ValidateCacheExecutionState state)
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
            // Population is best effort for an already-completed run. An unreadable input while
            // deriving a cache key must never turn a successful validation into an execution error.
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

    internal static AnalysisProfileCacheCounters BuildCacheProfileCounters(
        ValidateCommandOptions options,
        ValidateCacheExecutionState state,
        ArchitectureAnalysisSnapshotCounters? counters)
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

    private static void RecordCacheReject(ValidateCacheExecutionState state, AnalysisCacheRejectReason reason)
    {
        state.Rejects++;
        string key = reason.ToString();
        state.RejectReasonCounts.TryGetValue(key, out int existing);
        state.RejectReasonCounts[key] = existing + 1;
    }
}
