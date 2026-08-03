namespace ArchLinterNet.Core.Caching;

// Aggregates real AnalysisCachePopulation.TryLookup outcomes observed by one
// ArchitectureAnalysisSnapshot across every mode it evaluates — the source of truth
// AnalysisProfileCacheCounters.Lookups/Hits/Misses/BytesRead/RejectReasonCounts now populate from,
// instead of staying at 0 (review finding: "only Writes and Rejects are populated").
public sealed class AnalysisCacheLookupStats
{
    private int _lookups;
    private int _hits;
    private int _misses;
    private int _rejects;
    private long _bytesRead;
    private readonly Dictionary<string, int> _rejectReasonCounts = new(StringComparer.Ordinal);

    public void RecordLookup(AnalysisCacheLookupResult result)
    {
        _lookups++;
        _bytesRead += result.BytesRead;

        switch (result.Outcome)
        {
            case AnalysisCacheLookupOutcome.Hit:
                _hits++;
                break;
            case AnalysisCacheLookupOutcome.Miss:
                _misses++;
                break;
            case AnalysisCacheLookupOutcome.Reject:
            default:
                _rejects++;
                RecordReason(result.Reason);
                break;
        }
    }

    private void RecordReason(AnalysisCacheRejectReason? reason)
    {
        if (reason is not { } value)
        {
            return;
        }

        string key = value.ToString();
        _rejectReasonCounts.TryGetValue(key, out int existing);
        _rejectReasonCounts[key] = existing + 1;
    }

    public AnalysisCacheLookupStats Snapshot()
    {
        AnalysisCacheLookupStats copy = new()
        {
            _lookups = _lookups,
            _hits = _hits,
            _misses = _misses,
            _rejects = _rejects,
            _bytesRead = _bytesRead,
        };
        foreach ((string key, int value) in _rejectReasonCounts)
        {
            copy._rejectReasonCounts[key] = value;
        }

        return copy;
    }

    public int Lookups => _lookups;

    public int Hits => _hits;

    public int Misses => _misses;

    public int Rejects => _rejects;

    public long BytesRead => _bytesRead;

    public IReadOnlyDictionary<string, int> RejectReasonCounts => _rejectReasonCounts;
}
