using ArchLinterNet.Core.Caching;
using ArchLinterNet.Core.Profiling;
using ArchLinterNet.Core.Reporting;
using ArchLinterNet.Core.Validation;

namespace ArchLinterNet.Testing;

// Testing API ownership wrapper for one ArchitectureAnalysisSnapshot (issue #363): a test
// explicitly creates this via ArchitectureValidationBuilder.CreateSnapshot(), evaluates strict
// and/or audit against the one shared snapshot, and disposes it deterministically (typically via
// a `using` block) instead of paying for one independent policy/project/assembly setup per
// assertion.
//
// Finding #7: an internal cacheContext (see the internal constructor overload
// ArchitectureValidationBuilder.CreateSnapshot() actually uses) lets Evaluate populate the
// analysis cache after each completed, non-cancelled mode — mirroring ValidateCommandHandler's
// TryPopulateCache for the CLI host and ArchitectureValidationBuilder.Validate() for this host's
// own independent-run path — instead of only ever consulting the cache for lookups and never
// seeding a later hit. The public constructor below (unchanged signature) leaves cache population
// disabled, matching this type's prior behavior for any external caller that constructs it
// directly rather than through the builder.
public sealed class ArchitectureValidationSnapshotSession : IDisposable
{
    private readonly ArchitectureAnalysisSnapshot _snapshot;
    private readonly ValidationTiming? _timing;
    private readonly bool _collectProfile;
    private readonly long _allocatedBytesAtStart;
    private readonly ArchitectureValidationCacheSupport.CacheContext _cacheContext;

    public ArchitectureValidationSnapshotSession(
        ArchitectureAnalysisSnapshot snapshot,
        ValidationTiming? timing,
        bool collectProfile = false,
        long allocatedBytesAtStart = 0)
        : this(snapshot, timing, collectProfile, allocatedBytesAtStart, default)
    {
    }

    internal ArchitectureValidationSnapshotSession(
        ArchitectureAnalysisSnapshot snapshot,
        ValidationTiming? timing,
        bool collectProfile,
        long allocatedBytesAtStart,
        ArchitectureValidationCacheSupport.CacheContext cacheContext)
    {
        _snapshot = snapshot;
        _timing = timing;
        _collectProfile = collectProfile;
        _allocatedBytesAtStart = allocatedBytesAtStart;
        _cacheContext = cacheContext;
    }

    public ArchitectureValidationResult ValidateStrict()
    {
        return Evaluate("strict");
    }

    public ArchitectureValidationResult ValidateAudit()
    {
        return Evaluate("audit");
    }

    public ArchitectureAnalysisSnapshotCounters Counters => _snapshot.Counters;

    private ArchitectureValidationResult Evaluate(string mode)
    {
        ValidationOutcome outcome = _snapshot.Evaluate(mode, _timing);

        // Populated unconditionally after every completed Evaluate call, hit or miss — Put is
        // idempotent (same key, same content, harmless re-write) and this exactly mirrors
        // ValidateCommandHandler.Execution.cs's own unconditional post-mode TryPopulateCache call.
        // Evaluate() above never returns to this line at all when cancellation is observed (it
        // rethrows), so "after a completed non-cancelled run" is automatic here, not a separate
        // check. A default cacheContext (CacheOptions null — the public constructor's case) makes
        // TryPopulateCache a no-op, same as before this change for any caller not going through
        // ArchitectureValidationBuilder.CreateSnapshot().
        AnalysisCachePopulation.Outcome populationOutcome =
            ArchitectureValidationCacheSupport.TryPopulateCache(_cacheContext, mode, outcome);

        AnalysisProfile? profile = _collectProfile
            ? AnalysisProfileBuilder.Build(
                _snapshot.Counters, _timing, renderedSinkCount: 0, outputSinkCount: 0,
                ArchitectureValidationBuilder.ResolveCompletionStatus(outcome), cancellationObserved: false,
                new ArchLinterNet.Core.Profiling.AnalysisProfileBuildOptions
                {
                    Measurements = ArchitectureValidationBuilder.CaptureMeasurements(_allocatedBytesAtStart),
                    Cache = ArchitectureValidationCacheSupport.BuildCacheProfileCounters(
                        _cacheContext, _snapshot.Counters, populationOutcome),
                })
            : null;

        return ArchitectureValidationResultMapper.ToResult(outcome, _timing, mode, profile);
    }

    public void Dispose()
    {
        _snapshot.Dispose();
    }
}
