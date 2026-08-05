using ArchLinterNet.Core.Caching;

namespace ArchLinterNet.Core.Validation;

// Minimal typed counters for #363: composition/evaluation counts only. Full profiling/timing
// counters (durations, per-checker breakdowns) are deferred to #374 per
// docs/internal/analysis-build-state-blueprint.md's downstream implementation map, which
// explicitly requires timings/counters to never affect session/snapshot identity.
public sealed record ArchitectureAnalysisSnapshotCounters
{
    public int PolicyCompositions { get; init; }

    public int ProjectGraphEvaluations { get; init; }

    public int AssemblyLoads { get; init; }

    // Actual inventory retained by the completed snapshot. AssemblyLoads measures operations;
    // these counts describe the resulting graph and can therefore differ when resolution reuses
    // an already-loaded assembly or when a selected assembly is missing.
    public int DiscoveredProjectCount { get; init; }

    public int RetainedAssemblyCount { get; init; }

    public int SelectedAssemblyCount { get; init; }

    public int ModesEvaluated { get; init; }

    // One logical snapshot object is materialized for every successful CreateSnapshot call.
    // This is distinct from a post-ensure-built runner reload, which remains internal setup work.
    public int SnapshotMaterializations { get; init; }

    // The session's lazy source-file fact index can materialize at most once for a retained
    // snapshot; source scan counters make that invariant observable to analysis-profile/v1.
    public int FactIndexMaterializations { get; init; }

    public int SourceScanPasses { get; init; }

    public int SourceFilesScanned { get; init; }

    // Bounded parallel scanning (issue #408) instrumentation — see AnalysisProfileConcurrencyCounters,
    // which analysis-profile/v1 sources these from. MaxParallelism is the resolved effective degree
    // for this snapshot (see MaxParallelismResolver); the rest are zero when every scanning phase
    // took the sequential path.
    public int MaxParallelism { get; init; }

    public int ParallelScheduledWorkItems { get; init; }

    public int ParallelCompletedWorkItems { get; init; }

    public int ParallelObservedMaxConcurrency { get; init; }

    public int ParallelMergeOperations { get; init; }

    // Work a verified cache hit skipped relative to materializing and evaluating this prepared
    // snapshot. These are distinct from cache hit counts so a profile can prove what was avoided.
    public int AvoidedAssemblyLoads { get; init; }

    public int AvoidedFactIndexMaterializations { get; init; }

    public int AvoidedSourceScanPasses { get; init; }

    public int AvoidedContractExecutions { get; init; }

    public long AvoidedArtifactBytesLoaded { get; init; }

    // Finding/cycle results produced by each contract family across every evaluated mode.
    // Unlike ContractFamilyCounts, this measures results, not contracts invoked.
    public IReadOnlyDictionary<string, int> ContractFamilyResultCounts { get; init; } =
        new Dictionary<string, int>(StringComparer.Ordinal);

    // Real analysis-cache/v1 lookup instrumentation for this snapshot (see
    // ArchitectureAnalysisSnapshot.CacheStats) — null whenever no cache location was configured for
    // this request, so a host can distinguish "cache not used" from "used, zero lookups so far".
    public AnalysisCacheLookupStats? CacheLookups { get; init; }
}
