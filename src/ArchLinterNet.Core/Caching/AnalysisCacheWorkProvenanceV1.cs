namespace ArchLinterNet.Core.Caching;

// Measured work from the population run, persisted with its outcome so a cache hit can report
// exactly what that entry avoids without inferring work from policy configuration or path counts.
public sealed record AnalysisCacheWorkProvenanceV1(
    int AssemblyLoads,
    int FactIndexMaterializations,
    int SourceScanPasses,
    int ContractExecutions,
    long ArtifactBytesLoaded);
