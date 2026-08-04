namespace ArchLinterNet.Core.Caching;

// Shared corruption-reason classification for AnalysisProfileCacheCounters.CorruptionEvents — one
// definition for both hosts (CLI's ValidateCommandHandler.Cache.cs and Testing's
// ArchitectureValidationBuilder/ArchitectureValidationSnapshotSession), matching the
// "one shared Core implementation, not two independently maintained ones" pattern
// AnalysisCachePopulation already established. See finding #8: the Testing host previously left
// CorruptionEvents at zero entirely.
public static class AnalysisCacheCorruptionClassifier
{
    private static readonly string[] _corruptionReasonKeys =
    {
        nameof(AnalysisCacheRejectReason.Corrupt),
        nameof(AnalysisCacheRejectReason.Truncated),
        nameof(AnalysisCacheRejectReason.IntegrityMismatch),
        nameof(AnalysisCacheRejectReason.ForeignSchema),
    };

    public static int CountCorruptionEvents(IReadOnlyDictionary<string, int> rejectReasonCounts)
    {
        int total = 0;
        foreach (string key in _corruptionReasonKeys)
        {
            if (rejectReasonCounts.TryGetValue(key, out int count))
            {
                total += count;
            }
        }

        return total;
    }
}
