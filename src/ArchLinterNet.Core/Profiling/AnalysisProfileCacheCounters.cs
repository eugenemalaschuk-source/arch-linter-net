namespace ArchLinterNet.Core.Profiling;

// Reserved for issue #365 (persistent cache). Fields are 0/NotApplicable until that capability
// lands — see docs/internal/analysis-profile-dictionary.md.
public sealed record AnalysisProfileCacheCounters
{
    public AnalysisProfileReservedFieldStatus Status { get; init; } = AnalysisProfileReservedFieldStatus.NotApplicable;

    public int Lookups { get; init; }

    public int Hits { get; init; }
}
