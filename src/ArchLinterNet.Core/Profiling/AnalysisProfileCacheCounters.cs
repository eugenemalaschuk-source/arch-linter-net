namespace ArchLinterNet.Core.Profiling;

// Issue #365's persistent cache instrumentation. Status stays NotApplicable (and every field 0)
// when no run configured --cache/WithCache(); it becomes Active whenever the cache participated
// in a run, independent of whether any lookup produced a Hit — see
// docs/internal/analysis-profile-dictionary.md.
public sealed record AnalysisProfileCacheCounters
{
    public AnalysisProfileReservedFieldStatus Status { get; init; } = AnalysisProfileReservedFieldStatus.NotApplicable;

    public int Lookups { get; init; }

    public int Hits { get; init; }

    public int Misses { get; init; }

    public int Rejects { get; init; }

    public int Writes { get; init; }

    public long BytesRead { get; init; }

    public long BytesWritten { get; init; }

    public int IneligibleUnitCount { get; init; }

    public int CorruptionEvents { get; init; }

    public int CancelledBeforePublish { get; init; }

    public int AvoidedAssemblyLoads { get; init; }

    public int AvoidedFactIndexMaterializations { get; init; }

    public int AvoidedSourceScanPasses { get; init; }

    public int AvoidedContractExecutions { get; init; }

    public long AvoidedArtifactBytesLoaded { get; init; }

    // "disabled" | "auto" | "path" — never the resolved absolute cache location.
    public string Mode { get; init; } = "disabled";

    public IReadOnlyDictionary<string, int> RejectReasonCounts { get; init; } =
        new Dictionary<string, int>(StringComparer.Ordinal);
}
