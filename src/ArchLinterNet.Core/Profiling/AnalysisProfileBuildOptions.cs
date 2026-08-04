namespace ArchLinterNet.Core.Profiling;

// Optional host-provided evidence attached while assembling an analysis profile.
public sealed record AnalysisProfileBuildOptions
{
    public AnalysisProfileMeasurements? Measurements { get; init; }

    public AnalysisProfileOutput? Output { get; init; }

    // Real issue #365 cache instrumentation for this run; null leaves Counters.Cache at its
    // reserved NotApplicable default (see AnalysisProfileCounters.From).
    public AnalysisProfileCacheCounters? Cache { get; init; }
}
