namespace ArchLinterNet.Core.Profiling;

// Environment-dependent process measurements. Both fields are null when no ValidationTiming
// instance backed the profiled run — see AnalysisProfileBuilder.
public sealed record AnalysisProfileMeasurements
{
    public long? PeakWorkingSetBytes { get; init; }

    public long? AllocatedBytesTotal { get; init; }
}
