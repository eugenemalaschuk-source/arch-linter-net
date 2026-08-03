using ArchLinterNet.Core.Caching;
using ArchLinterNet.Core.Profiling;
using ArchLinterNet.Core.Validation;

namespace ArchLinterNet.Cli.Commands.Validate;

// Issue #365's --cache option. See openspec/specs/analysis-cache/spec.md.
//
// This wires configuration (disabled/auto/explicit-path resolution, safety validation) and
// population (writing a verified entry after a completed, non-cancelled run whose discovered
// projects are all #406 VerifiedCacheEligible — never true for this repository's own MSBuild
// evaluation today, since EvaluatedBuildInputManifestCollector always reports
// CacheIneligible/"evaluated-msbuild-evidence-incomplete"; see design.md). It intentionally does
// NOT short-circuit ExecuteSingleMode/ExecuteCombinedModes on a cache hit in this change — doing
// that safely requires ArchitectureAnalysisSnapshot/ArchitectureValidationApplicationService to
// accept a pre-computed outcome, which is out of this change's scope (see design.md's deferred
// follow-up).
internal sealed partial class ValidateCommandHandler
{
    private const string CacheDestinationAuto = "auto";

    private sealed class CacheExecutionState
    {
        public int Writes { get; set; }

        public int Rejects { get; set; }

        public Dictionary<string, int> RejectReasonCounts { get; } = new(StringComparer.Ordinal);

        public bool AttemptedPopulation { get; set; }
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

    // Populates only after a completed, non-cancelled run — a preflight-blocked or cancelled
    // attempt never reaches here. Gated internally on #406 VerifiedCacheEligible for every
    // discovered project, so nothing is ever persisted from an unproven build-input manifest.
    private void TryPopulateCache(
        ValidateCommandOptions options,
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
        string repositoryRoot = Path.GetDirectoryName(Path.GetFullPath(options.PolicyPath)) ?? Environment.CurrentDirectory;

        AnalysisCachePopulation.Outcome populationOutcome;
        try
        {
            AnalysisCacheKey key = BuildCacheKey(options, outcome, repositoryRoot);
            AnalysisCacheFactsV1 facts = new(
                outcome.Passed,
                outcome.Violations.Count,
                outcome.CoverageFindings.Count,
                outcome.Cycles.Count,
                outcome.UnmatchedIgnoredViolations.Count,
                outcome.PolicyConsistencyFindings.Count,
                outcome.ClassificationConflicts.Count,
                outcome.ClassificationMetadataFailures.Count,
                counters.DiscoveredProjectCount,
                counters.RetainedAssemblyCount,
                counters.SelectedAssemblyCount);

            populationOutcome = AnalysisCachePopulation.TryPopulate(
                location,
                key,
                outcome.DiscoveredProjectPaths,
                repositoryRoot,
                options.Configuration,
                options.TargetFramework,
                options.Platform,
                options.RuntimeIdentifier,
                facts,
                _cancellationToken);
        }
        catch (OperationCanceledException)
        {
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

        if (populationOutcome.RejectReason is { } reason)
        {
            RecordCacheReject(state, reason);
            return;
        }

        state.Writes++;
    }

    private static void RecordCacheReject(CacheExecutionState state, AnalysisCacheRejectReason reason)
    {
        state.Rejects++;
        string key = reason.ToString();
        state.RejectReasonCounts.TryGetValue(key, out int existing);
        state.RejectReasonCounts[key] = existing + 1;
    }

    private static AnalysisCacheKey BuildCacheKey(
        ValidateCommandOptions options, ValidationOutcome outcome, string repositoryRoot)
    {
        IReadOnlyList<string> policyFiles = outcome.PolicyImportPaths.Count > 0
            ? outcome.PolicyImportPaths
            : new[] { options.PolicyPath };

        return new AnalysisCacheKey(
            AnalysisCacheKey.ComputeRepositoryRootDigest(repositoryRoot),
            AnalysisCacheKey.ComputePolicyDigest(policyFiles),
            AnalysisCacheKey.ComputeModeSet(options.Mode.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)),
            options.ConditionSetName,
            AnalysisCacheKey.ComputeContractIdsDigest(options.ContractIds),
            options.Configuration,
            options.TargetFramework,
            options.Platform,
            options.RuntimeIdentifier);
    }

    private static AnalysisProfileCacheCounters BuildCacheProfileCounters(ValidateCommandOptions options, CacheExecutionState state)
    {
        return new AnalysisProfileCacheCounters
        {
            Status = options.CacheDestination is null
                ? AnalysisProfileReservedFieldStatus.NotApplicable
                : AnalysisProfileReservedFieldStatus.Active,
            Writes = state.Writes,
            Rejects = state.Rejects,
            Mode = ResolveCacheOptions(options).ModeCategory,
            RejectReasonCounts = state.RejectReasonCounts,
        };
    }
}
