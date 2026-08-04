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
    string? RuntimeIdentifier,
    // Finding #2: threaded through so TryEvaluateFromCache's AnalysisCacheKey folds in every
    // remaining result-affecting AnalysisSnapshotRequest dimension — see AnalysisCacheKey's own
    // remarks. IncludeAsmdefContracts/EnforceUnmatchedIgnoredViolationsPolicy are not carried here:
    // the snapshot already stores both directly (they compose the policy itself), so
    // TryEvaluateFromCache reads its own _includeAsmdefContracts/_enforceUnmatchedIgnoredViolationsPolicy
    // fields instead of a second copy.
    IReadOnlyList<string>? PreprocessorSymbols = null,
    string? BaselinePath = null);
