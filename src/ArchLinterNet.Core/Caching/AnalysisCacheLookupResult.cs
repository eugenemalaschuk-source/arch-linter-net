namespace ArchLinterNet.Core.Caching;

public sealed record AnalysisCacheLookupResult(
    AnalysisCacheLookupOutcome Outcome,
    AnalysisCacheRejectReason? Reason,
    AnalysisCacheEntryV1? Entry,
    long BytesRead)
{
    public static AnalysisCacheLookupResult Hit(AnalysisCacheEntryV1 entry, long bytesRead) =>
        new(AnalysisCacheLookupOutcome.Hit, null, entry, bytesRead);

    public static AnalysisCacheLookupResult Miss(AnalysisCacheRejectReason reason, long bytesRead = 0) =>
        new(AnalysisCacheLookupOutcome.Miss, reason, null, bytesRead);

    public static AnalysisCacheLookupResult Reject(AnalysisCacheRejectReason reason, long bytesRead = 0) =>
        new(AnalysisCacheLookupOutcome.Reject, reason, null, bytesRead);
}
