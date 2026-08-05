using ArchLinterNet.Core.Caching;
using ArchLinterNet.Core.Execution;

namespace ArchLinterNet.Core.Validation;

public sealed partial class ArchitectureAnalysisSnapshot
{
    private WorkSnapshot CaptureWorkSnapshot()
    {
        AnalysisSessionProfilingCounters? profiling = _profilingCounters;
        return new WorkSnapshot(
            _counters.AssemblyLoads,
            profiling?.FactIndexMaterializations ?? 0,
            profiling?.SourceScanPasses ?? 0,
            profiling?.ContractExecutions ?? 0,
            GetLoadedAssemblyArtifacts().Sum(artifact => artifact.BytesLoaded));
    }

    private AnalysisCacheWorkProvenanceV1 CreateWorkProvenance(WorkSnapshot before)
    {
        WorkSnapshot after = CaptureWorkSnapshot();
        return new AnalysisCacheWorkProvenanceV1(
            after.AssemblyLoads - before.AssemblyLoads,
            after.FactIndexMaterializations - before.FactIndexMaterializations,
            after.SourceScanPasses - before.SourceScanPasses,
            after.ContractExecutions - before.ContractExecutions,
            after.ArtifactBytesLoaded - before.ArtifactBytesLoaded);
    }

    private readonly record struct WorkSnapshot(
        int AssemblyLoads,
        int FactIndexMaterializations,
        int SourceScanPasses,
        int ContractExecutions,
        long ArtifactBytesLoaded);
}
