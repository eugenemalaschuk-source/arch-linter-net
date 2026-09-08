using ArchLinterNet.Core.Caching;

namespace ArchLinterNet.Cli.Commands.Validate.Application;

// Mutable cache counters belong to the invocation coordinator. Keeping this state outside the
// command façade prevents cache population from becoming a second handler lifecycle owner.
internal sealed class ValidateCacheExecutionState
{
    public int Writes { get; set; }

    public int Rejects { get; set; }

    public long BytesWritten { get; set; }

    public int IneligibleUnitCount { get; set; }

    public int CancelledBeforePublish { get; set; }

    public Dictionary<string, int> RejectReasonCounts { get; } = new(StringComparer.Ordinal);

    public bool AttemptedPopulation { get; set; }

    // Aggregated from ArchitectureAnalysisSnapshot.Counters.CacheLookups across every mode this
    // invocation evaluated — real Lookups/Hits/Misses/BytesRead, not left at 0.
    public AnalysisCacheLookupStats? Lookups { get; set; }
}
