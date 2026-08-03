namespace ArchLinterNet.Core.Caching;

// The reusable normalized fact set a cache entry carries — deliberately deterministic counts, not
// a bare `passed` boolean and not full finding detail (which would require deserializing the
// polymorphic IArchitectureDiagnosticPayload closed set; out of this capability's scope — see
// design.md). Independent of report rendering: nothing here is format-specific.
public sealed record AnalysisCacheFactsV1(
    bool Passed,
    int ViolationCount,
    int CoverageFindingCount,
    int CycleCount,
    int UnmatchedIgnoredViolationCount,
    int PolicyConsistencyFindingCount,
    int ClassificationConflictCount,
    int ClassificationMetadataFailureCount,
    int DiscoveredProjectCount,
    int RetainedAssemblyCount,
    int SelectedAssemblyCount);
