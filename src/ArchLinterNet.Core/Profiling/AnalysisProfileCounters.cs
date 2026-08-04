using ArchLinterNet.Core.Validation;

namespace ArchLinterNet.Core.Profiling;

// Deterministic counters only — identical for the same request regardless of whether a
// ValidationTiming instance backed the run. See
// openspec/specs/analysis-profile/spec.md, "Counters prove the one-snapshot and sink-count-only
// invariants".
public sealed record AnalysisProfileCounters
{
    public required int PolicyCompositions { get; init; }

    public required int ProjectGraphEvaluations { get; init; }

    public required int AssemblyLoads { get; init; }

    public required int DiscoveredProjectCount { get; init; }

    public required int RetainedAssemblyCount { get; init; }

    public required int SelectedAssemblyCount { get; init; }

    public required int ModesEvaluated { get; init; }

    public required int SnapshotMaterializations { get; init; }

    public required int FactIndexMaterializations { get; init; }

    public required int SourceScanPasses { get; init; }

    public required int SourceFilesScanned { get; init; }

    // Contract-family name -> number of contracts executed for that family, for whichever mode(s)
    // were evaluated. Sourced from ValidationTiming's per-family Count (see
    // ArchitectureContractExecutor.ExecuteStandardFamily/ExecuteCoverageFamily), not re-derived
    // independently, so it always matches what actually ran.
    public required IReadOnlyDictionary<string, int> ContractFamilyCounts { get; init; }

    public required IReadOnlyDictionary<string, int> ContractFamilyResultCounts { get; init; }

    // Number of distinct output formats rendered for this run (human/json/sarif), deduplicated —
    // requesting the same format for multiple destinations still renders it once. See
    // openspec/specs/multi-sink-output/spec.md, "One analysis serves all sinks".
    public required int RenderedSinkCount { get; init; }

    // Number of configured output destinations (stdout/stderr/file) for this run.
    public required int OutputSinkCount { get; init; }

    public AnalysisProfileCacheCounters Cache { get; init; } = new();

    public AnalysisProfileConcurrencyCounters Concurrency { get; init; } = new();

    public static AnalysisProfileCounters From(
        ArchitectureAnalysisSnapshotCounters snapshotCounters,
        IReadOnlyDictionary<string, int> contractFamilyCounts,
        int renderedSinkCount,
        int outputSinkCount)
    {
        return new AnalysisProfileCounters
        {
            PolicyCompositions = snapshotCounters.PolicyCompositions,
            ProjectGraphEvaluations = snapshotCounters.ProjectGraphEvaluations,
            AssemblyLoads = snapshotCounters.AssemblyLoads,
            DiscoveredProjectCount = snapshotCounters.DiscoveredProjectCount,
            RetainedAssemblyCount = snapshotCounters.RetainedAssemblyCount,
            SelectedAssemblyCount = snapshotCounters.SelectedAssemblyCount,
            ModesEvaluated = snapshotCounters.ModesEvaluated,
            SnapshotMaterializations = snapshotCounters.SnapshotMaterializations,
            FactIndexMaterializations = snapshotCounters.FactIndexMaterializations,
            SourceScanPasses = snapshotCounters.SourceScanPasses,
            SourceFilesScanned = snapshotCounters.SourceFilesScanned,
            ContractFamilyCounts = contractFamilyCounts,
            ContractFamilyResultCounts = snapshotCounters.ContractFamilyResultCounts,
            RenderedSinkCount = renderedSinkCount,
            OutputSinkCount = outputSinkCount,
            Concurrency = BuildConcurrencyCounters(snapshotCounters),
        };
    }

    // NotApplicable means every numeric field is 0 — including MaxParallelism, which is otherwise
    // "the resolved degree regardless of whether it was used" and must not leak through when no
    // phase actually ran in parallel. See
    // openspec/specs/analysis-profile/spec.md, "Cache and concurrency fields are populated when
    // their capability is active".
    private static AnalysisProfileConcurrencyCounters BuildConcurrencyCounters(
        ArchitectureAnalysisSnapshotCounters snapshotCounters)
    {
        if (snapshotCounters.ParallelScheduledWorkItems <= 0)
        {
            return new AnalysisProfileConcurrencyCounters();
        }

        return new AnalysisProfileConcurrencyCounters
        {
            Status = AnalysisProfileReservedFieldStatus.Active,
            MaxParallelism = snapshotCounters.MaxParallelism,
            ScheduledWorkItems = snapshotCounters.ParallelScheduledWorkItems,
            CompletedWorkItems = snapshotCounters.ParallelCompletedWorkItems,
            ObservedMaxConcurrency = snapshotCounters.ParallelObservedMaxConcurrency,
            MergeOperations = snapshotCounters.ParallelMergeOperations,
        };
    }
}
