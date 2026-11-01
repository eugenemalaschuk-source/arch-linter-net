namespace ArchLinterNet.Core.Profiling;

// Environment-dependent process measurements. Process fields are null when no host measurements
// were supplied. CycleRecorder is available only when the explicit analysis-profile timing
// observer backed the run.
public sealed record AnalysisProfileMeasurements
{
    public long? PeakWorkingSetBytes { get; init; }

    public long? AllocatedBytesTotal { get; init; }

    public AnalysisProfileCycleRecorderMeasurements? CycleRecorder { get; init; }
}
