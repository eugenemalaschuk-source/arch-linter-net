using ArchLinterNet.Core.Caching;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Model;

namespace ArchLinterNet.Core.Validation;

// Captures only before/after work evidence for cache authorization. The snapshot remains the
// owner of counters and decides when this stateless projection is taken.
internal static class ArchitectureAnalysisSnapshotCacheWorkProjector
{
    internal static ArchitectureAnalysisSnapshotWorkSnapshot Capture(
        ArchitectureAnalysisSnapshotCounters counters,
        AnalysisSessionProfilingCounters? profiling,
        IReadOnlyCollection<ArchitectureLoadedAssemblyArtifact> loadedArtifacts) =>
        new(
            counters.AssemblyLoads,
            profiling?.FactIndexMaterializations ?? 0,
            profiling?.SourceScanPasses ?? 0,
            profiling?.ContractExecutions ?? 0,
            loadedArtifacts.Sum(artifact => artifact.BytesLoaded));

    internal static AnalysisCacheWorkProvenanceV1 CreateProvenance(
        ArchitectureAnalysisSnapshotWorkSnapshot before,
        ArchitectureAnalysisSnapshotWorkSnapshot after) => new(
            after.AssemblyLoads - before.AssemblyLoads,
            after.FactIndexMaterializations - before.FactIndexMaterializations,
            after.SourceScanPasses - before.SourceScanPasses,
            after.ContractExecutions - before.ContractExecutions,
            after.ArtifactBytesLoaded - before.ArtifactBytesLoaded);
}

internal readonly record struct ArchitectureAnalysisSnapshotWorkSnapshot(
    int AssemblyLoads,
    int FactIndexMaterializations,
    int SourceScanPasses,
    int ContractExecutions,
    long ArtifactBytesLoaded);
