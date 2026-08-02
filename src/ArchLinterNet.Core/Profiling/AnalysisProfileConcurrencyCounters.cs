namespace ArchLinterNet.Core.Profiling;

// Reserved for issue #408 (bounded parallel scanning). Fields are 0/NotApplicable until that
// capability lands — see docs/internal/analysis-profile-dictionary.md.
public sealed record AnalysisProfileConcurrencyCounters
{
    public AnalysisProfileReservedFieldStatus Status { get; init; } = AnalysisProfileReservedFieldStatus.NotApplicable;

    public int Workers { get; init; }
}
