using System.Threading;

namespace ArchLinterNet.Core.Execution;

// Mutable instrumentation owned by one analysis session. It is deliberately separate from the
// immutable public snapshot-counter record: lazy fact-index work happens after the snapshot has
// been created, while callers must still observe its final values through Snapshot.Counters.
internal sealed class AnalysisSessionProfilingCounters
{
    private int _factIndexMaterializations;
    private int _sourceScanPasses;
    private int _sourceFilesScanned;

    public int FactIndexMaterializations => Volatile.Read(ref _factIndexMaterializations);

    public int SourceScanPasses => Volatile.Read(ref _sourceScanPasses);

    public int SourceFilesScanned => Volatile.Read(ref _sourceFilesScanned);

    public void RecordFactIndexMaterialization() => Interlocked.Increment(ref _factIndexMaterializations);

    public void RecordSourceScanPass() => Interlocked.Increment(ref _sourceScanPasses);

    public void RecordSourceFileScanned() => Interlocked.Increment(ref _sourceFilesScanned);
}
