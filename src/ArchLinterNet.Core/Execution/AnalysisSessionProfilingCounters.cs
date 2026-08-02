using System.Threading;

namespace ArchLinterNet.Core.Execution;

// Mutable instrumentation owned by one analysis session. It is deliberately separate from the
// immutable public snapshot-counter record: lazy fact-index work happens after the snapshot has
// been created, while callers must still observe its final values through Snapshot.Counters.
internal sealed class AnalysisSessionProfilingCounters
{
    private readonly object _gate = new();
    private readonly Dictionary<string, int> _contractFamilyResultCounts = new(StringComparer.Ordinal);
    private int _factIndexMaterializations;
    private int _sourceScanPasses;
    private int _sourceFilesScanned;

    public int FactIndexMaterializations => Volatile.Read(ref _factIndexMaterializations);

    public int SourceScanPasses => Volatile.Read(ref _sourceScanPasses);

    public int SourceFilesScanned => Volatile.Read(ref _sourceFilesScanned);

    public IReadOnlyDictionary<string, int> ContractFamilyResultCounts
    {
        get
        {
            lock (_gate)
            {
                return new Dictionary<string, int>(_contractFamilyResultCounts, StringComparer.Ordinal);
            }
        }
    }

    public void RecordFactIndexMaterialization() => Interlocked.Increment(ref _factIndexMaterializations);

    public void RecordSourceScanPass() => Interlocked.Increment(ref _sourceScanPasses);

    public void RecordSourceFileScanned() => Interlocked.Increment(ref _sourceFilesScanned);

    // Contract execution can observe cancellation after one family has completed but before the
    // executor returns its aggregate result. Recording each completed contract here keeps a
    // cancelled snapshot's profile truthful rather than losing that completed work.
    public void RecordContractFamilyResults(string family, int resultCount)
    {
        lock (_gate)
        {
            _contractFamilyResultCounts.TryGetValue(family, out int current);
            _contractFamilyResultCounts[family] = current + resultCount;
        }
    }

    // The recorder is scoped to the one executor invocation currently in flight. A completed
    // invocation is folded into ArchitectureAnalysisSnapshotCounters; leaving it here would make
    // the snapshot add its totals a second time when another mode is evaluated.
    public void ResetContractFamilyResultCounts()
    {
        lock (_gate)
        {
            _contractFamilyResultCounts.Clear();
        }
    }
}
