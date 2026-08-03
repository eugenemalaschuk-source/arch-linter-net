using ArchLinterNet.Core.Caching;

namespace ArchLinterNet.Core.Validation;

// Mode-independent cache-lookup configuration threaded into ArchitectureAnalysisSnapshot so
// Evaluate(mode) can attempt a real cache-hit short-circuit before running EvaluateCore for that
// mode — see ArchitectureAnalysisSnapshot.TryEvaluateFromCache. Null (the default, via
// AnalysisSnapshotRequest.CacheLocation being null) preserves the exact prior behavior: every
// Evaluate call always runs the full pipeline, unchanged.
public sealed record AnalysisSnapshotCacheContext(
    AnalysisCacheLocation Location,
    string? ConditionSetName,
    IReadOnlyCollection<string> ContractIds,
    string? Configuration,
    string? TargetFramework,
    string? Platform,
    string? RuntimeIdentifier);
